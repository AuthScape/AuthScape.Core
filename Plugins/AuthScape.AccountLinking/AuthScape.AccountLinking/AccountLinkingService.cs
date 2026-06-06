using AuthScape.AccountLinking.Models;
using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Context;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AuthScape.AccountLinking;

public class AccountLinkingService : IAccountLinkingService
{
    private readonly DatabaseContext db;
    private readonly UserManager<AppUser> userManager;
    private readonly ILogger<AccountLinkingService> logger;

    public AccountLinkingService(
        DatabaseContext db,
        UserManager<AppUser> userManager,
        ILogger<AccountLinkingService> logger)
    {
        this.db = db;
        this.userManager = userManager;
        this.logger = logger;
    }

    public async Task<AccountLinkResult> ResolveAsync(
        ExternalIdentity identity,
        AccountLinkingPolicy policy,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identity.Provider) || string.IsNullOrEmpty(identity.ExternalUserId))
            throw new ArgumentException("Provider and ExternalUserId are required on ExternalIdentity.");

        // Step 1 — fast path: already linked via AspNetUserLogins.
        var existingLogin = await userManager.FindByLoginAsync(identity.Provider, identity.ExternalUserId);
        if (existingLogin != null)
        {
            var auditId = await WriteAuditAsync(identity, policy, AccountLinkOutcome.ReturnedExistingLink,
                existingLogin.Id, ipAddress, userAgent, verificationTokenHash: null, cancellationToken);
            return AccountLinkResult.AlreadyLinked(existingLogin.Id, auditId);
        }

        // Step 2 — apply policy.
        // No email at all → can only create new (cannot collide).
        if (string.IsNullOrEmpty(identity.Email))
        {
            return await CreateNewUserAndLinkAsync(identity, policy, AccountLinkOutcome.NewUserCreated,
                ipAddress, userAgent, cancellationToken);
        }

        var existingByEmail = await userManager.FindByEmailAsync(identity.Email);

        switch (policy)
        {
            case AccountLinkingPolicy.NeverAutoLink:
                if (existingByEmail != null)
                {
                    // Email collision under strict policy → defer to verification UX.
                    var (token, hash) = GenerateVerificationToken();
                    var auditId = await WriteAuditAsync(identity, policy, AccountLinkOutcome.PendingManualVerification,
                        appUserId: null, ipAddress, userAgent, verificationTokenHash: hash, cancellationToken);
                    logger.LogInformation(
                        "Account-linking deferred for verification: provider={Provider} email={Email} existingUser={UserId} auditId={AuditId}",
                        identity.Provider, identity.Email, existingByEmail.Id, auditId);
                    return AccountLinkResult.PendingVerification(auditId,
                        $"An account with {identity.Email} already exists. We've emailed a verification link to complete the connection. (token={token})");
                }
                return await CreateNewUserAndLinkAsync(identity, policy, AccountLinkOutcome.NewUserCreated,
                    ipAddress, userAgent, cancellationToken);

            case AccountLinkingPolicy.LinkIfEmailVerified:
                if (existingByEmail != null && identity.EmailVerifiedByProvider)
                {
                    return await LinkExistingAsync(existingByEmail, identity, policy, ipAddress, userAgent, cancellationToken);
                }
                if (existingByEmail != null && !identity.EmailVerifiedByProvider)
                {
                    // Account-takeover-safe path: email collides but provider didn't verify → create new user, do NOT link.
                    logger.LogWarning(
                        "Refusing to auto-link unverified email: provider={Provider} email={Email} existingUser={UserId} — creating separate AppUser instead",
                        identity.Provider, identity.Email, existingByEmail.Id);
                    return await CreateNewUserAndLinkAsync(identity, policy,
                        AccountLinkOutcome.EmailUnverifiedNewUserCreated, ipAddress, userAgent, cancellationToken);
                }
                return await CreateNewUserAndLinkAsync(identity, policy, AccountLinkOutcome.NewUserCreated,
                    ipAddress, userAgent, cancellationToken);

            case AccountLinkingPolicy.AlwaysLinkByEmail:
                if (existingByEmail != null)
                {
                    return await LinkExistingAsync(existingByEmail, identity, policy, ipAddress, userAgent, cancellationToken);
                }
                return await CreateNewUserAndLinkAsync(identity, policy, AccountLinkOutcome.NewUserCreated,
                    ipAddress, userAgent, cancellationToken);

            default:
                throw new InvalidOperationException($"Unknown AccountLinkingPolicy: {policy}");
        }
    }

    public async Task<AccountLinkResult> CompleteVerifiedLinkAsync(
        long auditLogId,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        var pending = await db.Set<AccountLinkAuditLog>()
            .FirstOrDefaultAsync(a => a.Id == auditLogId, cancellationToken);
        if (pending == null || pending.Outcome != AccountLinkOutcome.PendingManualVerification)
            throw new InvalidOperationException("Audit log entry not found or not in pending state.");

        var providedHash = HashToken(verificationToken);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedHash),
                Encoding.UTF8.GetBytes(pending.VerificationTokenHash ?? "")))
        {
            throw new InvalidOperationException("Verification token does not match.");
        }

        if (string.IsNullOrEmpty(pending.Email))
            throw new InvalidOperationException("Pending audit row has no email; cannot complete link.");

        var existing = await userManager.FindByEmailAsync(pending.Email)
            ?? throw new InvalidOperationException("Existing AppUser for email not found at completion time.");

        var addLogin = await userManager.AddLoginAsync(existing,
            new UserLoginInfo(pending.Provider, pending.ExternalUserId, pending.Provider));
        if (!addLogin.Succeeded)
            throw new InvalidOperationException("Failed to add external login: "
                + string.Join("; ", addLogin.Errors.Select(e => e.Description)));

        var newAuditId = await WriteAuditAsync(
            new ExternalIdentity
            {
                Provider = pending.Provider,
                ExternalUserId = pending.ExternalUserId,
                Email = pending.Email,
                EmailVerifiedByProvider = true, // user proved control via the email link
                CompanyId = pending.CompanyId
            },
            pending.PolicyApplied,
            AccountLinkOutcome.LinkedToExistingUser,
            existing.Id,
            ipAddress: null,
            userAgent: null,
            verificationTokenHash: null,
            cancellationToken);

        return AccountLinkResult.Linked(existing.Id, newAuditId);
    }

    // ---- internals ----

    private async Task<AccountLinkResult> LinkExistingAsync(
        AppUser existing,
        ExternalIdentity identity,
        AccountLinkingPolicy policy,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var addLogin = await userManager.AddLoginAsync(existing,
            new UserLoginInfo(identity.Provider, identity.ExternalUserId, identity.ProviderDisplayName ?? identity.Provider));
        if (!addLogin.Succeeded)
        {
            // Most common case: the (Provider, ExternalUserId) is already attached to a *different* AppUser.
            // Treat this as a hard failure rather than silently rebinding.
            throw new InvalidOperationException("Failed to attach external login to AppUser: "
                + string.Join("; ", addLogin.Errors.Select(e => e.Description)));
        }

        var auditId = await WriteAuditAsync(identity, policy, AccountLinkOutcome.LinkedToExistingUser,
            existing.Id, ipAddress, userAgent, verificationTokenHash: null, cancellationToken);
        return AccountLinkResult.Linked(existing.Id, auditId);
    }

    private async Task<AccountLinkResult> CreateNewUserAndLinkAsync(
        ExternalIdentity identity,
        AccountLinkingPolicy policy,
        AccountLinkOutcome outcome,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        // Use a non-colliding userName when no email or when email already taken by a different identity.
        var userName = !string.IsNullOrEmpty(identity.Email)
            && await userManager.FindByEmailAsync(identity.Email) == null
            ? identity.Email
            : $"{identity.Provider}:{identity.ExternalUserId}";

        var user = new AppUser
        {
            UserName = userName,
            Email = identity.Email,
            EmailConfirmed = identity.EmailVerifiedByProvider,
            FirstName = identity.FirstName ?? "",
            LastName = identity.LastName ?? "",
            Created = DateTimeOffset.UtcNow,
            CompanyId = identity.CompanyId
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            throw new InvalidOperationException("Failed to create AppUser: "
                + string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var addLogin = await userManager.AddLoginAsync(user,
            new UserLoginInfo(identity.Provider, identity.ExternalUserId, identity.ProviderDisplayName ?? identity.Provider));
        if (!addLogin.Succeeded)
            throw new InvalidOperationException("Failed to attach external login to new AppUser: "
                + string.Join("; ", addLogin.Errors.Select(e => e.Description)));

        var auditId = await WriteAuditAsync(identity, policy, outcome,
            user.Id, ipAddress, userAgent, verificationTokenHash: null, cancellationToken);

        return outcome == AccountLinkOutcome.EmailUnverifiedNewUserCreated
            ? AccountLinkResult.UnverifiedEmailNewUser(user.Id, auditId)
            : AccountLinkResult.Created(user.Id, auditId);
    }

    private async Task<long> WriteAuditAsync(
        ExternalIdentity identity,
        AccountLinkingPolicy policy,
        AccountLinkOutcome outcome,
        long? appUserId,
        string? ipAddress,
        string? userAgent,
        string? verificationTokenHash,
        CancellationToken cancellationToken)
    {
        var row = new AccountLinkAuditLog
        {
            Timestamp = DateTime.UtcNow,
            AppUserId = appUserId,
            Provider = identity.Provider,
            ExternalUserId = identity.ExternalUserId,
            Email = identity.Email,
            EmailVerifiedByProvider = identity.EmailVerifiedByProvider,
            PolicyApplied = policy,
            Outcome = outcome,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CompanyId = identity.CompanyId,
            VerificationTokenHash = verificationTokenHash
        };
        db.Set<AccountLinkAuditLog>().Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private static (string token, string hash) GenerateVerificationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, HashToken(token));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

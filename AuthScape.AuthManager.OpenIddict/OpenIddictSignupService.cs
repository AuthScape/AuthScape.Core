using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;

namespace AuthScape.AuthManager.OpenIddict;

/// <summary>
/// Creates local <see cref="AppUser"/> records through ASP.NET Identity's <see cref="UserManager{T}"/>.
/// This preserves the existing OpenIddict signup behavior (password policy, lockout, etc.) — the
/// only change is that callers now go through <see cref="IAuthScapeSignupService"/> instead of
/// hand-rolling a UserManager call.
/// </summary>
public sealed class OpenIddictSignupService : IAuthScapeSignupService
{
    private readonly UserManager<AppUser> userManager;

    public OpenIddictSignupService(UserManager<AppUser> userManager)
    {
        this.userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<SignupResult> SignUpAsync(SignupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return SignupResult.Fail(SignupErrorCode.ValidationFailed, "Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return SignupResult.Fail(SignupErrorCode.ValidationFailed, "Password is required for OpenIddict signup.");

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return SignupResult.Fail(SignupErrorCode.DuplicateEmail, $"A user with email '{request.Email}' already exists.");

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.GivenName ?? "",
            LastName = request.FamilyName ?? "",
            Created = DateTimeOffset.UtcNow,
            IsActive = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // Surface password-policy failures distinctly so the UI can show a useful message.
            var code = result.Errors.Any(e =>
                e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                ? SignupErrorCode.PasswordPolicyFailed
                : SignupErrorCode.ValidationFailed;
            return SignupResult.Fail(code, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return SignupResult.Ok(user.Id.ToString());
    }
}

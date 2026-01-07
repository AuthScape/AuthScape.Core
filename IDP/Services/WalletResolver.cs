namespace IDP.Services
{
    using AuthScape.Models.PaymentGateway;
    using global::Services.Context;
    using global::Services.Database;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using Stripe;
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IWalletResolver
    {
        Task<Wallet> GetActiveAsync(HttpContext http, CancellationToken ct = default);
        Task EnsureStripeCustomerAsync(Wallet wallet, CancellationToken ct = default);
        Task<bool> CanManageAsync(long userId, Wallet wallet, CancellationToken ct = default);
    }

    public class WalletResolver : IWalletResolver
    {
        private readonly DatabaseContext _db;

        public WalletResolver(DatabaseContext db, IOptions<AppSettings> config)
        {
            _db = db;

            // Set Stripe API key from appsettings
            Stripe.StripeConfiguration.ApiKey = config.Value.Stripe.SecretKey;
        }

        public async Task<Wallet> GetActiveAsync(HttpContext http, CancellationToken ct = default)
        {
            if (!(http.User.Identity?.IsAuthenticated ?? false))
                throw new UnauthorizedAccessException("User is not authenticated.");

            // Try common claim keys in order
            var idValue =
                http.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                http.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                http.User.FindFirstValue("oid"); // Azure AD object id

            if (string.IsNullOrWhiteSpace(idValue))
                throw new UnauthorizedAccessException("Authenticated user is missing an ID claim (NameIdentifier/sub/oid).");

            if (!long.TryParse(idValue, out var currentUserId))
                throw new InvalidOperationException($"User ID claim is not a long: '{idValue}'.");

            // Explicit walletId?
            if (Guid.TryParse(http.Request.Query["walletId"], out var wid))
            {
                var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == wid, ct)
                    ?? throw new Exception("Wallet not found.");
                if (!await CanManageAsync(currentUserId, wallet, ct)) throw new UnauthorizedAccessException();
                return wallet;
            }

            // Default: user’s personal wallet (create if missing)
            var personal = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == currentUserId, ct);
            if (personal == null)
            {
                personal = new Wallet { UserId = currentUserId };
                _db.Wallets.Add(personal);
                await _db.SaveChangesAsync(ct);
            }
            return personal;
        }

        public async Task EnsureStripeCustomerAsync(Wallet wallet, CancellationToken ct = default)
        {
            var customerService = new CustomerService();

            // If we have a customer ID, verify it still exists in Stripe
            if (!string.IsNullOrWhiteSpace(wallet.PaymentCustomerId))
            {
                try
                {
                    var existing = await customerService.GetAsync(wallet.PaymentCustomerId, cancellationToken: ct);
                    if (existing != null && !existing.Deleted.GetValueOrDefault())
                    {
                        return; // Customer exists and is valid
                    }
                }
                catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
                {
                    // Customer doesn't exist in Stripe - will create a new one below
                }
            }

            // Create a new Stripe customer
            var cust = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Name = wallet.CompanyId != null ? $"Company:{wallet.CompanyId}" : $"User:{wallet.UserId}",
                Metadata = new Dictionary<string, string> { ["wallet_id"] = wallet.Id.ToString() }
            }, cancellationToken: ct);

            wallet.PaymentCustomerId = cust.Id;
            _db.Update(wallet);
            await _db.SaveChangesAsync(ct);
        }

        public Task<bool> CanManageAsync(long userId, Wallet wallet, CancellationToken ct = default)
        {
            // Personal
            if (wallet.UserId == userId) return Task.FromResult(true);

            // TODO: check your company membership/roles if wallet.CompanyId != null
            return Task.FromResult(false);
        }
    }

}

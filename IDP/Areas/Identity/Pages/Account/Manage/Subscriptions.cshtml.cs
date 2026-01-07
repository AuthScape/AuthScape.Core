using AuthScape.Models.PaymentGateway.Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Context;
using Stripe;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IDP.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class SubscriptionsModel : PageModel
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<SubscriptionsModel> _logger;

        public Guid ActiveWalletId { get; set; }

        public SubscriptionsModel(DatabaseContext context, ILogger<SubscriptionsModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Try common claim keys in order (matching WalletResolver pattern)
                var idValue =
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId) || userId == 0)
                {
                    _logger.LogWarning("User ID claim not found or invalid");
                    return RedirectToPage("/Account/Login");
                }

                // Get or create user's wallet
                var wallet = await _context.Wallets
                    .FirstOrDefaultAsync(w => w.UserId == userId);

                if (wallet == null)
                {
                    // Create wallet if it doesn't exist
                    wallet = new AuthScape.Models.PaymentGateway.Wallet
                    {
                        UserId = userId
                    };
                    _context.Wallets.Add(wallet);
                    await _context.SaveChangesAsync();
                }

                ActiveWalletId = wallet.Id;

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading subscriptions page");
                return Page();
            }
        }

        /// <summary>
        /// Remove a stale subscription that no longer exists in Stripe (e.g., from a copied database).
        /// </summary>
        public async Task<IActionResult> OnPostRemoveStaleAsync(Guid subscriptionId)
        {
            try
            {
                // Get user ID
                var idValue =
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId) || userId == 0)
                {
                    return new JsonResult(new { success = false, error = "User not authenticated" });
                }

                // Get user's wallet
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null)
                {
                    return new JsonResult(new { success = false, error = "Wallet not found" });
                }

                // Find the subscription
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.WalletId == wallet.Id);

                if (subscription == null)
                {
                    return new JsonResult(new { success = false, error = "Subscription not found" });
                }

                // Try to cancel in Stripe, but don't fail if it doesn't exist
                if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
                {
                    try
                    {
                        var stripeService = new SubscriptionService();
                        await stripeService.CancelAsync(subscription.StripeSubscriptionId);
                    }
                    catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
                    {
                        // Subscription doesn't exist in Stripe - this is expected for stale subscriptions
                        _logger.LogInformation("Subscription {StripeId} not found in Stripe, removing locally", subscription.StripeSubscriptionId);
                    }
                }

                // Mark as canceled locally
                subscription.Status = SubscriptionStatus.Canceled;
                subscription.CanceledAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stale subscription {SubscriptionId}", subscriptionId);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }
    }
}

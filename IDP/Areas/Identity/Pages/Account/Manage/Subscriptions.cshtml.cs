using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Context;
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
    }
}

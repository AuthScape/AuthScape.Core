using AuthScape.Models.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AuthScape.Models.PaymentGateway;
using IDP.Services;
using Microsoft.EntityFrameworkCore;

namespace IDP.Areas.Identity.Pages.Account.Manage
{
    public class WalletModel : PageModel
    {
        private readonly IWalletResolver _resolver;
        private readonly DatabaseContext _db;      // your DbContext
        private readonly AppSettings _cfg;         // your settings (contains Stripe keys)

        public WalletModel(IWalletResolver resolver, DatabaseContext db, IOptions<AppSettings> cfg)
        {
            _resolver = resolver;
            _db = db;
            _cfg = cfg.Value;
        }

        // Data the page needs
        public List<WalletPaymentMethod> Methods { get; private set; } = new();
        public decimal Balance { get; private set; }
        public Guid ActiveWalletId { get; private set; }
        public string StripePublicKey => _cfg.Stripe.PublishableKey;

        // GET /Wallets
        public async Task<IActionResult> OnGetAsync()
        {
            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            ActiveWalletId = wallet.Id;

            Methods = await _db.WalletPaymentMethods
                .Where(pm => pm.WalletId == wallet.Id && pm.Archived == null)
                .OrderBy(pm => pm.Brand)
                .ToListAsync();

            Balance = await _db.WalletTransactions
                .Where(t => t.WalletId == wallet.Id)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return Page();
        }

        // POST /Wallets?handler=SetupIntent   (AJAX from modal)
        public async Task<IActionResult> OnPostSetupIntentAsync(Guid? walletId)
        {
            var wallet = await _resolver.GetActiveAsync(HttpContext); // respects ?walletId if you implement it in resolver
            await _resolver.EnsureStripeCustomerAsync(wallet);

            var si = await new SetupIntentService().CreateAsync(new SetupIntentCreateOptions
            {
                Customer = wallet.PaymentCustomerId,
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = new Dictionary<string, string> { ["wallet_id"] = wallet.Id.ToString() }
            });

            return new JsonResult(new { clientSecret = si.ClientSecret });
        }

        // POST /Wallets?handler=AddFunds      (AJAX → returns Checkout URL)
        public async Task<IActionResult> OnPostAddFundsAsync(long amountCents, Guid? walletId)
        {
            if (amountCents < 100) return BadRequest("Minimum $1");

            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            var session = await new SessionService().CreateAsync(new SessionCreateOptions
            {
                Mode = "payment",
                Customer = wallet.PaymentCustomerId,
                SuccessUrl = Url.PageLink("/Wallets/Index", values: new { walletId = wallet.Id })!,
                CancelUrl = Url.PageLink("/Wallets/Index", values: new { walletId = wallet.Id })!,
                LineItems = new List<SessionLineItemOptions> {
                new() {
                  Quantity = 1,
                  PriceData = new SessionLineItemPriceDataOptions {
                      Currency = "usd",
                      UnitAmount = amountCents,
                      ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Wallet top-up" }
                  }
                }
            },
                Metadata = new Dictionary<string, string>
                {
                    ["wallet_id"] = wallet.Id.ToString(),
                    ["intent"] = "wallet_topup"
                }
            });

            return new JsonResult(new { url = session.Url });
        }

        // POST /Wallets?handler=MakeDefault&pmId=pm_xxx
        public async Task<IActionResult> OnPostMakeDefaultAsync(string pmId, Guid? walletId)
        {
            if (string.IsNullOrWhiteSpace(pmId)) return BadRequest();

            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await new CustomerService().UpdateAsync(wallet.PaymentCustomerId,
                new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = pmId }
                });

            return new OkResult();
        }

        // POST /Wallets?handler=DeleteMethod&pmId=pm_xxx
        public async Task<IActionResult> OnPostDeleteMethodAsync(string pmId, Guid? walletId)
        {
            if (string.IsNullOrWhiteSpace(pmId)) return BadRequest();

            await new PaymentMethodService().DetachAsync(pmId);

            var local = await _db.WalletPaymentMethods.FirstOrDefaultAsync(p => p.PaymentMethodId == pmId);
            if (local != null)
            {
                local.Archived = DateTimeOffset.UtcNow;
                _db.Update(local);
                await _db.SaveChangesAsync();
            }

            return new OkResult();
        }
    }
}

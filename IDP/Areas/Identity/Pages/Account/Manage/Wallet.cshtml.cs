using AuthScape.Models.PaymentGateway;
using AuthScape.Models.Users;
using IDP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Areas.Identity.Pages.Account.Manage
{
    public class WalletModel : PageModel
    {
        private readonly IWalletResolver _resolver;
        private readonly DatabaseContext _db;
        private readonly AppSettings _cfg;

        private readonly SetupIntentService _siService;
        private readonly CustomerService _customerService;
        private readonly PaymentMethodService _pmService;

        public WalletModel(IWalletResolver resolver, DatabaseContext db, IOptions<AppSettings> cfg)
        {
            _resolver = resolver;
            _db = db;
            _cfg = cfg.Value;

            _siService = new SetupIntentService();
            _customerService = new CustomerService();
            _pmService = new PaymentMethodService();
        }

        // Data for the page
        public List<WalletPaymentMethod> Methods { get; private set; } = new();
        public decimal Balance { get; private set; }
        public Guid ActiveWalletId { get; private set; }
        public string StripePublicKey => _cfg.Stripe.PublishableKey;

        // GET /Wallets
        public async Task<IActionResult> OnGetAsync(
            [FromQuery(Name = "setup_intent")] string? setupIntentId = null,
            [FromQuery(Name = "setup_intent_client_secret")] string? setupIntentClientSecret = null,
            [FromQuery(Name = "redirect_status")] string? redirectStatus = null)
        {
            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);
            ActiveWalletId = wallet.Id;

            // Handle SetupIntent redirect result if present
            if (!string.IsNullOrEmpty(setupIntentId))
            {
                var si = await _siService.GetAsync(setupIntentId, new SetupIntentGetOptions
                {
                    Expand = new List<string> { "payment_method", "latest_attempt" }
                });

                // Ownership checks
                if (si.Metadata != null &&
                    si.Metadata.TryGetValue("wallet_id", out var walletIdFromSi) &&
                    !string.Equals(walletIdFromSi, wallet.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                    return RedirectToPage("Wallets", new { error = "wallet_mismatch" });

                if (!string.Equals(si.CustomerId, wallet.PaymentCustomerId, StringComparison.Ordinal))
                    return RedirectToPage("Wallets", new { error = "customer_mismatch" });

                switch (si.Status)
                {
                    case "succeeded":
                        await SavePaymentMethodAsync(wallet, si);
                        return RedirectToPage("Wallets", new { success = "setup_complete" });

                    case "requires_action":
                        if (si.NextAction?.Type == "verify_with_microdeposits")
                        {
                            ViewData["AchVerifyPending"] = true;
                            ViewData["AchHostedUrl"] = si.NextAction.VerifyWithMicrodeposits?.HostedVerificationUrl;
                            ViewData["AchVerifyHint"] = si.NextAction.VerifyWithMicrodeposits?.MicrodepositType; // "amounts" or "descriptor_code"
                            break; // fall through to render page with banner
                        }
                        return RedirectToPage("Wallets", new { error = si.Status });

                    case "processing":
                        ViewData["Processing"] = true;
                        break;

                    case "requires_payment_method":
                        return RedirectToPage("Wallets", new { error = "requires_payment_method" });

                    case "canceled":
                        return RedirectToPage("Wallets", new { error = "canceled" });

                    default:
                        return RedirectToPage("Wallets", new { error = "unexpected_status", status = si.Status });
                }
            }

            // Normal page load
            Methods = await _db.WalletPaymentMethods
                .Where(pm => pm.WalletId == wallet.Id && pm.Archived == null)
                .OrderBy(pm => pm.Brand)
                .ToListAsync();

            Balance = await _db.WalletTransactions
                .Where(t => t.WalletId == wallet.Id)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return Page();
        }

        // Create SetupIntent for Payment Element (ACH + Card)
        // IMPORTANT: For Payment Element, us_bank_account verification_method must be "automatic" | "instant" | "skip"
        // POST /Wallets?handler=SetupIntent
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSetupIntentAsync(Guid? walletId)
        {
            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            var opts = new SetupIntentCreateOptions
            {
                Customer = wallet.PaymentCustomerId,
                PaymentMethodTypes = new List<string> { "us_bank_account", "card" },
                Metadata = new Dictionary<string, string> { ["wallet_id"] = wallet.Id.ToString() },
                PaymentMethodOptions = new SetupIntentPaymentMethodOptionsOptions
                {
                    UsBankAccount = new SetupIntentPaymentMethodOptionsUsBankAccountOptions
                    {
                        VerificationMethod = "automatic" // allows instant, can fall back to micro-deposits
                    }
                }
            };

            var si = await _siService.CreateAsync(opts);
            return new JsonResult(new { clientSecret = si.ClientSecret });
        }

        // NEW: Status probe so UI can show micro-deposits banner even without redirect params
        // POST /Wallets?handler=AchStatus
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAchStatusAsync(string? setupIntentId = null, string? clientSecret = null)
        {
            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            string? siId = !string.IsNullOrWhiteSpace(setupIntentId)
                ? setupIntentId
                : ExtractSetupIntentIdFromClientSecret(clientSecret);

            if (string.IsNullOrWhiteSpace(siId))
            {
                // If you persist SI IDs when creating them, you can fetch the latest here from your DB.
                return new JsonResult(new { found = false, needsVerification = false });
            }

            var si = await _siService.GetAsync(siId, new SetupIntentGetOptions
            {
                Expand = new List<string> { "latest_attempt", "payment_method" }
            });

            if (si == null ||
                !string.Equals(si.CustomerId, wallet.PaymentCustomerId, StringComparison.Ordinal) ||
                (si.Metadata?.TryGetValue("wallet_id", out var wid) == true &&
                 !string.Equals(wid, wallet.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                return new JsonResult(new { found = false, needsVerification = false });
            }

            if (string.Equals(si.Status, "succeeded", StringComparison.Ordinal))
            {
                await SavePaymentMethodAsync(wallet, si); // idempotent
                return new JsonResult(new { found = true, needsVerification = false, status = si.Status });
            }

            if (string.Equals(si.Status, "requires_action", StringComparison.Ordinal) &&
                si.NextAction?.Type == "verify_with_microdeposits")
            {
                var hosted = si.NextAction.VerifyWithMicrodeposits?.HostedVerificationUrl;
                var method = si.NextAction.VerifyWithMicrodeposits?.MicrodepositType; // "amounts" | "descriptor_code"
                return new JsonResult(new
                {
                    found = true,
                    needsVerification = true,
                    status = si.Status,
                    setupIntentId = si.Id,
                    clientSecret = si.ClientSecret,
                    hostedVerificationUrl = hosted,
                    microdepositType = method
                });
            }

            return new JsonResult(new { found = true, needsVerification = false, status = si.Status });
        }

        // Verify ACH micro-deposits server-side (two amounts or descriptor code)
        // POST /Wallets?handler=VerifyAch
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostVerifyAchAsync(
            string? setupIntentId,
            string? clientSecret,
            long? amount1,
            long? amount2,
            string? descriptorCode)
        {
            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            var siId = !string.IsNullOrWhiteSpace(setupIntentId)
                ? setupIntentId
                : ExtractSetupIntentIdFromClientSecret(clientSecret);

            if (string.IsNullOrWhiteSpace(siId))
                return BadRequest("Missing setup intent id/client secret.");

            var si = await _siService.GetAsync(siId, new SetupIntentGetOptions
            {
                Expand = new List<string> { "payment_method", "latest_attempt" }
            });

            if (!string.Equals(si.CustomerId, wallet.PaymentCustomerId, StringComparison.Ordinal))
                return BadRequest("Customer mismatch.");
            if (si.Metadata != null &&
                si.Metadata.TryGetValue("wallet_id", out var walletIdFromSi) &&
                !string.Equals(walletIdFromSi, wallet.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                return BadRequest("Wallet mismatch.");

            var verifyOpts = new SetupIntentVerifyMicrodepositsOptions();
            if (!string.IsNullOrWhiteSpace(descriptorCode))
            {
                verifyOpts.DescriptorCode = descriptorCode.Trim();
            }
            else if (amount1.HasValue && amount2.HasValue)
            {
                if (amount1 <= 0 || amount2 <= 0) return BadRequest("Amounts must be > 0.");
                verifyOpts.Amounts = new List<long?> { amount1.Value, amount2.Value }; // cents
            }
            else
            {
                return BadRequest("Provide either two amounts or a descriptor code.");
            }

            var verified = await _siService.VerifyMicrodepositsAsync(siId, verifyOpts);

            if (string.Equals(verified.Status, "succeeded", StringComparison.Ordinal))
            {
                await SavePaymentMethodAsync(wallet, verified);
                return new JsonResult(new { ok = true, status = "succeeded" });
            }

            return new JsonResult(new { ok = true, status = verified.Status });
        }

        // Checkout top-ups (card + ACH)
        // POST /Wallets?handler=AddFunds
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAddFundsAsync(long amountCents, Guid? walletId)
        {
            if (amountCents < 100) return BadRequest("Minimum $1");

            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            var session = await new SessionService().CreateAsync(new SessionCreateOptions
            {
                Mode = "payment",
                Customer = wallet.PaymentCustomerId,
                PaymentMethodTypes = new List<string> { "card", "us_bank_account" },
                SuccessUrl = Url.PageLink("/Wallets/Index", values: new { walletId = wallet.Id, checkout = "success" })!,
                CancelUrl = Url.PageLink("/Wallets/Index", values: new { walletId = wallet.Id, checkout = "cancel" })!,
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

        // POST /Wallets?handler=MakeDefault
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostMakeDefaultAsync(string pmId, Guid? walletId)
        {
            if (string.IsNullOrWhiteSpace(pmId)) return BadRequest();

            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _customerService.UpdateAsync(wallet.PaymentCustomerId,
                new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = pmId }
                });

            return new OkResult();
        }

        // POST /Wallets?handler=DeleteMethod
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteMethodAsync(string pmId, Guid? walletId)
        {
            if (string.IsNullOrWhiteSpace(pmId)) return BadRequest();

            await _pmService.DetachAsync(pmId);

            var local = await _db.WalletPaymentMethods.FirstOrDefaultAsync(p => p.PaymentMethodId == pmId);
            if (local != null)
            {
                local.Archived = DateTimeOffset.UtcNow;
                _db.Update(local);
                await _db.SaveChangesAsync();
            }

            return new OkResult();
        }

        // -------- helpers --------

        private static string? ExtractSetupIntentIdFromClientSecret(string? clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientSecret)) return null;
            var i = clientSecret.IndexOf("_secret_", StringComparison.Ordinal);
            return i > 0 ? clientSecret.Substring(0, i) : clientSecret;
        }

        private async Task SavePaymentMethodAsync(Wallet wallet, SetupIntent si)
        {
            var pmId = si.PaymentMethodId ?? si.PaymentMethod?.Id;
            if (string.IsNullOrEmpty(pmId)) return;

            // Make default (optional)
            await _customerService.UpdateAsync(wallet.PaymentCustomerId, new CustomerUpdateOptions
            {
                InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = pmId }
            });

            var pm = si.PaymentMethod ?? await _pmService.GetAsync(pmId);

            // Card fields
            string? brand = pm.Card?.Brand;
            string? last4 = pm.Card?.Last4 ?? pm.UsBankAccount?.Last4;
            long? expMonth = pm.Card?.ExpMonth;
            long? expYear = pm.Card?.ExpYear;
            string? funding = pm.Card?.Funding;
            string? fingerprint = pm.Card?.Fingerprint ?? pm.UsBankAccount?.Fingerprint;

            // Bank fields
            string? bankName = pm.UsBankAccount?.BankName;
            string? routingNumber = pm.UsBankAccount?.RoutingNumber;
            string? accountType = pm.UsBankAccount?.AccountType;
            string? accountHolder = pm.UsBankAccount?.AccountHolderType;

            WalletType walletType = pm.Type switch
            {
                "card" => WalletType.card,
                "us_bank_account" => WalletType.us_bank_account,
                "sepa_debit" => WalletType.sepa_debit,
                "bacs_debit" => WalletType.bacs_debit,
                "acss_debit" => WalletType.acss_debit,
                "link" => WalletType.link,
                _ => WalletType.card
            };

            var existing = await _db.WalletPaymentMethods
                .FirstOrDefaultAsync(x => x.WalletId == wallet.Id && x.PaymentMethodId == pm.Id);

            if (existing == null)
            {
                _db.WalletPaymentMethods.Add(new WalletPaymentMethod
                {
                    WalletId = wallet.Id,
                    PaymentMethodId = pm.Id,
                    Brand = brand,
                    Last4 = last4,
                    ExpMonth = expMonth,
                    ExpYear = expYear,
                    Funding = funding,
                    FingerPrint = fingerprint,
                    WalletType = walletType,
                    Archived = null,
                    BankName = bankName,
                    RoutingNumber = routingNumber,
                    AccountType = accountType,
                    AccountHolderType = accountHolder
                });
            }
            else
            {
                existing.Brand = brand;
                existing.Last4 = last4;
                existing.ExpMonth = expMonth;
                existing.ExpYear = expYear;
                existing.Funding = funding;
                existing.FingerPrint = fingerprint;
                existing.WalletType = walletType;
                existing.BankName = bankName;
                existing.RoutingNumber = routingNumber;
                existing.AccountType = accountType;
                existing.AccountHolderType = accountHolder;
                existing.Archived = null;
                _db.WalletPaymentMethods.Update(existing);
            }

            // Deduplicate by fingerprint
            if (!string.IsNullOrEmpty(fingerprint))
            {
                var dups = await _db.WalletPaymentMethods
                    .Where(x => x.WalletId == wallet.Id &&
                                x.FingerPrint == fingerprint &&
                                x.PaymentMethodId != pm.Id &&
                                x.Archived == null)
                    .ToListAsync();
                foreach (var d in dups) d.Archived = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync();
        }
    }
}

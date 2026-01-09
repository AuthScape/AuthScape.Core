using AuthScape.Models.PaymentGateway;
using AuthScape.Models.PaymentGateway.Stripe;
using AuthScape.Models.Users;
using AuthScape.Services;
using IDP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IDP.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class PaymentsModel : PageModel
    {
        private readonly IWalletResolver _resolver;
        private readonly DatabaseContext _db;
        private readonly AppSettings _cfg;
        private readonly ILogger<PaymentsModel> _logger;
        private readonly IAchVerificationEmailService _achEmailService;

        private readonly SetupIntentService _siService;
        private readonly CustomerService _customerService;
        private readonly PaymentMethodService _pmService;

        public PaymentsModel(
            IWalletResolver resolver,
            DatabaseContext db,
            IOptions<AppSettings> cfg,
            ILogger<PaymentsModel> logger,
            IAchVerificationEmailService achEmailService)
        {
            _resolver = resolver;
            _db = db;
            _cfg = cfg.Value;
            _logger = logger;
            _achEmailService = achEmailService;

            _siService = new SetupIntentService();
            _customerService = new CustomerService();
            _pmService = new PaymentMethodService();
        }

        // Data for the page
        public List<WalletPaymentMethod> PaymentMethods { get; private set; } = new();
        public decimal WalletBalance { get; private set; }
        public Guid ActiveWalletId { get; private set; }
        public string StripePublicKey => _cfg.Stripe.PublishableKey;
        public string ActiveTab { get; private set; } = "overview";
        public bool HasStripeConnectAccount { get; private set; }
        public StripeConnectAccount? ConnectAccount { get; private set; }
        public int ActiveSubscriptionCount { get; private set; }
        public int PaymentMethodCount { get; private set; }

        // GET /Payments
        public async Task<IActionResult> OnGetAsync(
            [FromQuery] string? tab = null,
            [FromQuery(Name = "setup_intent")] string? setupIntentId = null,
            [FromQuery(Name = "setup_intent_client_secret")] string? setupIntentClientSecret = null,
            [FromQuery(Name = "redirect_status")] string? redirectStatus = null,
            [FromQuery] string? success = null,
            [FromQuery] string? error = null)
        {
            try
            {
                var wallet = await _resolver.GetActiveAsync(HttpContext);
                await _resolver.EnsureStripeCustomerAsync(wallet);
                ActiveWalletId = wallet.Id;

                // Set active tab from query param
                if (!string.IsNullOrEmpty(tab))
                {
                    ActiveTab = tab.ToLowerInvariant() switch
                    {
                        "subscriptions" => "subscriptions",
                        "payment-methods" => "payment-methods",
                        "billing-history" => "billing-history",
                        "quick-pay" => "quick-pay",
                        "stripe-connect" => "stripe-connect",
                        _ => "overview"
                    };
                }

                // Handle SetupIntent redirect result if present (for ACH verification)
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
                        return RedirectToPage("Payments", new { tab = "payment-methods", error = "wallet_mismatch" });

                    if (!string.Equals(si.CustomerId, wallet.PaymentCustomerId, StringComparison.Ordinal))
                        return RedirectToPage("Payments", new { tab = "payment-methods", error = "customer_mismatch" });

                    switch (si.Status)
                    {
                        case "succeeded":
                            await SavePaymentMethodAsync(wallet, si);
                            return RedirectToPage("Payments", new { tab = "payment-methods", success = "setup_complete" });

                        case "requires_action":
                            if (si.NextAction?.Type == "verify_with_microdeposits")
                            {
                                ViewData["AchVerifyPending"] = true;
                                ViewData["AchHostedUrl"] = si.NextAction.VerifyWithMicrodeposits?.HostedVerificationUrl;
                                ViewData["AchVerifyHint"] = si.NextAction.VerifyWithMicrodeposits?.MicrodepositType;
                                ActiveTab = "payment-methods";

                                // Send ACH verification email
                                var currentUserId = GetUserId();
                                if (currentUserId.HasValue)
                                {
                                    string bankLast4 = si.PaymentMethod?.UsBankAccount?.Last4;
                                    string bankName = si.PaymentMethod?.UsBankAccount?.BankName;
                                    var microdeposits = si.NextAction.VerifyWithMicrodeposits;
                                    string arrivalDate = microdeposits?.ArrivalDate != default
                                        ? microdeposits.ArrivalDate.ToString("MMMM d, yyyy")
                                        : "1-2 business days";
                                    string hostedUrl = microdeposits?.HostedVerificationUrl;
                                    string verificationType = microdeposits?.MicrodepositType;

                                    await _achEmailService.SendInitialVerificationEmailAsync(
                                        currentUserId.Value,
                                        bankLast4,
                                        bankName,
                                        arrivalDate,
                                        hostedUrl,
                                        verificationType);
                                }

                                break;
                            }
                            return RedirectToPage("Payments", new { tab = "payment-methods", error = si.Status });

                        case "processing":
                            ViewData["Processing"] = true;
                            ActiveTab = "payment-methods";
                            break;

                        case "requires_payment_method":
                            return RedirectToPage("Payments", new { tab = "payment-methods", error = "requires_payment_method" });

                        case "canceled":
                            return RedirectToPage("Payments", new { tab = "payment-methods", error = "canceled" });

                        default:
                            return RedirectToPage("Payments", new { tab = "payment-methods", error = "unexpected_status" });
                    }
                }

                // Load payment methods
                PaymentMethods = await _db.WalletPaymentMethods
                    .Where(pm => pm.WalletId == wallet.Id && pm.Archived == null)
                    .OrderBy(pm => pm.Brand)
                    .ToListAsync();
                PaymentMethodCount = PaymentMethods.Count;

                // Calculate wallet balance
                WalletBalance = await _db.WalletTransactions
                    .Where(t => t.WalletId == wallet.Id)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                // Get active subscription count
                ActiveSubscriptionCount = await _db.Subscriptions
                    .Where(s => s.WalletId == wallet.Id &&
                                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
                    .CountAsync();

                // Check for Stripe Connect account
                var userId = GetUserId();
                if (userId.HasValue)
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                    if (user?.CompanyId != null)
                    {
                        ConnectAccount = await _db.StripeConnectAccounts
                            .FirstOrDefaultAsync(c => c.CompanyId == user.CompanyId);
                        HasStripeConnectAccount = ConnectAccount != null;
                    }
                }

                // Pass success/error messages to view
                if (!string.IsNullOrEmpty(success))
                    ViewData["SuccessMessage"] = success;
                if (!string.IsNullOrEmpty(error))
                    ViewData["ErrorMessage"] = error;

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payments page");
                return Page();
            }
        }

        #region Payment Method Handlers

        // Create SetupIntent for Payment Element (ACH + Card)
        // POST /Payments?handler=SetupIntent
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSetupIntentAsync(Guid? walletId)
        {
            try
            {
                var wallet = await _resolver.GetActiveAsync(HttpContext);
                await _resolver.EnsureStripeCustomerAsync(wallet);

                var opts = new SetupIntentCreateOptions
                {
                    Customer = wallet.PaymentCustomerId,
                    AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                    },
                    Metadata = new Dictionary<string, string> { ["wallet_id"] = wallet.Id.ToString() },
                    PaymentMethodOptions = new SetupIntentPaymentMethodOptionsOptions
                    {
                        UsBankAccount = new SetupIntentPaymentMethodOptionsUsBankAccountOptions
                        {
                            VerificationMethod = "automatic"
                        }
                    }
                };

                var si = await _siService.CreateAsync(opts);
                return new JsonResult(new { clientSecret = si.ClientSecret });
            }
            catch (StripeException ex)
            {
                return BadRequest(new { error = ex.StripeError?.Message ?? ex.Message, code = ex.StripeError?.Code });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Status probe for micro-deposits banner
        // POST /Payments?handler=AchStatus
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
                await SavePaymentMethodAsync(wallet, si);
                return new JsonResult(new { found = true, needsVerification = false, status = si.Status });
            }

            if (string.Equals(si.Status, "requires_action", StringComparison.Ordinal) &&
                si.NextAction?.Type == "verify_with_microdeposits")
            {
                var hosted = si.NextAction.VerifyWithMicrodeposits?.HostedVerificationUrl;
                var method = si.NextAction.VerifyWithMicrodeposits?.MicrodepositType;
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

        // Verify ACH micro-deposits server-side
        // POST /Payments?handler=VerifyAch
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
                verifyOpts.Amounts = new List<long?> { amount1.Value, amount2.Value };
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

        // POST /Payments?handler=MakeDefault
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostMakeDefaultAsync(string pmId, Guid? walletId)
        {
            if (string.IsNullOrWhiteSpace(pmId)) return BadRequest();

            var wallet = await _resolver.GetActiveAsync(HttpContext);

            try
            {
                await _customerService.UpdateAsync(wallet.PaymentCustomerId,
                    new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = pmId }
                    });
            }
            catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
            {
                var local = await _db.WalletPaymentMethods.FirstOrDefaultAsync(p => p.PaymentMethodId == pmId);
                if (local != null)
                {
                    local.Archived = DateTimeOffset.UtcNow;
                    _db.Update(local);
                    await _db.SaveChangesAsync();
                }
                return BadRequest(new { error = "This payment method no longer exists. It has been removed." });
            }

            return new OkResult();
        }

        // POST /Payments?handler=DeleteMethod
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteMethodAsync(string pmId, Guid? walletId)
        {
            if (string.IsNullOrWhiteSpace(pmId)) return BadRequest();

            try
            {
                await _pmService.DetachAsync(pmId);
            }
            catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
            {
                // Payment method doesn't exist in Stripe - that's fine
            }

            var local = await _db.WalletPaymentMethods.FirstOrDefaultAsync(p => p.PaymentMethodId == pmId);
            if (local != null)
            {
                local.Archived = DateTimeOffset.UtcNow;
                _db.Update(local);
                await _db.SaveChangesAsync();
            }

            return new OkResult();
        }

        #endregion

        #region Wallet/Funds Handlers

        // Checkout top-ups (card + ACH)
        // POST /Payments?handler=AddFunds
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAddFundsAsync(long amountCents, Guid? walletId)
        {
            if (amountCents < 100) return BadRequest("Minimum $1");

            var wallet = await _resolver.GetActiveAsync(HttpContext);
            await _resolver.EnsureStripeCustomerAsync(wallet);

            var successUrl = Url.Page("/Account/Manage/Payments", null, new { tab = "overview", success = "funds_added" }, Request.Scheme);
            var cancelUrl = Url.Page("/Account/Manage/Payments", null, new { tab = "overview", error = "checkout_canceled" }, Request.Scheme);

            var session = await new SessionService().CreateAsync(new SessionCreateOptions
            {
                Mode = "payment",
                Customer = wallet.PaymentCustomerId,
                PaymentMethodTypes = new List<string> { "card", "us_bank_account" },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
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

        #endregion

        #region Subscription Handlers

        // Remove a stale subscription that no longer exists in Stripe
        // POST /Payments?handler=RemoveStale
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRemoveStaleAsync(Guid subscriptionId)
        {
            try
            {
                var userId = GetUserId();
                if (!userId.HasValue)
                {
                    return new JsonResult(new { success = false, error = "User not authenticated" });
                }

                var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId.Value);
                if (wallet == null)
                {
                    return new JsonResult(new { success = false, error = "Wallet not found" });
                }

                var subscription = await _db.Subscriptions
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
                        _logger.LogInformation("Subscription {StripeId} not found in Stripe, removing locally", subscription.StripeSubscriptionId);
                    }
                }

                subscription.Status = SubscriptionStatus.Canceled;
                subscription.CanceledAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stale subscription {SubscriptionId}", subscriptionId);
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        #endregion

        #region Helpers

        private long? GetUserId()
        {
            var idValue =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                User.FindFirst("oid")?.Value;

            if (!string.IsNullOrWhiteSpace(idValue) && long.TryParse(idValue, out var userId) && userId > 0)
                return userId;

            return null;
        }

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

            // Make default
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

        #endregion
    }
}

using AuthScape.Services;
using AuthScape.StripePayment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Services.Context;
using Services.Database;
using Stripe;

namespace AuthScape.StripePayment.Controller.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
    public class SubscriptionController : ControllerBase
    {
        readonly DatabaseContext context;
        readonly AppSettings appSettings;
        readonly IUserManagementService userManagementService;
        readonly IStripeSubscriptionService subscriptionService;
        readonly IStripeInvoiceService invoiceService;

        public SubscriptionController(
            IOptions<AppSettings> appSettings,
            DatabaseContext context,
            IUserManagementService userManagementService,
            IStripeSubscriptionService subscriptionService,
            IStripeInvoiceService invoiceService)
        {
            this.appSettings = appSettings.Value;
            this.context = context;
            this.userManagementService = userManagementService;
            this.subscriptionService = subscriptionService;
            this.invoiceService = invoiceService;

            if (this.appSettings.Stripe != null && this.appSettings.Stripe.SecretKey != null)
            {
                StripeConfiguration.ApiKey = this.appSettings.Stripe.SecretKey;
            }
        }

        /// <summary>
        /// Get all available subscription plans from Stripe
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailablePlans()
        {
            try
            {
                var plans = await subscriptionService.ListAvailablePlansAsync();
                return Ok(new { success = true, plans });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new subscription for the current user/company
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest req)
        {
            try
            {
                // Get user ID from claims (works with both cookie and bearer token auth)
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                // Get the appropriate wallet
                Guid walletId;
                if (req.PaymentMethodType == "company")
                {
                    // Try to get company ID from claims
                    var companyIdValue = User.FindFirst("companyId")?.Value;
                    if (long.TryParse(companyIdValue, out var companyId))
                    {
                        var wallet = await context.Wallets
                            .FirstOrDefaultAsync(w => w.CompanyId == companyId);
                        if (wallet == null)
                            return BadRequest(new { success = false, error = "No company wallet found" });
                        walletId = wallet.Id;
                    }
                    else
                    {
                        return BadRequest(new { success = false, error = "Company wallet requested but no company ID found" });
                    }
                }
                else
                {
                    var wallet = await context.Wallets
                        .FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet == null)
                        return BadRequest(new { success = false, error = "No user wallet found" });
                    walletId = wallet.Id;
                }

                var options = new CreateSubscriptionOptions
                {
                    TrialPeriodDays = req.TrialPeriodDays,
                    PromoCode = req.PromoCode,
                    PaymentMethodId = req.PaymentMethodId,
                    Metadata = req.Metadata
                };

                var result = await subscriptionService.CreateSubscriptionAsync(walletId, req.PriceId, options);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscriptionId = result.Subscription.Id,
                    stripeSubscriptionId = result.Subscription.StripeSubscriptionId,
                    status = result.Subscription.Status.ToString(),
                    clientSecret = result.ClientSecret,
                    currentPeriodEnd = result.Subscription.CurrentPeriodEnd,
                    trialEnd = result.Subscription.TrialEnd
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get all subscriptions for the current user/company
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMySubscriptions([FromQuery] bool includeInactive = false, [FromQuery] string walletType = "user")
        {
            try
            {
                // Get user ID from claims (works with both cookie and bearer token auth)
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                Guid walletId;
                if (walletType == "company")
                {
                    // Try to get company ID from claims
                    var companyIdValue = User.FindFirst("companyId")?.Value;
                    if (long.TryParse(companyIdValue, out var companyId))
                    {
                        var wallet = await context.Wallets
                            .FirstOrDefaultAsync(w => w.CompanyId == companyId);
                        if (wallet == null)
                            return Ok(new { success = true, subscriptions = new List<object>() });
                        walletId = wallet.Id;
                    }
                    else
                    {
                        return BadRequest(new { success = false, error = "Company wallet requested but no company ID found" });
                    }
                }
                else
                {
                    var wallet = await context.Wallets
                        .FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet == null)
                        return Ok(new { success = true, subscriptions = new List<object>() });
                    walletId = wallet.Id;
                }

                var results = await subscriptionService.ListSubscriptionsAsync(walletId, includeInactive);

                var subscriptions = results.Select(r => new
                {
                    id = r.Subscription.Id,
                    stripeSubscriptionId = r.Subscription.StripeSubscriptionId,
                    status = r.Subscription.Status.ToString(),
                    productName = r.Subscription.ProductName,
                    amount = r.Subscription.Amount,
                    currency = r.Subscription.Currency,
                    interval = r.Subscription.Interval,
                    intervalCount = r.Subscription.IntervalCount,
                    currentPeriodStart = r.Subscription.CurrentPeriodStart,
                    currentPeriodEnd = r.Subscription.CurrentPeriodEnd,
                    cancelAtPeriodEnd = r.Subscription.CancelAtPeriodEnd,
                    canceledAt = r.Subscription.CanceledAt,
                    trialStart = r.Subscription.TrialStart,
                    trialEnd = r.Subscription.TrialEnd,
                    createdAt = r.Subscription.CreatedAt
                }).ToList();

                return Ok(new { success = true, subscriptions });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific subscription by ID
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSubscription([FromQuery] Guid subscriptionId)
        {
            try
            {
                var result = await subscriptionService.GetSubscriptionAsync(subscriptionId);

                if (!result.Success)
                    return NotFound(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        stripeSubscriptionId = result.Subscription.StripeSubscriptionId,
                        status = result.Subscription.Status.ToString(),
                        productName = result.Subscription.ProductName,
                        amount = result.Subscription.Amount,
                        currency = result.Subscription.Currency,
                        interval = result.Subscription.Interval,
                        intervalCount = result.Subscription.IntervalCount,
                        currentPeriodStart = result.Subscription.CurrentPeriodStart,
                        currentPeriodEnd = result.Subscription.CurrentPeriodEnd,
                        cancelAtPeriodEnd = result.Subscription.CancelAtPeriodEnd,
                        trialEnd = result.Subscription.TrialEnd,
                        items = result.Subscription.Items.Select(i => new
                        {
                            id = i.Id,
                            productId = i.ProductId,
                            description = i.Description,
                            quantity = i.Quantity,
                            amount = i.Amount,
                            currency = i.Currency
                        })
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a subscription
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest req)
        {
            try
            {
                var result = await subscriptionService.CancelSubscriptionAsync(req.SubscriptionId, req.CancelImmediately);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        status = result.Subscription.Status.ToString(),
                        cancelAtPeriodEnd = result.Subscription.CancelAtPeriodEnd,
                        canceledAt = result.Subscription.CanceledAt,
                        endedAt = result.Subscription.EndedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Resume a canceled subscription
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResumeSubscription([FromBody] ResumeSubscriptionRequest req)
        {
            try
            {
                var result = await subscriptionService.ResumeSubscriptionAsync(req.SubscriptionId);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        status = result.Subscription.Status.ToString(),
                        cancelAtPeriodEnd = result.Subscription.CancelAtPeriodEnd
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Pause a subscription
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PauseSubscription([FromBody] PauseSubscriptionRequest req)
        {
            try
            {
                var result = await subscriptionService.PauseSubscriptionAsync(req.SubscriptionId);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        status = result.Subscription.Status.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Upgrade or downgrade a subscription
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ChangeSubscriptionPlan([FromBody] ChangeSubscriptionPlanRequest req)
        {
            try
            {
                var result = await subscriptionService.UpgradeDowngradeSubscriptionAsync(
                    req.SubscriptionId,
                    req.NewPriceId,
                    req.Prorate);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        status = result.Subscription.Status.ToString(),
                        productName = result.Subscription.ProductName,
                        amount = result.Subscription.Amount,
                        interval = result.Subscription.Interval
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Preview the cost of changing a subscription plan
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PreviewPlanChange([FromBody] PreviewPlanChangeRequest req)
        {
            try
            {
                var result = await subscriptionService.PreviewSubscriptionChangeAsync(req.SubscriptionId, req.NewPriceId);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    preview = new
                    {
                        amountDue = result.AmountDue,
                        currency = result.Currency,
                        prorationDate = result.ProrationDate,
                        nextBillingDate = result.NextBillingDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Apply a promo code to a subscription
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ApplyPromoCode([FromBody] ApplyPromoCodeRequest req)
        {
            try
            {
                var result = await subscriptionService.ApplyPromoCodeAsync(req.SubscriptionId, req.PromoCode);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        couponId = result.Subscription.CouponId,
                        discountAmount = result.Subscription.DiscountAmount,
                        amount = result.Subscription.Amount
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Update the payment method for a subscription
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdatePaymentMethod([FromBody] UpdatePaymentMethodRequest req)
        {
            try
            {
                var result = await subscriptionService.UpdatePaymentMethodAsync(req.SubscriptionId, req.PaymentMethodId);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = result.Subscription.Id,
                        defaultPaymentMethodId = result.Subscription.DefaultPaymentMethodId
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Sync subscription data from Stripe
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SyncSubscription([FromBody] SyncSubscriptionRequest req)
        {
            try
            {
                var subscription = await subscriptionService.SyncSubscriptionFromStripeAsync(req.StripeSubscriptionId);

                return Ok(new
                {
                    success = true,
                    subscription = new
                    {
                        id = subscription.Id,
                        stripeSubscriptionId = subscription.StripeSubscriptionId,
                        status = subscription.Status.ToString(),
                        lastSyncedAt = subscription.LastSyncedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    #region Request Models

    public class CreateSubscriptionRequest
    {
        public string PriceId { get; set; }
        public string PaymentMethodType { get; set; } = "user"; // "user" or "company"
        public string PaymentMethodId { get; set; }
        public int? TrialPeriodDays { get; set; }
        public string? PromoCode { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class CancelSubscriptionRequest
    {
        public Guid SubscriptionId { get; set; }
        public bool CancelImmediately { get; set; } = false;
    }

    public class ResumeSubscriptionRequest
    {
        public Guid SubscriptionId { get; set; }
    }

    public class PauseSubscriptionRequest
    {
        public Guid SubscriptionId { get; set; }
    }

    public class ChangeSubscriptionPlanRequest
    {
        public Guid SubscriptionId { get; set; }
        public string NewPriceId { get; set; }
        public bool Prorate { get; set; } = true;
    }

    public class PreviewPlanChangeRequest
    {
        public Guid SubscriptionId { get; set; }
        public string NewPriceId { get; set; }
    }

    public class ApplyPromoCodeRequest
    {
        public Guid SubscriptionId { get; set; }
        public string PromoCode { get; set; }
    }

    public class UpdatePaymentMethodRequest
    {
        public Guid SubscriptionId { get; set; }
        public string PaymentMethodId { get; set; }
    }

    public class SyncSubscriptionRequest
    {
        public string StripeSubscriptionId { get; set; }
    }

    #endregion
}

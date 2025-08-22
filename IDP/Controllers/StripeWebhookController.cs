using AuthScape.Models.PaymentGateway;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthScape.IDP.Controllers
{
    [ApiController]
    [Route("stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly DatabaseContext _db;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly string _webhookSecret;

        public StripeWebhookController(DatabaseContext db, IOptions<AppSettings> cfg, ILogger<StripeWebhookController> logger)
        {
            _db = db;
            _logger = logger;
            _webhookSecret = cfg.Value.Stripe.SigningSecret; // set in config
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _webhookSecret);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid webhook signature");
                return BadRequest();
            }

            switch (stripeEvent.Type)
            {
                case EventTypes.PaymentIntentSucceeded:
                    {
                        var pi = stripeEvent.Data.Object as PaymentIntent;
                        var walletId = pi?.Metadata?["wallet_id"];
                        var intent = pi?.Metadata?["intent"];
                        if (intent == "wallet_topup" && Guid.TryParse(walletId, out var wid))
                        {
                            // Idempotency: ensure we haven’t already recorded this PI
                            var exists = await _db.WalletTransactions.AnyAsync(t => t.ExternalRef == pi.Id);
                            if (!exists)
                            {
                                // In payment_intent.succeeded
                                long cents = (pi.AmountReceived > 0 ? pi.AmountReceived : pi.Amount);
                                decimal amount = cents / 100m; // USD has 2 decimals

                                // Amount is in cents
                                _db.WalletTransactions.Add(new WalletTransaction
                                {
                                    WalletId = wid,
                                    Amount = amount,
                                    CreatedUtc = DateTime.UtcNow,
                                    ExternalRef = pi.Id,
                                    Description = "ACH/Card top-up via Stripe Checkout"
                                });
                                await _db.SaveChangesAsync();
                            }
                        }
                        break;
                    }
                case EventTypes.PaymentIntentProcessing:
                    // Optional: mark a “pending top-up” if you want to show it in UI.
                    break;

                case EventTypes.PaymentIntentPaymentFailed:
                    // Optional: notify user
                    break;

                case EventTypes.SetupIntentSucceeded:
                    // Optional: You already handle saving in the return URL flow,
                    // but webhooks are a nice double-check for resiliency.
                    break;
            }

            return Ok();
        }
    }

}
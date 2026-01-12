using AuthScape.Models.PaymentGateway;
using AuthScape.Models.PaymentGateway.Stripe;
using AuthScape.Services;
using AuthScape.StripePayment.Models;
using AuthScape.StripePayment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Services.Context;
using Services.Database;

namespace AuthScape.StripePayment.Controller.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        readonly IStripePayService stripePayService;
        readonly IUserManagementService userManagementService;
        readonly DatabaseContext context;
        readonly IAchVerificationEmailService achEmailService;
        public PaymentController(IStripePayService stripePayService, IUserManagementService userManagementService, DatabaseContext context, IAchVerificationEmailService achEmailService)
        {
            this.stripePayService = stripePayService;
            this.context = context;
            this.userManagementService = userManagementService;
            this.achEmailService = achEmailService;
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> ConnectCustomer(PaymentRequest paymentRequest)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims and database
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                // Load user data from database to get CompanyId, LocationId, etc.
                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return Unauthorized(new { success = false, error = "User not found" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser
                {
                    Id = userId,
                    CompanyId = user.CompanyId,
                    LocationId = user.LocationId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                };
            }

            return Ok(await stripePayService.ConnectCustomer(signedInUser, paymentRequest));
        }

        [HttpPost]
        public async Task<IActionResult> ConnectCustomerNoAuth(PaymentRequest paymentRequest)
        {
            return Ok(await stripePayService.ConnectCustomer(null, paymentRequest));
        }

        [HttpGet]
        public async Task<IActionResult> GetStripePublicKey()
        {
            var publicKey = await stripePayService.GetStripePublicKey();
            if (string.IsNullOrEmpty(publicKey))
            {
                return NotFound(new { success = false, message = "Stripe public key not configured. Please add a setting with Name='StripePublicKey' in the Settings table." });
            }
            return Ok(new { publicKey = publicKey });
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> SetupStripeConnect(string returnBaseUrl)
        {
            var signedInUser = await userManagementService.GetSignedInUser();
            return Ok(await stripePayService.SetupStripeConnect(signedInUser, returnBaseUrl));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> GetStripeConnectStatus()
        {
            var signedInUser = await userManagementService.GetSignedInUser();
            var status = await stripePayService.GetStripeConnectStatus(signedInUser);
            if (status == null)
            {
                return NotFound(new { success = false, message = "No Stripe Connect account found" });
            }
            return Ok(status);
        }

        [HttpPost]
        public async Task<IActionResult> GeneratePaymentLink(PaymentLinkParam param)
        {
            return Ok(await stripePayService.GeneratePaymentLink(param));
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> Charge(ChargeParam param)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
            }

            return Ok(await stripePayService.Charge(signedInUser, param));
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> ChargeWithExistingPayment(ChargeWithExistingPaymentParam param)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
            }

            await stripePayService.ChargeWithExistingPayment(signedInUser, param.InvoiceId, param.WalletPaymentMethodId, param.Amount);
            return Ok();
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> GetPaymentMethods(PaymentMethodType paymentMethodType)
        {
            // Try to get signed in user from the service first
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser
                {
                    Id = userId
                };

                // Try to get company ID if requesting company payment methods
                if (paymentMethodType == PaymentMethodType.Company)
                {
                    var companyIdValue = User.FindFirst("companyId")?.Value;
                    if (long.TryParse(companyIdValue, out var companyId))
                    {
                        signedInUser.CompanyId = companyId;
                    }
                }

                // Try to get location ID if requesting location payment methods
                if (paymentMethodType == PaymentMethodType.Location)
                {
                    var locationIdValue = User.FindFirst("locationId")?.Value;
                    if (long.TryParse(locationIdValue, out var locationId))
                    {
                        signedInUser.LocationId = locationId;
                    }
                }
            }

            var paymentMethods = await stripePayService.GetPaymentMethods(signedInUser, paymentMethodType);

            // Get wallet to determine default payment method
            Wallet wallet = null;
            if (paymentMethodType == PaymentMethodType.Company && signedInUser.CompanyId.HasValue)
            {
                wallet = await context.Wallets.FirstOrDefaultAsync(w => w.CompanyId == signedInUser.CompanyId);
            }
            else if (paymentMethodType == PaymentMethodType.User)
            {
                wallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserId == signedInUser.Id);
            }
            else if (paymentMethodType == PaymentMethodType.Location && signedInUser.LocationId.HasValue)
            {
                wallet = await context.Wallets.FirstOrDefaultAsync(w => w.LocationId == signedInUser.LocationId);
            }

            var defaultPaymentMethodId = wallet?.DefaultPaymentMethodId;

            // Map to response with isDefault flag
            var result = paymentMethods?.Select(pm => new
            {
                id = pm.Id,
                walletId = pm.WalletId,
                expMonth = pm.ExpMonth,
                expYear = pm.ExpYear,
                last4 = pm.Last4,
                brand = pm.Brand,
                bankName = pm.BankName,
                accountType = pm.AccountType,
                accountHolderType = pm.AccountHolderType,
                routingNumber = pm.RoutingNumber,
                walletType = pm.WalletType,
                paymentMethodId = pm.PaymentMethodId,
                isDefault = pm.Id == defaultPaymentMethodId
            }).ToList();

            return Ok(result);
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> CheckIfACHNeedValidation()
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
            }

            return Ok(await stripePayService.ACHNeedValidation(signedInUser));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> GetUnverifiedACHPaymentMethods()
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
            }

            // Get all wallets for this user (User, Company, Location)
            var wallets = await context.Wallets
                .Include(w => w.WalletPaymentMethods)
                .Where(w => w.UserId == signedInUser.Id ||
                           w.CompanyId == signedInUser.CompanyId ||
                           w.LocationId == signedInUser.LocationId)
                .ToListAsync();

            if (!wallets.Any())
            {
                return Ok(new { needsVerification = false, unverifiedMethods = new List<object>() });
            }

            // Get all ACH payment methods from all wallets
            var achMethods = wallets
                .SelectMany(w => w.WalletPaymentMethods ?? new List<WalletPaymentMethod>())
                .Where(pm => pm.WalletType == WalletType.us_bank_account && pm.Archived == null)
                .ToList();

            if (!achMethods.Any())
            {
                return Ok(new { needsVerification = false, unverifiedMethods = new List<object>() });
            }

            // Get all customer IDs from wallets
            var customerIds = wallets
                .Where(w => !string.IsNullOrEmpty(w.PaymentCustomerId))
                .Select(w => w.PaymentCustomerId)
                .Distinct()
                .ToList();

            if (!customerIds.Any())
            {
                return Ok(new { needsVerification = false, unverifiedMethods = new List<object>() });
            }

            var unverifiedMethods = new List<object>();
            var allSetupIntents = new List<Stripe.SetupIntent>();

            try
            {
                var siService = new Stripe.SetupIntentService();

                // Get SetupIntents for all customer IDs
                foreach (var customerId in customerIds)
                {
                    var setupIntents = await siService.ListAsync(new Stripe.SetupIntentListOptions
                    {
                        Customer = customerId,
                        Limit = 100
                    });
                    allSetupIntents.AddRange(setupIntents.Data);
                }

                // Filter to SetupIntents that require micro-deposit verification
                var unverifiedSetupIntents = allSetupIntents
                    .Where(si => si.Status == "requires_action" &&
                                 si.NextAction?.Type == "verify_with_microdeposits")
                    .OrderByDescending(si => si.Created) // Most recent first
                    .ToList();

                // Debug logging
                Console.WriteLine($"Found {unverifiedSetupIntents.Count} unverified SetupIntents");
                foreach (var si in unverifiedSetupIntents)
                {
                    Console.WriteLine($"  SetupIntent {si.Id}: PaymentMethodId={si.PaymentMethodId}, Created={si.Created}, MicrodepositType={si.NextAction?.VerifyWithMicrodeposits?.MicrodepositType}");
                }
                Console.WriteLine($"Looking for ACH methods with PaymentMethodIds:");
                foreach (var achMethod in achMethods)
                {
                    Console.WriteLine($"  ACH {achMethod.Id}: PaymentMethodId={achMethod.PaymentMethodId}, Last4={achMethod.Last4}");
                }

                // Match SetupIntents to ACH payment methods
                foreach (var achMethod in achMethods)
                {
                    Stripe.SetupIntent matchingSetupIntent = null;

                    // Priority 1: Match by stored SetupIntentId (most reliable)
                    if (!string.IsNullOrEmpty(achMethod.SetupIntentId))
                    {
                        matchingSetupIntent = unverifiedSetupIntents
                            .FirstOrDefault(si => si.Id == achMethod.SetupIntentId);
                        if (matchingSetupIntent != null)
                        {
                            Console.WriteLine($"  Matched by SetupIntentId: SetupIntent {matchingSetupIntent.Id} -> ACH {achMethod.Id}");
                        }
                        else
                        {
                            // SetupIntent exists but might not be in our filtered list - fetch it directly
                            try
                            {
                                var directSi = await siService.GetAsync(achMethod.SetupIntentId);
                                if (directSi.Status == "requires_action" &&
                                    directSi.NextAction?.Type == "verify_with_microdeposits")
                                {
                                    matchingSetupIntent = directSi;
                                    Console.WriteLine($"  Matched by direct SetupIntent fetch: {directSi.Id} -> ACH {achMethod.Id}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error fetching SetupIntent {achMethod.SetupIntentId}: {ex.Message}");
                            }
                        }
                    }

                    // Priority 2: Match by PaymentMethodId - query Stripe directly for SetupIntents with this PaymentMethod
                    if (matchingSetupIntent == null && !string.IsNullOrEmpty(achMethod.PaymentMethodId))
                    {
                        // First check in our pre-loaded list
                        matchingSetupIntent = unverifiedSetupIntents
                            .FirstOrDefault(si => si.PaymentMethodId == achMethod.PaymentMethodId);

                        if (matchingSetupIntent != null)
                        {
                            Console.WriteLine($"  Matched by PaymentMethodId in list: SetupIntent {matchingSetupIntent.Id} -> ACH {achMethod.Id}");
                        }
                        else
                        {
                            // Query Stripe directly for SetupIntents with this PaymentMethod
                            try
                            {
                                var pmSetupIntents = await siService.ListAsync(new Stripe.SetupIntentListOptions
                                {
                                    PaymentMethod = achMethod.PaymentMethodId,
                                    Limit = 10
                                });

                                var directMatch = pmSetupIntents.Data
                                    .FirstOrDefault(si => si.Status == "requires_action" &&
                                                          si.NextAction?.Type == "verify_with_microdeposits");

                                if (directMatch != null)
                                {
                                    matchingSetupIntent = directMatch;
                                    Console.WriteLine($"  Matched by direct PaymentMethod query: SetupIntent {directMatch.Id} -> ACH {achMethod.Id}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error querying SetupIntents for PaymentMethod {achMethod.PaymentMethodId}: {ex.Message}");
                            }
                        }
                    }

                    if (matchingSetupIntent != null)
                    {
                        var microdeposits = matchingSetupIntent.NextAction.VerifyWithMicrodeposits;
                        unverifiedMethods.Add(new
                        {
                            walletPaymentMethodId = achMethod.Id,
                            paymentMethodId = achMethod.PaymentMethodId,
                            setupIntentId = matchingSetupIntent.Id,
                            last4 = achMethod.Last4,
                            bankName = achMethod.BankName,
                            accountType = achMethod.AccountType,
                            clientSecret = matchingSetupIntent.ClientSecret,
                            hostedVerificationUrl = microdeposits?.HostedVerificationUrl,
                            microdepositType = microdeposits?.MicrodepositType
                        });
                    }
                    else
                    {
                        Console.WriteLine($"  No matching SetupIntent found for ACH {achMethod.Id} (PaymentMethodId={achMethod.PaymentMethodId}, SetupIntentId={achMethod.SetupIntentId})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking SetupIntents: {ex.Message}");
                return Ok(new
                {
                    needsVerification = false,
                    unverifiedMethods = new List<object>(),
                    error = ex.Message
                });
            }

            return Ok(new
            {
                needsVerification = unverifiedMethods.Any(),
                unverifiedMethods = unverifiedMethods
            });
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> AddPaymentMethod(SavePaymentMethod savePaymentMethod)
        {
            var signedInUser = await userManagementService.GetSignedInUser();

            // If null (cookie auth), construct from claims and database
            if (signedInUser == null)
            {
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                // Load user data from database
                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return Unauthorized(new { success = false, error = "User not found" });
                }

                signedInUser = new AuthScape.Models.Users.SignedInUser
                {
                    Id = userId,
                    CompanyId = user.CompanyId,
                    LocationId = user.LocationId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                };
            }

            var result = await stripePayService.AddPaymentMethod(signedInUser, savePaymentMethod.PaymentMethodType, savePaymentMethod.WalletId, savePaymentMethod.StripePaymentMethod, savePaymentMethod.StripeSetupIntentId);

            // Check if this is an ACH payment method requiring verification
            var pmService = new Stripe.PaymentMethodService();
            var paymentMethod = await pmService.GetAsync(savePaymentMethod.StripePaymentMethod);

            if (paymentMethod?.Type == "us_bank_account")
            {
                // Check SetupIntent status for this payment method
                var siService = new Stripe.SetupIntentService();
                var setupIntents = await siService.ListAsync(new Stripe.SetupIntentListOptions
                {
                    PaymentMethod = savePaymentMethod.StripePaymentMethod,
                    Limit = 1
                });

                var setupIntent = setupIntents.Data.FirstOrDefault();
                if (setupIntent?.Status == "requires_action" &&
                    setupIntent.NextAction?.Type == "verify_with_microdeposits")
                {
                    var microdeposits = setupIntent.NextAction.VerifyWithMicrodeposits;
                    string arrivalDateStr = microdeposits?.ArrivalDate != default
                        ? microdeposits.ArrivalDate.ToString("MMMM d, yyyy")
                        : "1-2 business days";

                    await achEmailService.SendInitialVerificationEmailAsync(
                        signedInUser.Id,
                        paymentMethod.UsBankAccount?.Last4,
                        paymentMethod.UsBankAccount?.BankName,
                        arrivalDateStr,
                        microdeposits?.HostedVerificationUrl,
                        microdeposits?.MicrodepositType);

                    return Ok(new
                    {
                        id = result,
                        requiresVerification = true,
                        hostedVerificationUrl = microdeposits?.HostedVerificationUrl,
                        clientSecret = setupIntent.ClientSecret,
                        microdepositType = microdeposits?.MicrodepositType
                    });
                }
            }

            return Ok(new { id = result, requiresVerification = false });
        }

        [HttpDelete]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> RemovePaymentMethod(Guid Id)
        {
            try
            {
                var signedInUser = await userManagementService.GetSignedInUser();

                // If null (cookie auth), construct from claims
                if (signedInUser == null)
                {
                    var idValue =
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                        User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                        User.FindFirst("sub")?.Value ??
                        User.FindFirst("oid")?.Value;

                    if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                    {
                        return Unauthorized(new { success = false, error = "User ID not found in claims" });
                    }

                    signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
                }

                await stripePayService.RemovePaymentMethod(signedInUser, Id);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Set the default payment method for a wallet
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> SetDefaultPaymentMethod([FromBody] SetDefaultPaymentMethodRequest request)
        {
            try
            {
                var signedInUser = await userManagementService.GetSignedInUser();

                // If null (cookie auth), construct from claims
                if (signedInUser == null)
                {
                    var idValue =
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                        User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                        User.FindFirst("sub")?.Value ??
                        User.FindFirst("oid")?.Value;

                    if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                    {
                        return Unauthorized(new { success = false, error = "User ID not found in claims" });
                    }

                    signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
                }

                // If WalletId is not provided, look it up from the payment method
                var walletId = request.WalletId;
                if (walletId == Guid.Empty)
                {
                    var paymentMethod = await context.WalletPaymentMethods
                        .FirstOrDefaultAsync(pm => pm.Id == request.PaymentMethodId);
                    if (paymentMethod == null)
                    {
                        return BadRequest(new { success = false, error = "Payment method not found" });
                    }
                    walletId = paymentMethod.WalletId;
                }

                var success = await stripePayService.SetDefaultPaymentMethod(
                    signedInUser,
                    walletId,
                    request.PaymentMethodId);

                if (!success)
                    return BadRequest(new { success = false, error = "Failed to set default payment method" });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get the default payment method for a wallet
        /// </summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> GetDefaultPaymentMethod([FromQuery] Guid walletId)
        {
            try
            {
                var signedInUser = await userManagementService.GetSignedInUser();

                // If null (cookie auth), construct from claims
                if (signedInUser == null)
                {
                    var idValue =
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                        User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                        User.FindFirst("sub")?.Value ??
                        User.FindFirst("oid")?.Value;

                    if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                    {
                        return Unauthorized(new { success = false, error = "User ID not found in claims" });
                    }

                    signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
                }

                var paymentMethod = await stripePayService.GetDefaultPaymentMethod(signedInUser, walletId);

                if (paymentMethod == null)
                    return NotFound(new { success = false, error = "No default payment method found" });

                return Ok(new
                {
                    success = true,
                    paymentMethod = new
                    {
                        id = paymentMethod.Id,
                        walletType = paymentMethod.WalletType.ToString(),
                        last4 = paymentMethod.Last4,
                        brand = paymentMethod.Brand,
                        expMonth = paymentMethod.ExpMonth,
                        expYear = paymentMethod.ExpYear,
                        bankName = paymentMethod.BankName,
                        accountType = paymentMethod.AccountType,
                        paymentMethodId = paymentMethod.PaymentMethodId
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Quick charge - one-time payment using an existing payment method
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> QuickCharge([FromBody] QuickChargeRequest request)
        {
            try
            {
                var signedInUser = await userManagementService.GetSignedInUser();

                // If null (cookie auth), construct from claims
                if (signedInUser == null)
                {
                    var idValue =
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                        User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                        User.FindFirst("sub")?.Value ??
                        User.FindFirst("oid")?.Value;

                    if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                    {
                        return Unauthorized(new { success = false, error = "User ID not found in claims" });
                    }

                    signedInUser = new AuthScape.Models.Users.SignedInUser { Id = userId };
                }

                if (request.AmountCents < 100)
                {
                    return BadRequest(new { success = false, error = "Minimum charge amount is $1.00" });
                }

                if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
                {
                    return BadRequest(new { success = false, error = "Payment method is required" });
                }

                // Parse the payment method ID - can be either a Guid (from DB) or a Stripe payment method ID string
                Guid walletPaymentMethodId;
                if (!Guid.TryParse(request.PaymentMethodId, out walletPaymentMethodId))
                {
                    return BadRequest(new { success = false, error = "Invalid payment method ID format" });
                }

                // Use the existing Charge method with the payment method
                var chargeParam = new ChargeParam
                {
                    PaymentMethodType = PaymentMethodType.User,
                    Amount = request.AmountCents / 100m, // Convert cents to dollars
                    WalletPaymentMethodId = walletPaymentMethodId
                };

                var result = await stripePayService.Charge(signedInUser, chargeParam);

                if (result == null || !result.Success)
                {
                    return StatusCode(500, new { success = false, error = result?.Reason ?? "Payment processing failed" });
                }

                // Return processing status for ACH payments that are still pending
                return Ok(new {
                    success = true,
                    paymentIntentId = result.StripePaymentIntentId,
                    status = result.Reason ?? "succeeded" // "processing" for ACH, null/succeeded for cards
                });
            }
            catch (Stripe.StripeException ex)
            {
                return BadRequest(new { success = false, error = ex.StripeError?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class PaidInvoiceParam
    {
        public long InvoiceId { get; set; }
        public string PaymentIntent { get; set; }
    }

    public class SavePaymentMethod
    {
        public Guid WalletId { get; set; }
        public PaymentMethodType PaymentMethodType { get; set; }
        public string StripePaymentMethod { get; set; }
        public string StripeSetupIntentId { get; set; } // For ACH verification matching
    }

    public class PaymentMethodSyncParam
    {
        public string payment_intent { get; set; }
        public long? invoiceId { get; set; }
    }

    public class ChargeWithExistingPaymentParam
    {
        public long InvoiceId { get; set; }
        public Guid WalletPaymentMethodId { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaymentMethodAttachParam
    {
        public string CustomerId { get; set; }
        public string PaymentMethodId { get; set; }
    }

    public class SetDefaultPaymentMethodRequest
    {
        public Guid WalletId { get; set; }
        public Guid PaymentMethodId { get; set; }
    }

    public class QuickChargeRequest
    {
        public long AmountCents { get; set; }
        public string PaymentMethodId { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }
}

using AuthScape.Models.PaymentGateway;
using AuthScape.Models.PaymentGateway.Stripe;
using AuthScape.Services;
using AuthScape.StripePayment.Models;
using AuthScape.StripePayment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.StripePayment.Controller.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        readonly IStripePayService stripePayService;
        readonly IUserManagementService userManagementService;
        public PaymentController(IStripePayService stripePayService, IUserManagementService userManagementService)
        {
            this.stripePayService = stripePayService;
            this.userManagementService = userManagementService;
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> ConnectCustomer(PaymentRequest paymentRequest)
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

            return Ok(await stripePayService.ConnectCustomer(signedInUser, paymentRequest));
        }

        [HttpPost]
        public async Task<IActionResult> ConnectCustomerNoAuth(PaymentRequest paymentRequest)
        {
            return Ok(await stripePayService.ConnectCustomer(null, paymentRequest));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> SetupStripeConnect(string returnBaseUrl)
        {
            var signedInUser = await userManagementService.GetSignedInUser();
            return Ok(await stripePayService.SetupStripeConnect(signedInUser, returnBaseUrl));
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
            return Ok(paymentMethods);
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

        [HttpPost]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> AddPaymentMethod(SavePaymentMethod savePaymentMethod)
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

            return Ok(await stripePayService.AddPaymentMethod(signedInUser, savePaymentMethod.PaymentMethodType, savePaymentMethod.WalletId, savePaymentMethod.StripePaymentMethod));
        }

        [HttpDelete]
        [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
        public async Task<IActionResult> RemovePaymentMethod(Guid Id)
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

            return Ok();
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

                var success = await stripePayService.SetDefaultPaymentMethod(
                    signedInUser,
                    request.WalletId,
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
}

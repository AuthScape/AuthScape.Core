using AuthScape.Models.Users;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Authentication;
using Newtonsoft.Json;
using Services.Context;
using Services.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDP.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseContext _databaseContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly IFido2 _fido2;
        private readonly SignInManager<AppUser> _signInManager;
        readonly AppSettings appSettings;

        public HomeController(
            DatabaseContext databaseContext,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IFido2 fido2,
            IOptions<AppSettings> appSettings)
        {
            _databaseContext = databaseContext;
            _userManager = userManager;
            _fido2 = fido2;
            _signInManager = signInManager;
            this.appSettings = appSettings.Value;
        }

        [HttpGet]
        public async Task<JsonResult> GetRegistrationOptions(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return Json(new { error = "User not found" });

            var existingCredentials = await _databaseContext.Fido2Credentials
                .Where(z => z.UserId == user.Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToListAsync();

            var options = _fido2.RequestNewCredential(
                new RequestNewCredentialParams
                {
                    User = new Fido2User
                    {
                        Id = Encoding.UTF8.GetBytes(user.Id.ToString()),
                        Name = user.UserName,
                        DisplayName = user.UserName
                    },
                    ExcludeCredentials = existingCredentials,
                    AuthenticatorSelection = new AuthenticatorSelection
                    {
                        AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                        UserVerification = UserVerificationRequirement.Required,
                        ResidentKey = Fido2NetLib.Objects.ResidentKeyRequirement.Required // Updated from RequireResidentKey
                    },
                    AttestationPreference = AttestationConveyancePreference.Direct
                });

            // Store the FULL options properly
            var optionsJson = JsonConvert.SerializeObject(options);
            HttpContext.Session.SetString("fido2Challenge",
                WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(optionsJson)));

            return Json(options);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterCredential([FromBody] AuthenticatorAttestationRawResponse response)
        {
            try
            {
                // 1. Retrieve and decode the stored challenge PROPERLY
                var base64Challenge = HttpContext.Session.GetString("fido2Challenge");
                if (string.IsNullOrEmpty(base64Challenge))
                {
                    return BadRequest(new { error = "Challenge is missing or invalid" });
                }

                // 2. Decode and deserialize the original options
                var decodedBytes = WebEncoders.Base64UrlDecode(base64Challenge);
                var optionsJson = Encoding.UTF8.GetString(decodedBytes);
                var originalChallenge = JsonConvert.DeserializeObject<CredentialCreateOptions>(optionsJson);

                // 3. Validate the credential properly - updated to use new API
                var success = await _fido2.MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = response,
                        OriginalOptions = originalChallenge,
                        IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) =>
                        {
                            // Load credentials into memory to avoid SequenceEqual translation issue
                            var credentials = await _databaseContext.Fido2Credentials.ToListAsync(cancellationToken);
                            var existingCredential = credentials.Any(c => c.CredentialId.SequenceEqual(args.CredentialId));
                            return !existingCredential;
                        }
                    });

                // 4. Handle the result - updated for new return type
                // Since new API throws exceptions on failure, if we get here it was successful
                var user = await _userManager.FindByIdAsync(Encoding.UTF8.GetString(originalChallenge.User.Id));
                if (user == null)
                {
                    return BadRequest(new { error = "User not found" });
                }

                await _databaseContext.Fido2Credentials.AddAsync(new Fido2Credential
                {
                    CredentialId = success.Id, // Updated property names
                    PublicKey = success.PublicKey,
                    UserHandle = success.User.Id,
                    SignatureCounter = success.SignCount, // Updated property name
                    CredType = success.Type, // Updated property name  
                    RegDate = DateTime.Now,
                    AaGuid = success.AaGuid != Guid.Empty ? success.AaGuid.ToString() : null,
                    UserId = user.Id,
                    DeviceName = $"Device {DateTime.Now:yyyy-MM-dd}"
                });

                await _databaseContext.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                // The new API throws exceptions instead of returning error status
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetAssertionOptions(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return Json(new { error = "User not found" });

            var allowedCredentials = await _databaseContext.Fido2Credentials
                .Where(c => c.UserId == user.Id)
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToListAsync();

            //var registeredCredentials = user.Credentials
            //    .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            //    .ToList();

            var options = _fido2.GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials = new List<PublicKeyCredentialDescriptor>(),
                    UserVerification = UserVerificationRequirement.Required
                });

            // Convert byte[] challenge to base64 URL-safe string
            HttpContext.Session.SetString("assertionChallenge",
                WebEncoders.Base64UrlEncode(options.Challenge));

            return Json(options);
        }

        [HttpGet]
        public async Task<JsonResult> GetRegisteredDevices()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { error = "User not found" });

            var devices = await _databaseContext.Fido2Credentials
                .Where(c => c.UserId == user.Id)
                .Select(c => new
                {
                    credentialId = Convert.ToBase64String(c.CredentialId),
                    registrationDate = c.RegDate,
                    deviceName = c.DeviceName // You'll need to add this property to your Fido2Credential model
                })
                .ToListAsync();

            return Json(devices);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteCredential(string credentialId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found");

            var credentialBytes = Convert.FromBase64String(credentialId);
            var credential = await _databaseContext.Fido2Credentials
                .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(credentialBytes) && c.UserId == user.Id);

            if (credential == null) return NotFound("Credential not found");

            _databaseContext.Fido2Credentials.Remove(credential);
            await _databaseContext.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyAssertion([FromBody] AuthenticatorAssertionRawResponse response)
        {
            // 1. Retrieve and decode challenge
            var base64Challenge = HttpContext.Session.GetString("assertionChallenge");
            var challenge = WebEncoders.Base64UrlDecode(base64Challenge);

            // 2. Get stored credential
            var credentialIdBytes = WebEncoders.Base64UrlDecode(response.Id);
            var credential = await _databaseContext.Fido2Credentials
                .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(credentialIdBytes));

            if (credential == null) return BadRequest("Credential not found");

            // 3. Create assertion options
            var assertionOptions = new AssertionOptions
            {
                Challenge = challenge,
                RpId = new Uri(appSettings.IDPUrl).Host,
                AllowCredentials = new List<PublicKeyCredentialDescriptor>
        {
            new PublicKeyCredentialDescriptor(credential.CredentialId)
        },
                UserVerification = UserVerificationRequirement.Required
            };

            try
            {
                // 4. Verify assertion
                var result = await _fido2.MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = response,
                        OriginalOptions = assertionOptions,
                        StoredPublicKey = credential.PublicKey,
                        StoredSignatureCounter = (uint)credential.SignatureCounter,
                        IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                            credential.UserHandle.SequenceEqual(args.UserHandle)
                        )
                    });

                // If we get here, the assertion was successful
                // Update signature counter - note it's SignCount, not Counter
                credential.SignatureCounter = result.SignCount;
                await _databaseContext.SaveChangesAsync();

                // Get user from UserHandle
                var userId = Encoding.UTF8.GetString(credential.UserHandle);
                var user = await _userManager.FindByIdAsync(userId.ToString());
                await _signInManager.SignInAsync(user, isPersistent: false);

                var returnUrl = appSettings.LoginRedirectUrl + "/login";
                return Ok(returnUrl);
            }
            catch (Exception ex)
            {
                // Assertion failed
                return BadRequest($"Assertion verification failed: {ex.Message}");
            }
        }

    }
}

using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Services.Context;
using System.Linq;
using Newtonsoft.Json;
using AuthScape.Models.Users;
using Fido2NetLib.Objects;
using Fido2NetLib;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Models.Authentication;
using StrongGrid.Resources;
using Services.Database;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;

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
                new Fido2User
                {
                    Id = Encoding.UTF8.GetBytes(user.Id.ToString()),
                    Name = user.UserName,
                    DisplayName = user.UserName
                },
                existingCredentials,
                new AuthenticatorSelection
                {
                    AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                    UserVerification = UserVerificationRequirement.Required,
                    RequireResidentKey = true,
                },
                AttestationConveyancePreference.Direct
            );

            // Store the FULL options properly
            var optionsJson = JsonConvert.SerializeObject(options);
            HttpContext.Session.SetString("fido2Challenge",
                Base64Url.Encode(Encoding.UTF8.GetBytes(optionsJson)));

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
                var decodedBytes = Base64Url.Decode(base64Challenge);
                var optionsJson = Encoding.UTF8.GetString(decodedBytes);
                var originalChallenge = JsonConvert.DeserializeObject<CredentialCreateOptions>(optionsJson);

                // 3. Validate the credential properly
                var success = await _fido2.MakeNewCredentialAsync(
                    response,
                    originalChallenge,
                    async (args, cancellationToken) =>
                    {
                        var existingCredential = await _databaseContext.Fido2Credentials
                            .AnyAsync(c => c.CredentialId.SequenceEqual(args.CredentialId), cancellationToken);
                        return !existingCredential;
                    });

                // 4. Handle the result
                if (success.Status == "ok")
                {
                    var user = await _userManager.FindByIdAsync(Encoding.UTF8.GetString(originalChallenge.User.Id));
                    if (user == null)
                    {
                        return BadRequest(new { error = "User not found" });
                    }

                    await _databaseContext.Fido2Credentials.AddAsync(new Fido2Credential
                    {
                        CredentialId = success.Result.CredentialId,
                        PublicKey = success.Result.PublicKey,
                        UserHandle = success.Result.User.Id,
                        SignatureCounter = (uint)success.Result.Counter,
                        CredType = success.Result.CredType,
                        RegDate = DateTime.Now,
                        AaGuid = success.Result.Aaguid != Guid.Empty
                            ? success.Result.Aaguid.ToString()
                            : null, // Handle empty GUIDs
                        UserId = user.Id,
                        DeviceName = $"Device {DateTime.Now:yyyy-MM-dd}"
                    });

                    await _databaseContext.SaveChangesAsync();

                    //await _userManager.UpdateAsync(user);
                    return Ok(new { success = true });
                }

                return BadRequest(new { error = success.ErrorMessage });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Internal error: {ex.Message}" });
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
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required);

            // Convert byte[] challenge to base64 URL-safe string
            HttpContext.Session.SetString("assertionChallenge",
                                        Base64Url.Encode(options.Challenge));

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
            var challenge = Base64Url.Decode(base64Challenge);

            // 2. Get stored credential
            var credential = await _databaseContext.Fido2Credentials
                .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(response.Id));

            if (credential == null) return BadRequest("Credential not found");

            // 3. Create assertion options
            var assertionOptions = new AssertionOptions
            {
                Challenge = challenge,
                RpId = "localhost", // Match your Fido2 configuration
                AllowCredentials = new List<PublicKeyCredentialDescriptor>
        {
            new PublicKeyCredentialDescriptor(credential.CredentialId)
        },
                UserVerification = UserVerificationRequirement.Required
            };

            // 4. Verify assertion with correct parameter order
            var result = await _fido2.MakeAssertionAsync(
                response,
                assertionOptions,
                credential.PublicKey,
                (uint)credential.SignatureCounter,
                (args, _) => Task.FromResult(
                    credential.UserHandle.SequenceEqual(args.UserHandle)
                ));

            if (result.Status == "ok")
            {
                // Update signature counter
                credential.SignatureCounter = result.Counter;
                await _databaseContext.SaveChangesAsync();

                // Get user from UserHandle
                var userId = Encoding.UTF8.GetString(credential.UserHandle);  // ← Use credential.UserHandle
                var user = await _userManager.FindByIdAsync(userId.ToString());

                await _signInManager.SignInAsync(user, isPersistent: false);

                var returnUrl = appSettings.LoginRedirectUrl + "/login";
                return Ok(returnUrl);
            }

            return BadRequest(result.ErrorMessage);
        }

    }
}

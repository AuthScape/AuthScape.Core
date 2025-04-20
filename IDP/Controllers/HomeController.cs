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

namespace IDP.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseContext _databaseContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly IFido2 _fido2;
        private readonly SignInManager<AppUser> _signInManager;

        public HomeController(
            DatabaseContext databaseContext,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IFido2 fido2)
        {
            _databaseContext = databaseContext;
            _userManager = userManager;
            _fido2 = fido2;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<JsonResult> GetRegistrationOptions(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return Json(new { error = "User not found" });

            var existingCredentials = user.Credentials
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            // Direct options creation with required parameters
            var options = _fido2.RequestNewCredential(
                user: new Fido2User
                {
                    Id = BitConverter.GetBytes(user.Id),
                    Name = user.UserName,
                    DisplayName = user.UserName
                },
                excludeCredentials: existingCredentials,
                authenticatorSelection: new AuthenticatorSelection
                {
                    AuthenticatorAttachment = AuthenticatorAttachment.Platform, // Force Windows Hello
                    UserVerification = UserVerificationRequirement.Required, // Require PIN
                    RequireResidentKey = true // Passwordless
                },
                AttestationConveyancePreference.None
            );

            HttpContext.Session.SetString("fido2Challenge", Base64Url.Encode(options.Challenge));
            return Json(options);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterCredential([FromBody] AuthenticatorAttestationRawResponse response)
        {
            var challenge = HttpContext.Session.GetString("fido2Challenge");
            if (string.IsNullOrEmpty(challenge))
            {
                return BadRequest("Challenge is missing or invalid.");
            }

            // Parse the challenge from JSON
            var originalChallenge = JsonConvert.DeserializeObject<CredentialCreateOptions>(challenge);

            var success = await _fido2.MakeNewCredentialAsync(
                response,
                originalChallenge,
                async (args, cancellationToken) =>
                {
                    // Verify credential uniqueness logic here
                    var existingCredential = await _databaseContext.Fido2Credentials
                        .AnyAsync(c => c.CredentialId.SequenceEqual(args.CredentialId), cancellationToken);

                    return !existingCredential;
                }
            );

            if (success.Status == "ok")
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return BadRequest("User not found.");
                }

                user.Credentials.Add(new Fido2Credential
                {
                    CredentialId = success.Result.CredentialId,
                    PublicKey = success.Result.PublicKey,
                    UserHandle = success.Result.User.Id,
                    SignatureCounter = (uint)success.Result.Counter,
                    CredType = success.Result.CredType,
                    RegDate = DateTime.Now,
                    AaGuid = success.Result.Aaguid.ToString()
                });

                await _userManager.UpdateAsync(user);
                return Ok();
            }

            return BadRequest(success.ErrorMessage);
        }

        [HttpGet]
        public async Task<JsonResult> GetAssertionOptions(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return Json(new { error = "User not found" });

            var allowedCredentials = user.Credentials
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            var options = _fido2.GetAssertionOptions(
                allowedCredentials,
                UserVerificationRequirement.Required);

            // Convert byte[] challenge to base64 URL-safe string
            HttpContext.Session.SetString("assertionChallenge",
                                        Base64Url.Encode(options.Challenge));

            return Json(options);
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
                RpId = "your-domain.com", // Match your Fido2 configuration
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
                var userId = BitConverter.ToInt64(credential.UserHandle, 0);  // ← Use credential.UserHandle
                var user = await _userManager.FindByIdAsync(userId.ToString());

                await _signInManager.SignInAsync(user, isPersistent: false);
                return Ok();
            }

            return BadRequest(result.ErrorMessage);
        }

    }
}

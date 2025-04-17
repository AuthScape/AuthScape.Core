using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System;
using System.Threading.Tasks;
using StrongGrid.Resources;
using Services.Context;
using System.Linq;

namespace IDP.Controllers
{
    public class HomeController : Controller
    {
        readonly DatabaseContext databaseContext;
        public HomeController(DatabaseContext databaseContext)
        {
            databaseContext = this.databaseContext;
        }


        private static string ToBase64UrlString(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }


        [HttpPost]
        public ActionResult StartRegistration(string username)
        {
            //var user = databaseContext.Users.FirstOrDefault(u => u.UserName == username);
            //if (user == null)
            //{
            //    user = new User
            //    {
            //        UserId = Guid.NewGuid().ToByteArray(),
            //        UserName = username,
            //        DisplayName = username
            //    };
            //    db.Users.Add(user);
            //    db.SaveChanges();
            //}

            var options = new
            {
                challenge = ToBase64UrlString(RandomNumberGenerator.GetBytes(32)),
                rp = new { name = "Your App Name" },
                user = new
                {
                    id = Guid.NewGuid().ToByteArray(),
                    name = "brandonzuech@gmail.com",
                    displayName = "brandonzuech@gmail.com"
                },
                pubKeyCredParams = new[]
                {
                    new { type = "public-key", alg = -7 },  // ES256
                    new { type = "public-key", alg = -257 } // RS256
                },
                authenticatorSelection = new { userVerification = "preferred" },
                timeout = 60000,
                attestation = "direct"
            };

            return Json(options);
        }












        //[HttpPost]
        //public ActionResult StartAuthentication(string username)
        //{
        //    var user = databaseContext.Users.FirstOrDefault(u => u.UserName == username);
        //    //var credentials = databaseContext.Credentials.Where(c => c.UserId == user.UserId).ToList();

        //    var options = new
        //    {
        //        challenge = ToBase64UrlString(RandomNumberGenerator.GetBytes(32)),
        //        allowCredentials = credentials.Select(c => new
        //        {
        //            type = "public-key",
        //            id = ToBase64UrlString(c.CredentialId),
        //            transports = new[] { "internal" }
        //        }),
        //        userVerification = "preferred",
        //        timeout = 60000
        //    };

        //    return Json(options);
        //}

        //[HttpPost]
        //public async Task<ActionResult> FinishAuthentication(AuthenticatorAssertionRawResponse assertionResponse)
        //{
        //    var credential = db.Credentials
        //        .FirstOrDefault(c => c.CredentialId == Base64UrlDecode(assertionResponse.id));

        //    if (VerifySignature(
        //        assertionResponse.response.authenticatorData,
        //        assertionResponse.response.clientDataJSON,
        //        assertionResponse.response.signature,
        //        credential.PublicKey))
        //    {
        //        credential.SignCount++;
        //        db.SaveChanges();
        //        return Ok();
        //    }

        //    return BadRequest("Authentication failed");
        //}
    }
}

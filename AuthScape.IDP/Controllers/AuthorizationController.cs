using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using IDP.Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthScape.Models.Users;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Models;
using Services.Context;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Models.Users;

namespace IDP.Controllers
{
    public class AuthorizationController : Controller
    {
        private readonly IOpenIddictScopeManager _scopeManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly DatabaseContext databaseContext;

        public AuthorizationController(
            IOpenIddictScopeManager scopeManager,
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            DatabaseContext databaseContext)
        {
            _scopeManager = scopeManager;
            _signInManager = signInManager;
            _userManager = userManager;
            this.databaseContext = databaseContext;
        }

        //[Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
        [HttpGet("~/connect/authorize")]
        public async Task<IActionResult> Authorize() 
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The request cannot be retrieved.");

            if (!User.Identity.IsAuthenticated)
            {
                // If the client application request promptless authentication,
                // return an error indicating that the user is not logged in.
                if (request.HasPromptValue(OpenIddictConstants.PromptValues.None))
                {
                    var properties = new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The user is not logged in."
                    });

                    // Ask OpenIddict to return a login_required error to the client application.
                    return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

                return Challenge();
            }

            // Retrieve the profile of the logged in user.
            var user = await _userManager.GetUserAsync(User) ??
                throw new InvalidOperationException("The user details cannot be retrieved.");

            // Create a new ClaimsPrincipal containing the claims that
            // will be used to create an id_token, a token or a code.
            var principal = await _signInManager.CreateUserPrincipalAsync(user);



            var subjectClaim = principal.Claims.Where(z => z.Type == "sub").FirstOrDefault();
            var userId = Convert.ToInt64(subjectClaim.Value);

            var currentUser = await databaseContext.Users
                .AsNoTracking()
                .Include(c => c.Company)
                .Include(l => l.Location)
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();

            if (currentUser != null)
            {
                // Add company name claim
                if (currentUser.Company != null)
                {
                    principal.Identities.First().AddClaim(new Claim("companyName", currentUser.Company.Title));
                }

                if (currentUser.CompanyId != null)
                {
                    principal.Identities.First().AddClaim(new Claim("companyId", currentUser.CompanyId.ToString()));
                }

                // Add company name claim
                if (currentUser.Location != null)
                {
                    principal.Identities.First().AddClaim(new Claim("locationName", currentUser.Location.Title));
                }

                if (currentUser.LocationId != null)
                {
                    principal.Identities.First().AddClaim(new Claim("locationId", currentUser.LocationId.ToString()));
                }

                if (currentUser.FirstName != null)
                {
                    principal.Identities.First().AddClaim(new Claim("firstName", currentUser.FirstName));
                }

                if (currentUser.LastName != null)
                {
                    principal.Identities.First().AddClaim(new Claim("lastName", currentUser.LastName));
                }


                // permissions
                var permissions = await databaseContext.UserClaims
                    .AsNoTracking()
                    .Where(s => s.UserId == currentUser.Id && s.ClaimType == "permissions")
                    .FirstOrDefaultAsync();

                if (permissions != null && !String.IsNullOrWhiteSpace(permissions.ClaimValue))
                {
                    var userPermissions = new List<Permission>();
                    var permissionList = permissions.ClaimValue.Split(",");

                    foreach (var permissionItem in permissionList)
                    {
                        var permission = await databaseContext.Permissions.AsNoTracking().Where(p => p.Id == Guid.Parse(permissionItem)).FirstOrDefaultAsync();
                        if (permission != null)
                        {
                            userPermissions.Add(permission);
                        }
                    }

                    principal.Identities.First().AddClaim(new Claim("userPermissions", JsonConvert.SerializeObject(userPermissions)));
                }


                // roles
                var userRoles = databaseContext.UserRoles
                    .Where(u => u.UserId == userId);

                var roles = new List<QueryRole>();
                foreach (var role in userRoles)
                {
                    var queryRole = await databaseContext.Roles.AsNoTracking().Where(u => u.Id == role.RoleId).Select(s => new QueryRole() { Id = s.Id, Name = s.Name }).FirstOrDefaultAsync();
                    if (queryRole != null)
                    {
                        roles.Add(queryRole);
                    }
                }

                //var names = roles.Select(s => s.Name);

                principal.Identities.First().AddClaim(new Claim("usersRoles", JsonConvert.SerializeObject(roles)));
            }



            // Set the list of scopes granted to the client application.
            var scopes = request.GetScopes();


            principal.SetScopes(request.GetScopes());
            principal.SetResources(await _scopeManager.ListResourcesAsync(scopes).ToListAsync());

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        [HttpGet("~/connect/logout")]
        public async Task<IActionResult> Logout(string redirect)
        {
            // Ask ASP.NET Core Identity to delete the local and external cookies created
            // when the user agent is redirected from the external identity provider
            // after a successful authentication flow (e.g Google or Facebook).
            await _signInManager.SignOutAsync();

            // Returning a SignOutResult will ask OpenIddict to redirect the user agent
            // to the post_logout_redirect_uri specified by the client application.
            //return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            return Redirect(redirect);
        }


        [HttpPost("~/connect/token"), Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // use for server to server communication
            if (request.IsClientCredentialsGrantType())
            {
                // Note: the client credentials are automatically validated by OpenIddict:
                // if client_id or client_secret are invalid, this action won't be invoked.

                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // Subject (sub) is a required field, we use the client id as the subject identifier here.
                identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId ?? throw new InvalidOperationException());


                var user = await databaseContext.Users.Where(u => u.Id == Convert.ToInt64(request.ClientId)).FirstOrDefaultAsync();




                identity.AddClaim(OpenIddictConstants.Claims.GivenName, user.FirstName);
                identity.AddClaim(OpenIddictConstants.Claims.FamilyName, user.LastName);
                identity.AddClaim(OpenIddictConstants.Claims.Username, user.UserName);





                // Add some claim, don't forget to add destination otherwise it won't be added to the access token.
                identity.AddClaim("some-claim", "some-value", OpenIddictConstants.Destinations.AccessToken);

                var claimsPrincipal = new ClaimsPrincipal(identity);

                claimsPrincipal.SetScopes(request.GetScopes());

                // Returning a SignInResult will ask OpenIddict to issue the appropriate access/ identity tokens.
                return SignIn(claimsPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            
            // use for react and applications
            else if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                // Retrieve the claims principal stored in the authorization code/device code/refresh token.
                var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;

                // Retrieve the user profile corresponding to the authorization code/refresh token.
                // Note: if you want to automatically invalidate the authorization code/refresh token
                // when the user password/roles change, use the following line instead:
                // var user = _signInManager.ValidateSecurityStampAsync(info.Principal);
                var user = await _userManager.GetUserAsync(principal);
                if (user == null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                        }));
                }

                // Ensure the user is still allowed to sign in.
                if (!await _signInManager.CanSignInAsync(user))
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                        }));
                }

                foreach (var claim in principal.Claims)
                {
                    claim.SetDestinations(GetDestinations(claim, principal));
                }

                // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else
            {
                throw new InvalidOperationException("The specified grant type is not supported.");
            }

            throw new InvalidOperationException("The specified grant type is not supported.");
        }




        //[HttpPost("~/connect/token")]
        //public async Task<IActionResult> Exchange()
        //{
        //    var request = HttpContext.GetOpenIddictServerRequest() ??
        //                  throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        //    ClaimsPrincipal claimsPrincipal;

        //    //if (request.IsClientCredentialsGrantType())
        //    //{
        //    //    // Note: the client credentials are automatically validated by OpenIddict:
        //    //    // if client_id or client_secret are invalid, this action won't be invoked.

        //    //    var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        //    //    // Subject (sub) is a required field, we use the client id as the subject identifier here.
        //    //    identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId ?? throw new InvalidOperationException());

        //    //    // Add some claim, don't forget to add destination otherwise it won't be added to the access token.
        //    //    identity.AddClaim("some-claim", "some-value", OpenIddictConstants.Destinations.AccessToken);

        //    //    claimsPrincipal = new ClaimsPrincipal(identity);

        //    //    claimsPrincipal.SetScopes(request.GetScopes());
        //    //}

        //    if (request.IsAuthorizationCodeGrantType())
        //    {
        //        // Retrieve the claims principal stored in the authorization code
        //        claimsPrincipal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
        //    }

        //    //else if (request.IsRefreshTokenGrantType())
        //    //{
        //    //    // Retrieve the claims principal stored in the refresh token.
        //    //    claimsPrincipal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
        //    //}

        //    else
        //    {
        //        throw new InvalidOperationException("The specified grant type is not supported.");
        //    }

        //    // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
        //    return SignIn(claimsPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        //}








        private IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
        {
            // Note: by default, claims are NOT automatically included in the access and identity tokens.
            // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
            // whether they should be included in access tokens, in identity tokens or in both.

            switch (claim.Type)
            {
                case Claims.Name:
                    yield return Destinations.AccessToken;

                    if (principal.HasScope(Scopes.Profile))
                        yield return Destinations.IdentityToken;

                    yield break;

                case Claims.Email:
                    yield return Destinations.AccessToken;

                    if (principal.HasScope(Scopes.Email))
                        yield return Destinations.IdentityToken;

                    yield break;

                case Claims.Role:
                    yield return Destinations.AccessToken;

                    if (principal.HasScope(Scopes.Roles))
                        yield return Destinations.IdentityToken;

                    yield break;

                // Never include the security stamp in the access and identity tokens, as it's a secret value.
                case "AspNet.Identity.SecurityStamp": yield break;

                default:
                    yield return Destinations.AccessToken;
                    yield break;
            }
        }
    }

}

﻿using AuthScape.Models.Users;
using CoreBackpack.Time;
using IDP.ViewModels.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Models.Users;
using Newtonsoft.Json;
using Services.Context;
using System.Security.Claims;

namespace AuthScape.Services
{
    public interface IUserManagementService
    {
        Task<(IdentityResult?, string? redirectUri)> OnSignup(string? timezone, string returnUrl, RegisterViewModel model, SignInManager<AppUser> _signInManager, UserManager<AppUser> _userManager);
        Task<SignedInUser> GetSignedInUser();
        Task<Dictionary<string, string>> OnShowSignup();
    }

    public class UserManagementService : IUserManagementService
    {
        readonly IHttpContextAccessor httpContextAccessor;
        readonly DatabaseContext databaseContext;
        public UserManagementService(IHttpContextAccessor httpContextAccessor, DatabaseContext databaseContext)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.databaseContext = databaseContext;
        }

        public async Task<SignedInUser> GetSignedInUser()
        {
            var identity = httpContextAccessor.HttpContext.User.Identity as ClaimsIdentity;
            if (identity != null && identity.IsAuthenticated)
            {
                var sub = identity.Claims.Where(c => c.Type == "sub").FirstOrDefault();
                var companyId = identity.Claims.Where(c => c.Type == "companyId").FirstOrDefault();
                var companyName = identity.Claims.Where(c => c.Type == "companyName").FirstOrDefault();
                var locationName = identity.Claims.Where(c => c.Type == "locationName").FirstOrDefault();
                var locationId = identity.Claims.Where(c => c.Type == "locationId").FirstOrDefault();
                var username = identity.Claims.Where(c => c.Type == "username").FirstOrDefault();
                var firstName = identity.Claims.Where(c => c.Type == "firstName").FirstOrDefault();
                var lastName = identity.Claims.Where(c => c.Type == "lastName").FirstOrDefault();
                var permissions = identity.Claims.Where(c => c.Type == "userPermissions").FirstOrDefault();
                var roles = identity.Claims.Where(c => c.Type == "usersRoles").FirstOrDefault();

                if (sub != null && username != null)
                {
                    var signedInUser = new SignedInUser();

                    signedInUser.Id = Convert.ToInt64(sub.Value);
                    signedInUser.Email = username.Value;

                    if (companyId != null)
                    {
                        signedInUser.CompanyId = Convert.ToInt64(companyId.Value);
                    }

                    if (locationId != null)
                    {
                        signedInUser.LocationId = Convert.ToInt64(locationId.Value);
                    }

                    if (companyName != null)
                    {
                        signedInUser.CompanyName = companyName.Value;
                    }

                    if (locationName != null)
                    {
                        signedInUser.LocationName = locationName.Value;
                    }

                    if (firstName != null)
                    {
                        signedInUser.FirstName = firstName.Value;
                    }

                    if (lastName != null)
                    {
                        signedInUser.LastName = lastName.Value;
                    }

                    if (permissions != null)
                    {
                        signedInUser.Permissions = JsonConvert.DeserializeObject<List<Permission>>(permissions.Value);
                    }

                    if (roles != null)
                    {
                        signedInUser.Roles = JsonConvert.DeserializeObject<List<QueryRole>>(roles.Value);
                    }

                    return signedInUser;



                    //var userRoles = databaseContext.UserRoles
                    //    .Where(u => u.UserId == userId);

                    //var roles = new List<QueryRole>();
                    //foreach (var role in userRoles)
                    //{
                    //    var queryRole = await databaseContext.Roles.AsNoTracking().Where(u => u.Id == role.RoleId).Select(s => new QueryRole() { Id = s.Id, Name = s.Name }).FirstOrDefaultAsync();
                    //    if (queryRole != null)
                    //    {
                    //        roles.Add(queryRole);
                    //    }
                    //}

                    //var permissions = new List<string>();

                    //var userClaims = await databaseContext.UserClaims.AsNoTracking().Where(c => c.UserId == userId && c.ClaimType == "permissions").FirstOrDefaultAsync();
                    //if (userClaims != null && !String.IsNullOrWhiteSpace(userClaims.ClaimValue))
                    //{
                    //    var permissionIds = userClaims.ClaimValue.Split(",");
                    //    foreach (var item in permissionIds)
                    //    {
                    //        var _permissions = await databaseContext.Permissions.Where(p => p.Id == Guid.Parse(item)).AsNoTracking().FirstOrDefaultAsync();
                    //        if (_permissions != null)
                    //        {
                    //            permissions.Add(_permissions.Name);
                    //        }
                    //    }
                    //}

                    //// need to link here
                    //var usr = await databaseContext.Users
                    //    .Include(u => u.Company)
                    //    .Include(u => u.Location)
                    //    .AsNoTracking()
                    //    .Where(u => u.Id == userId)
                    //    .Select(u => new SignedInUser()
                    //    {
                    //        Id = u.Id,
                    //        FirstName = u.FirstName,
                    //        LastName = u.LastName,
                    //        Email = u.Email,
                    //        locale = u.locale,
                    //        Roles = roles,
                    //        CompanyId = u.CompanyId,
                    //        LocationId = u.LocationId,
                    //        CompanyName = u.Company != null ? u.Company.Title : null,
                    //        LocationName = u.Location != null ? u.Location.Title : null,
                    //        Permissions = permissions
                    //    })
                    //    .FirstOrDefaultAsync();


                    //if (usr != null)
                    //{
                    //    return usr;
                    //}
                }
            }

            return null;
        }

        public async Task<(IdentityResult?, string? redirectUri)> OnSignup(string? timezone, string returnUrl, RegisterViewModel model, SignInManager<AppUser> _signInManager, UserManager<AppUser> _userManager)
        {


            // create the user
            var user = new AppUser { UserName = model.Email, Email = model.Email, FirstName = model.FirstName, LastName = model.LastName, Created = SystemTime.Now, IsActive = true, LastLoggedIn = SystemTime.Now, locale = timezone };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Create the company (this won't work because the company should be created after the user is created in case the password fails)
                var newCompany = new Company()
                {
                    Title = model.Company,
                    Description = "",
                };
                databaseContext.Companies.Add(newCompany);
                await databaseContext.SaveChangesAsync();

                var userSignedUp = await databaseContext.Users.Where(u => u.Id == user.Id).FirstOrDefaultAsync();
                if (userSignedUp != null)
                {
                    userSignedUp.CompanyId = newCompany.Id;
                    await databaseContext.SaveChangesAsync();
                }

                var signInResult = await _signInManager.PasswordSignInAsync(model.Email, model.Password, true, lockoutOnFailure: false);
                if (signInResult.Succeeded)
                {
                    var uri = new Uri(returnUrl);
                    string host = uri.Host;
                    string scheme = uri.Scheme;
                    int port = uri.Port;

                    var resultUri = uri.Scheme + "://" + uri.Host + ":" + port + "/signin-oidc?signupPass=true";

                    return new(result, resultUri);

                }

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=532713
                // Send an email with this link
                //var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                //var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Context.Request.Scheme);
                //await _emailSender.SendEmailAsync(model.Email, "Confirm your account",
                //    "Please confirm your account by clicking this link: <a href=\"" + callbackUrl + "\">link</a>");
                //await _signInManager.SignInAsync(user, isPersistent: false);
                //return Redirect(returnUrl);
            }
            return new(result, null);
            //AddErrors(result);
        }

        public async Task<Dictionary<string, string>> OnShowSignup()
        {
            var param = new Dictionary<string, string>();

            param.Add("Manufactures", "weee");

            return param;
        }
    }
}
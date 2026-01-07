using AuthScape.Models.Invite;
using AuthScape.Models.Users;
using AuthScape.Services;
using AuthScape.Services.Invite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Invite;
using Services;
using Services.Context;
using Services.Database;

namespace AuthScape.IDP.Controllers
{
    public class InviteController : Controller
    {
        readonly UserManager<AppUser> _userManager;
        readonly DatabaseContext _applicationDbContext;
        readonly IHttpContextAccessor _httpContextAccessor;
        readonly IMailService mailService;
        readonly AppSettings appSettings;
        readonly IUserManagementService userManagementService;
        readonly IInviteService inviteService;
        readonly IInviteSettingsService inviteSettingsService;

        public InviteController(
            UserManager<AppUser> userManager,
            DatabaseContext _applicationDbContext,
            IHttpContextAccessor _httpContextAccessor,
            IMailService mailService,
            IInviteService inviteService,
            IOptions<AppSettings> appSettings,
            IUserManagementService userManagementService,
            IInviteSettingsService inviteSettingsService)
        {
            _userManager = userManager;
            this._applicationDbContext = _applicationDbContext;
            this._httpContextAccessor = _httpContextAccessor;
            this.mailService = mailService;
            this.appSettings = appSettings.Value;
            this.userManagementService = userManagementService;
            this.inviteService = inviteService;
            this.inviteSettingsService = inviteSettingsService;
        }

        /// <summary>
        /// Invite post logic
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> InviteUsers([FromBody] InviteRequested requested)
        {
            if (requested.Requests == null || !requested.Requests.Any())
            {
                return BadRequest("Invalid Request");
            }

            // Get current user (inviter)
            var currentUser = await userManagementService.GetSignedInUser();
            if (currentUser == null)
            {
                return Unauthorized("User must be authenticated to send invites");
            }

            // Validate each request against invite settings
            var allErrors = new List<string>();
            foreach (var request in requested.Requests)
            {
                var validation = await inviteSettingsService.ValidateInviteRequestAsync(
                    request,
                    currentUser.Id,
                    currentUser.CompanyId);

                if (!validation.IsValid)
                {
                    allErrors.AddRange(validation.Errors.Select(e => $"{request.Email}: {e}"));
                }
            }

            if (allErrors.Any())
            {
                return BadRequest(new { errors = allErrors, success = false });
            }

            var newUsers = await inviteService.OnInviteUser(_applicationDbContext, requested.Requests, currentUser.Id);

            var errors = "";
            foreach (var user in newUsers)
            {
                var dbUser = await _applicationDbContext.Users.Where(u => u.Id == user.Id).FirstOrDefaultAsync();
                if (dbUser != null)
                {
                    try
                    {
                        var passwordResetToken = await _userManager.GeneratePasswordResetTokenAsync(dbUser);
                        if (passwordResetToken != null)
                        {
                            var resetToken = System.Net.WebUtility.UrlEncode(passwordResetToken);
                            if (_httpContextAccessor.HttpContext != null)
                            {
                                string host = _httpContextAccessor.HttpContext.Request.Scheme + "://" +
                                    _httpContextAccessor.HttpContext.Request.Host.Value;

                                await mailService.InviteUser(
                                        dbUser,
                                        host + "/Invite/Signup?id=" + dbUser.Id + "&WebsiteUrlRedirect=" + requested.Host + "&resetToken=" + resetToken
                                );
                            }
                        }
                    }
                    catch (Exception exp)
                    {
                        return BadRequest(exp.Message + " - " + (exp.InnerException != null ? exp.InnerException.Message : ""));
                    }
                }
            }

            return Json(new
            {
                errors = errors,
                success = String.IsNullOrWhiteSpace(errors) ? true : false
            });
        }


        /// <summary>
        /// Load the invite/signup page
        /// </summary>
        /// <param name="inviteViewModel"></param>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Signup(InviteViewModel inviteViewModel)
        {
            var dbUser = await _applicationDbContext.Users.Where(u => u.Id == inviteViewModel.Id).FirstOrDefaultAsync();
            if (dbUser != null)
            {
                inviteViewModel.FirstName = dbUser.FirstName;
                inviteViewModel.LastName = dbUser.LastName;
                inviteViewModel.Email = dbUser.UserName;

                var location = await _applicationDbContext.Locations.Where(l => l.Id == dbUser.LocationId).FirstOrDefaultAsync();
                if (location != null)
                {
                    inviteViewModel.LocationName = location.Title;
                }

                var company = await _applicationDbContext.Companies.Where(l => l.Id == dbUser.CompanyId).FirstOrDefaultAsync();
                if (company != null)
                {
                    inviteViewModel.CompanyName = company.Title;
                }

                if (!String.IsNullOrWhiteSpace(dbUser.PasswordHash) && !String.IsNullOrWhiteSpace(dbUser.SecurityStamp))
                {
                    inviteViewModel.AlreadyWithinSystem = true;
                }

                // pull the private label minified code
                var dnsRecord = await _applicationDbContext.DnsRecords.Where(d => d.Domain.ToLower() == inviteViewModel.WebsiteUrlRedirect.ToLower()).FirstOrDefaultAsync();
                if (dnsRecord != null)
                {
                    inviteViewModel.MinifiedCSS = dnsRecord.MinifiedCSSFile;
                }
                else
                {
                    inviteViewModel.MinifiedCSS = null;
                }

                await inviteService.OnInvitePageLoading(inviteViewModel, dbUser);

                return View(inviteViewModel);
            }

            return Redirect(appSettings.InviteSignupRedirectUrl);
        }

        /// <summary>
        /// On creating an account
        /// </summary>
        /// <param name="inviteViewModel"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AccountCreated(InviteViewModel inviteViewModel)
        {
            var dbUser = await _applicationDbContext.Users.Where(u => u.Id == inviteViewModel.Id).FirstOrDefaultAsync();
            if (dbUser != null)
            {
                if (String.IsNullOrWhiteSpace(inviteViewModel.FirstName) ||
                    String.IsNullOrWhiteSpace(inviteViewModel.LastName) ||
                    String.IsNullOrWhiteSpace(inviteViewModel.Password) ||
                    String.IsNullOrWhiteSpace(inviteViewModel.ConfirmPassword))
                {
                    inviteViewModel.ErrorMessage = "Please provide information for all fields.";
                    return RedirectToAction("Signup", "Invite", inviteViewModel);
                }

                if (inviteViewModel.Password != inviteViewModel.ConfirmPassword)
                {
                    inviteViewModel.ErrorMessage = "Password and Confirm Password do not match.";
                    return RedirectToAction("Signup", "Invite", inviteViewModel);
                }

                var identityUser = await _userManager.ResetPasswordAsync(dbUser, inviteViewModel.ResetToken, inviteViewModel.Password);
                if (identityUser.Succeeded)
                {
                    // Apply roles and permissions from the invite
                    var pendingInvite = await _applicationDbContext.UserInvites
                        .Where(i => i.InvitedUserId == inviteViewModel.Id && i.Status == InviteStatus.Pending)
                        .FirstOrDefaultAsync();

                    if (pendingInvite != null)
                    {
                        await inviteService.ApplyInviteRolesAndPermissions(inviteViewModel.Id, pendingInvite.Id);
                    }

                    await inviteService.OnInviteCompleted(dbUser, inviteViewModel);
                    await _applicationDbContext.SaveChangesAsync();

                    return Redirect(appSettings.InviteSignupRedirectUrl);
                }
                else
                {
                    var error = identityUser.Errors.FirstOrDefault();
                    if (error != null)
                    {
                        inviteViewModel.ErrorMessage = error.Description;
                        return RedirectToAction("Signup", "Invite", inviteViewModel);
                    }
                }
            }
            return RedirectToAction("Signup", "Invite", inviteViewModel);
        }
    }
}
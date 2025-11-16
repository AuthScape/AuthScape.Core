// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using AuthScape.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace mvcTest.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ProviderDisplayName { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // If the user does not have an account, check if we can auto-register
                ReturnUrl = returnUrl;
                ProviderDisplayName = info.ProviderDisplayName;

                // Try to get email from external provider claims
                string email = null;
                if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    email = info.Principal.FindFirstValue(ClaimTypes.Email);
                }

                // Auto-register if we have a valid email from the provider
                if (!string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email))
                {
                    // Check if email is already in use
                    var existingUser = await _userManager.FindByEmailAsync(email);
                    if (existingUser == null)
                    {
                        // Extract first and last name from OAuth claims
                        string firstName = null;
                        string lastName = null;

                        // Try to get first name from various claim types
                        if (info.Principal.HasClaim(c => c.Type == ClaimTypes.GivenName))
                        {
                            firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
                        }
                        else if (info.Principal.HasClaim(c => c.Type == "given_name"))
                        {
                            firstName = info.Principal.FindFirstValue("given_name");
                        }

                        // Try to get last name from various claim types
                        if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Surname))
                        {
                            lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);
                        }
                        else if (info.Principal.HasClaim(c => c.Type == "family_name"))
                        {
                            lastName = info.Principal.FindFirstValue("family_name");
                        }

                        // If we don't have first/last name, try to parse from full name
                        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                        {
                            string fullName = null;
                            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Name))
                            {
                                fullName = info.Principal.FindFirstValue(ClaimTypes.Name);
                            }
                            else if (info.Principal.HasClaim(c => c.Type == "name"))
                            {
                                fullName = info.Principal.FindFirstValue("name");
                            }

                            if (!string.IsNullOrWhiteSpace(fullName))
                            {
                                var nameParts = fullName.Split(' ', 2);
                                firstName = nameParts[0];
                                if (nameParts.Length > 1)
                                {
                                    lastName = nameParts[1];
                                }
                            }
                        }

                        // Ensure we have at least some values for required fields
                        if (string.IsNullOrWhiteSpace(firstName))
                        {
                            firstName = email.Split('@')[0]; // Use email username as fallback
                        }
                        if (string.IsNullOrWhiteSpace(lastName))
                        {
                            lastName = "User"; // Default last name
                        }

                        // Auto-create the account
                        var user = CreateUser();
                        await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
                        await _emailStore.SetEmailAsync(user, email, CancellationToken.None);

                        // Set additional user properties
                        user.FirstName = firstName;
                        user.LastName = lastName;
                        user.Created = DateTimeOffset.Now;
                        user.LastLoggedIn = DateTimeOffset.Now;
                        user.IsActive = true;

                        var createResult = await _userManager.CreateAsync(user);
                        if (createResult.Succeeded)
                        {
                            var addLoginResult = await _userManager.AddLoginAsync(user, info);
                            if (addLoginResult.Succeeded)
                            {
                                _logger.LogInformation("User created an account using {Name} provider with auto-registration.", info.LoginProvider);

                                var userId = await _userManager.GetUserIdAsync(user);
                                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                                var callbackUrl = Url.Page(
                                    "/Account/ConfirmEmail",
                                    pageHandler: null,
                                    values: new { area = "Identity", userId = userId, code = code },
                                    protocol: Request.Scheme);

                                await _emailSender.SendEmailAsync(email, "Confirm your email",
                                    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                                // If account confirmation is required, redirect to confirmation page
                                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                                {
                                    return RedirectToPage("./RegisterConfirmation", new { Email = email });
                                }

                                // Sign in the user immediately
                                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                                return LocalRedirect(returnUrl);
                            }
                        }

                        // If auto-registration failed, log errors and fall through to manual confirmation
                        foreach (var error in createResult.Errors)
                        {
                            _logger.LogWarning("Auto-registration failed: {Error}", error.Description);
                        }
                    }
                }

                // Fall back to manual confirmation if:
                // - No email provided by provider
                // - Email is invalid
                // - Email already exists
                // - Auto-registration failed
                Input = new InputModel
                {
                    Email = email
                };
                return Page();
            }
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        var userId = await _userManager.GetUserIdAsync(user);
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = userId, code = code },
                            protocol: Request.Scheme);

                        await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                        // If account confirmation is required, we need to show the link if we don't have a real email sender
                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                        }

                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(AppUser)}'. " +
                    $"Ensure that '{nameof(AppUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        private IUserEmailStore<AppUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<AppUser>)_userStore;
        }
    }
}

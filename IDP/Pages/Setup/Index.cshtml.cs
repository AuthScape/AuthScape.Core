using AuthScape.Models.Users;
using IDP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IDP.Pages.Setup
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly ISetupService _setupService;
        private readonly ILogger<IndexModel> _logger;
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly IOpenIddictScopeManager _scopeManager;

        public IndexModel(
            UserManager<AppUser> userManager,
            RoleManager<Role> roleManager,
            ISetupService setupService,
            ILogger<IndexModel> logger,
            IOpenIddictApplicationManager applicationManager,
            IOpenIddictScopeManager scopeManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _setupService = setupService;
            _logger = logger;
            _applicationManager = applicationManager;
            _scopeManager = scopeManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "First name is required")]
            [Display(Name = "First Name")]
            [StringLength(100)]
            public string FirstName { get; set; }

            [Required(ErrorMessage = "Last name is required")]
            [Display(Name = "Last Name")]
            [StringLength(100)]
            public string LastName { get; set; }

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Required(ErrorMessage = "Password confirmation is required")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            _logger.LogInformation("Setup page GET request");
            // Check if setup is still required
            var setupRequired = await _setupService.IsSetupRequiredAsync();
            if (!setupRequired)
            {
                _logger.LogInformation("Setup not required, redirecting");
                // Setup already completed, redirect to home
                return RedirectToPage("/Admin/Index");
            }

            _logger.LogInformation("Showing setup page");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Setup form submitted");
            _logger.LogInformation("Input values - FirstName: {FirstName}, LastName: {LastName}, Email: {Email}",
                Input?.FirstName ?? "NULL",
                Input?.LastName ?? "NULL",
                Input?.Email ?? "NULL");

            // Check if setup is still required
            var setupRequired = await _setupService.IsSetupRequiredAsync();
            if (!setupRequired)
            {
                _logger.LogWarning("Setup already completed, redirecting to admin");
                // Setup already completed, redirect to admin
                return RedirectToPage("/Admin/Index");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogError("Validation error: {Error}", error.ErrorMessage);
                }
                return Page();
            }

            try
            {
                _logger.LogInformation("Starting admin user creation");
                // Step 1: Create the Admin role if it doesn't exist
                var adminRole = await _roleManager.FindByNameAsync("Admin");
                if (adminRole == null)
                {
                    adminRole = new Role
                    {
                        Name = "Admin"
                    };

                    var roleResult = await _roleManager.CreateAsync(adminRole);
                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        return Page();
                    }

                    _logger.LogInformation("Created Admin role during initial setup");
                }

                // Step 2: Create the admin user
                var user = new AppUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    EmailConfirmed = true, // Auto-confirm email for initial admin
                    FirstName = Input.FirstName,
                    LastName = Input.LastName,
                    Created = DateTimeOffset.Now,
                    LastLoggedIn = DateTimeOffset.Now,
                    IsActive = true
                };

                var createResult = await _userManager.CreateAsync(user, Input.Password);
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Page();
                }

                _logger.LogInformation("Created initial admin user: {Email}", Input.Email);

                // Step 3: Assign Admin role to the user
                var addToRoleResult = await _userManager.AddToRoleAsync(user, "Admin");
                if (!addToRoleResult.Succeeded)
                {
                    foreach (var error in addToRoleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Page();
                }

                _logger.LogInformation("Assigned Admin role to user: {Email}", Input.Email);

                // Step 4: Mark setup as complete
                await _setupService.MarkSetupCompleteAsync();

                _logger.LogInformation("Initial setup completed successfully");

                // Step 5: Redirect to home page
                // Note: The application should be restarted after setup for OpenIddict initialization
                TempData["SuccessMessage"] = "Setup completed successfully! Please restart the application and sign in with your admin credentials.";
                return Redirect("/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial setup");
                ModelState.AddModelError(string.Empty, "An error occurred during setup. Please try again.");
                return Page();
            }
        }
    }
}

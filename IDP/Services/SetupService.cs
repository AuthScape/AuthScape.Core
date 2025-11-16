using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Services
{
    public interface ISetupService
    {
        Task<bool> IsSetupRequiredAsync();
        Task MarkSetupCompleteAsync();
    }

    public class SetupService : ISetupService
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<Role> _roleManager;

        public SetupService(
            DatabaseContext context,
            UserManager<AppUser> userManager,
            RoleManager<Role> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Check if initial setup is required
        /// Setup is required if:
        /// 1. Database exists but no Admin role exists, OR
        /// 2. Admin role exists but has no users
        /// </summary>
        public async Task<bool> IsSetupRequiredAsync()
        {
            try
            {
                // Ensure database exists first
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return true;
                }

                // Check if Admin role exists
                var adminRole = await _roleManager.FindByNameAsync("Admin");
                if (adminRole == null)
                {
                    return true;
                }

                // Check if any users have the Admin role
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                if (!adminUsers.Any())
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // If any error occurs (e.g., database doesn't exist), require setup
                return true;
            }
        }

        /// <summary>
        /// Mark setup as complete in the settings table
        /// </summary>
        public async Task MarkSetupCompleteAsync()
        {
            try
            {
                var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Name == "SetupCompleted");

                if (setting == null)
                {
                    _context.Settings.Add(new AuthScape.Models.Settings.Settings
                    {
                        Id = Guid.NewGuid(),
                        SettingTypeId = 1,
                        Name = "SetupCompleted",
                        Value = "true"
                    });
                }
                else
                {
                    setting.Value = "true";
                    _context.Settings.Update(setting);
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                // If Settings table doesn't exist or there's an error, ignore it
                // Setup completion will be detected by presence of Admin role and users
            }
        }
    }
}

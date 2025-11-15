using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthScape.Services
{
    public class RoleDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string? NormalizedName { get; set; }
        public int UserCount { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    public class CreateRoleDto
    {
        public string Name { get; set; }
    }

    public class UpdateRoleDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

    public interface IRoleService
    {
        Task AddRole(string name);
        Task DeleteRole(long Id);
        Task ChangeName(long Id, string name);
        Task<List<RoleDto>> GetAllRolesAsync();
        Task<RoleDto?> GetRoleByIdAsync(long id);
        Task<bool> CreateRoleAsync(CreateRoleDto dto);
        Task<bool> UpdateRoleAsync(UpdateRoleDto dto);
        Task<bool> DeleteRoleAsync(long id);
        Task<bool> RoleExistsAsync(string name, long? excludeId = null);
    }

    public class RoleService : IRoleService
    {
        readonly DatabaseContext context;
        private readonly RoleManager<Role> roleManager;
        private readonly UserManager<AppUser> userManager;

        public RoleService(DatabaseContext context, RoleManager<Role> roleManager, UserManager<AppUser> userManager)
        {
            this.context = context;
            this.roleManager = roleManager;
            this.userManager = userManager;
        }

        public async Task AddRole(string name)
        {
            var newRole = new Models.Users.Role()
            {
                Name = name,
                NormalizedName = name.ToUpper(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

            await context.Roles.AddAsync(newRole);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRole(long Id)
        {
            var role = await context.Roles.Where(r => r.Id == Id).FirstOrDefaultAsync();
            if (role != null)
            {
                context.Roles.Remove(role);
                await context.SaveChangesAsync();
            }
        }

        public async Task ChangeName(long Id, string name)
        {
            var role = await context.Roles.Where(r => r.Id == Id).FirstOrDefaultAsync();
            if (role != null)
            {
                role.Name = name;
                role.NormalizedName = name.ToUpper();
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            var roles = await roleManager.Roles.ToListAsync();
            var roleDtos = new List<RoleDto>();

            foreach (var role in roles)
            {
                var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);

                roleDtos.Add(new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name!,
                    NormalizedName = role.NormalizedName,
                    UserCount = usersInRole.Count
                });
            }

            return roleDtos.OrderBy(r => r.Name).ToList();
        }

        public async Task<RoleDto?> GetRoleByIdAsync(long id)
        {
            var role = await roleManager.FindByIdAsync(id.ToString());
            if (role == null)
                return null;

            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);

            return new RoleDto
            {
                Id = role.Id,
                Name = role.Name!,
                NormalizedName = role.NormalizedName,
                UserCount = usersInRole.Count
            };
        }

        public async Task<bool> CreateRoleAsync(CreateRoleDto dto)
        {
            var role = new Role
            {
                Name = dto.Name
            };

            var result = await roleManager.CreateAsync(role);
            return result.Succeeded;
        }

        public async Task<bool> UpdateRoleAsync(UpdateRoleDto dto)
        {
            var role = await roleManager.FindByIdAsync(dto.Id.ToString());
            if (role == null)
                return false;

            role.Name = dto.Name;
            var result = await roleManager.UpdateAsync(role);
            return result.Succeeded;
        }

        public async Task<bool> DeleteRoleAsync(long id)
        {
            var role = await roleManager.FindByIdAsync(id.ToString());
            if (role == null)
                return false;

            // Check if role has users
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
            if (usersInRole.Any())
                return false; // Cannot delete role with users

            var result = await roleManager.DeleteAsync(role);
            return result.Succeeded;
        }

        public async Task<bool> RoleExistsAsync(string name, long? excludeId = null)
        {
            var role = await roleManager.FindByNameAsync(name);
            if (role == null)
                return false;

            if (excludeId.HasValue && role.Id == excludeId.Value)
                return false;

            return true;
        }
    }
}

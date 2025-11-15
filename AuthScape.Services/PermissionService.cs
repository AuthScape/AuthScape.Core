using Microsoft.EntityFrameworkCore;
using Models.Users;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthScape.Services
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class CreatePermissionDto
    {
        public string Name { get; set; }
    }

    public class UpdatePermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public interface IPermissionService
    {
        Task<List<PermissionDto>> GetAllPermissionsAsync();
        Task<PermissionDto?> GetPermissionByIdAsync(Guid id);
        Task<bool> CreatePermissionAsync(CreatePermissionDto dto);
        Task<bool> UpdatePermissionAsync(UpdatePermissionDto dto);
        Task<bool> DeletePermissionAsync(Guid id);
        Task<bool> PermissionExistsAsync(string name, Guid? excludeId = null);
    }

    public class PermissionService : IPermissionService
    {
        readonly DatabaseContext context;

        public PermissionService(DatabaseContext context)
        {
            this.context = context;
        }

        public async Task<List<PermissionDto>> GetAllPermissionsAsync()
        {
            var permissions = await context.Permissions.ToListAsync();

            return permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name
            }).OrderBy(p => p.Name).ToList();
        }

        public async Task<PermissionDto?> GetPermissionByIdAsync(Guid id)
        {
            var permission = await context.Permissions.FindAsync(id);
            if (permission == null)
                return null;

            return new PermissionDto
            {
                Id = permission.Id,
                Name = permission.Name
            };
        }

        public async Task<bool> CreatePermissionAsync(CreatePermissionDto dto)
        {
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = dto.Name
            };

            await context.Permissions.AddAsync(permission);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePermissionAsync(UpdatePermissionDto dto)
        {
            var permission = await context.Permissions.FindAsync(dto.Id);
            if (permission == null)
                return false;

            permission.Name = dto.Name;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePermissionAsync(Guid id)
        {
            var permission = await context.Permissions.FindAsync(id);
            if (permission == null)
                return false;

            context.Permissions.Remove(permission);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PermissionExistsAsync(string name, Guid? excludeId = null)
        {
            var permission = await context.Permissions
                .Where(p => p.Name == name)
                .FirstOrDefaultAsync();

            if (permission == null)
                return false;

            if (excludeId.HasValue && permission.Id == excludeId.Value)
                return false;

            return true;
        }
    }
}

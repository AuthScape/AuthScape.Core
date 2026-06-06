using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Custom-permission CRUD lived in the old UserManageSystem CRM table that's no longer part
    /// of the auth core. The interface stays so InviteSettings + the IDP admin page still inject
    /// it; all methods are no-ops returning empty results. Restore behavior by bringing back the
    /// Permissions DbSet (or by using ASP.NET Identity claims/roles natively instead).
    /// </summary>
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
        public Task<List<PermissionDto>> GetAllPermissionsAsync()
            => Task.FromResult(new List<PermissionDto>());

        public Task<PermissionDto?> GetPermissionByIdAsync(Guid id)
            => Task.FromResult<PermissionDto?>(null);

        public Task<bool> CreatePermissionAsync(CreatePermissionDto dto)
            => Task.FromResult(false);

        public Task<bool> UpdatePermissionAsync(UpdatePermissionDto dto)
            => Task.FromResult(false);

        public Task<bool> DeletePermissionAsync(Guid id)
            => Task.FromResult(false);

        public Task<bool> PermissionExistsAsync(string name, Guid? excludeId = null)
            => Task.FromResult(false);
    }
}

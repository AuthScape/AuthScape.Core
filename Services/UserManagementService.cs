using AuthScape.Models.Users;
using Microsoft.AspNetCore.Http;
using Models.Users;
using Newtonsoft.Json;
using System.Security.Claims;

namespace AuthScape.Services
{
    /// <summary>
    /// Hydrates a <see cref="SignedInUser"/> snapshot from the claims attached to the current
    /// request's principal. The IDP-issued access token is the source of truth — this service
    /// just projects the relevant claims into a strongly-typed shape that controllers and
    /// services can consume without re-parsing claims everywhere.
    /// </summary>
    public interface IUserManagementService
    {
        Task<SignedInUser?> GetSignedInUser();
    }

    public class UserManagementService : IUserManagementService
    {
        readonly IHttpContextAccessor httpContextAccessor;

        public UserManagementService(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public Task<SignedInUser?> GetSignedInUser()
        {
            var identity = httpContextAccessor.HttpContext?.User.Identity as ClaimsIdentity;
            if (identity == null || !identity.IsAuthenticated)
            {
                return Task.FromResult<SignedInUser?>(null);
            }

            var sub = identity.FindFirst("sub")?.Value;
            var username = identity.FindFirst("username")?.Value;
            if (sub == null || username == null)
            {
                return Task.FromResult<SignedInUser?>(null);
            }

            var user = new SignedInUser
            {
                Id    = Convert.ToInt64(sub),
                Email = username,

                FirstName    = identity.FindFirst("firstName")?.Value,
                LastName     = identity.FindFirst("lastName")?.Value,
                CompanyId    = ParseLong(identity.FindFirst("companyId")?.Value),
                LocationId   = ParseLong(identity.FindFirst("locationId")?.Value),
                CompanyName  = identity.FindFirst("companyName")?.Value,
                LocationName = identity.FindFirst("locationName")?.Value,
            };

            var permissionsJson = identity.FindFirst("userPermissions")?.Value;
            if (!string.IsNullOrWhiteSpace(permissionsJson))
            {
                user.Permissions = JsonConvert.DeserializeObject<List<Permission>>(permissionsJson);
            }

            var rolesJson = identity.FindFirst("usersRoles")?.Value;
            if (!string.IsNullOrWhiteSpace(rolesJson))
            {
                user.Roles = JsonConvert.DeserializeObject<List<QueryRole>>(rolesJson);
            }

            return Task.FromResult<SignedInUser?>(user);
        }

        private static long? ParseLong(string? value)
            => long.TryParse(value, out var parsed) ? parsed : null;
    }
}

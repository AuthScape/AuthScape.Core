using AuthScape.Scim.Models.Dtos;

namespace AuthScape.Scim;

/// <summary>
/// Tenant-scoped SCIM 2.0 service. All operations take a CompanyId so the controllers
/// can route every request to the right tenant based on the bearer token's `scim_company_id` claim.
///
/// This service is the single seam between SCIM-the-protocol and AppUser-the-store.
/// The same interface could power a future bulk-import admin UI or a CLI tool without
/// changing the SCIM HTTP layer.
/// </summary>
public interface IScimService
{
    Task<ScimListResponse<ScimUser>> ListUsersAsync(long companyId, ScimQuery query, CancellationToken cancellationToken = default);
    Task<ScimUser?> GetUserAsync(long companyId, string id, CancellationToken cancellationToken = default);
    Task<ScimUser> CreateUserAsync(long companyId, ScimUser user, CancellationToken cancellationToken = default);
    Task<ScimUser> ReplaceUserAsync(long companyId, string id, ScimUser user, CancellationToken cancellationToken = default);
    Task<ScimUser> PatchUserAsync(long companyId, string id, ScimPatchRequest patch, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(long companyId, string id, CancellationToken cancellationToken = default);
}

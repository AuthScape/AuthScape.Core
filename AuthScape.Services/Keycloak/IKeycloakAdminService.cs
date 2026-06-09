using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthScape.Services.Keycloak
{
    /// <summary>
    /// Manages a Keycloak realm via its Admin REST API.
    /// All methods throw <see cref="KeycloakAdminException"/> when Keycloak is unreachable, returns a non-success status,
    /// or the admin client is misconfigured. The controller layer translates these to typed HTTP responses so the
    /// AuthScape NextJS portal can render a "Keycloak unavailable" banner without confusing it with a real 500.
    /// </summary>
    public interface IKeycloakAdminService
    {
        Task<KeycloakHealthDto> CheckHealthAsync();

        Task<List<KeycloakClientDto>> GetClientsAsync();
        Task<KeycloakClientDto> GetClientAsync(string id);
        Task<string> CreateClientAsync(KeycloakClientCreateDto dto);
        Task UpdateClientAsync(KeycloakClientUpdateDto dto);
        Task DeleteClientAsync(string id);

        Task<List<KeycloakUserDto>> GetUsersAsync(int first = 0, int max = 100, string search = null);
        Task<KeycloakUserDto> GetUserAsync(string id);
        Task<string> CreateUserAsync(KeycloakUserCreateDto dto);
        Task UpdateUserAsync(KeycloakUserUpdateDto dto);
        Task DeleteUserAsync(string id);
        Task SendPasswordResetEmailAsync(string userId);

        Task<List<KeycloakClientScopeDto>> GetClientScopesAsync();
        Task CreateClientScopeAsync(KeycloakClientScopeCreateDto dto);
        Task UpdateClientScopeAsync(KeycloakClientScopeUpdateDto dto);
        Task DeleteClientScopeAsync(string id);
    }
}

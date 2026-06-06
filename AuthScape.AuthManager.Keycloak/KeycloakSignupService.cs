using AuthScape.Services.Keycloak;

namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// Creates users in Keycloak through the existing <see cref="IKeycloakAdminService"/> (admin REST
/// API client). Maps the typed <see cref="KeycloakAdminException"/> failure kinds onto AuthScape's
/// <see cref="SignupErrorCode"/> values so the host controller can return appropriate HTTP status
/// codes without depending on Keycloak's exception types.
/// </summary>
public sealed class KeycloakSignupService : IAuthScapeSignupService
{
    private readonly IKeycloakAdminService admin;

    public KeycloakSignupService(IKeycloakAdminService admin)
    {
        this.admin = admin;
    }

    /// <inheritdoc />
    public async Task<SignupResult> SignUpAsync(SignupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return SignupResult.Fail(SignupErrorCode.ValidationFailed, "Email is required.");

        var dto = new KeycloakUserCreateDto
        {
            Username = request.Email,
            Email = request.Email,
            FirstName = request.GivenName,
            LastName = request.FamilyName,
            Enabled = true,
            EmailVerified = false,
            InitialPassword = request.Password,
            TemporaryPassword = request.RequirePasswordReset,
        };

        try
        {
            var providerUserId = await admin.CreateUserAsync(dto);
            if (string.IsNullOrEmpty(providerUserId))
                return SignupResult.Fail(SignupErrorCode.Unknown, "Keycloak returned no user id on create.");

            if (request.RequirePasswordReset)
            {
                // Best-effort: trigger Keycloak's UPDATE_PASSWORD action email. Failures here don't roll
                // back the user creation — the admin can resend manually.
                try { await admin.SendPasswordResetEmailAsync(providerUserId); }
                catch (KeycloakAdminException) { }
            }

            return SignupResult.Ok(providerUserId);
        }
        catch (KeycloakAdminException ex)
        {
            var code = ex.Kind switch
            {
                KeycloakAdminFailureKind.Disabled => SignupErrorCode.ProviderDisabled,
                KeycloakAdminFailureKind.Misconfigured => SignupErrorCode.ProviderMisconfigured,
                KeycloakAdminFailureKind.Unauthorized => SignupErrorCode.ProviderUnauthorized,
                KeycloakAdminFailureKind.Unreachable => SignupErrorCode.ProviderUnreachable,
                _ => SignupErrorCode.Unknown,
            };
            return SignupResult.Fail(code, ex.Message);
        }
    }
}

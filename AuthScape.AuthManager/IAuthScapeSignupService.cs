namespace AuthScape.AuthManager;

/// <summary>
/// Provider-agnostic signup operation. Each provider package registers its own implementation —
/// the host signup controller calls only this interface and doesn't know whether the user is being
/// created in OpenIddict's local store or in a Keycloak realm.
/// </summary>
public interface IAuthScapeSignupService
{
    /// <summary>Create a new user record in the active provider's store.</summary>
    Task<SignupResult> SignUpAsync(SignupRequest request, CancellationToken ct = default);
}

/// <summary>
/// Inputs to <see cref="IAuthScapeSignupService.SignUpAsync"/>.
/// </summary>
public class SignupRequest
{
    /// <summary>Primary email — used as the username for both OpenIddict (UserName) and Keycloak.</summary>
    public string Email { get; set; } = "";

    /// <summary>Initial password. May be null when the provider supports passwordless registration
    /// (e.g. Keycloak's "send invite" flow), in which case the implementation supplies a temporary
    /// password and triggers a reset email.</summary>
    public string? Password { get; set; }

    /// <summary>Given (first) name.</summary>
    public string? GivenName { get; set; }

    /// <summary>Family (last) name.</summary>
    public string? FamilyName { get; set; }

    /// <summary>Optional company scope for the new user.</summary>
    public long? CompanyId { get; set; }

    /// <summary>If true, the provider creates the user but marks them as needing a password reset
    /// before first login. Used for admin-initiated invites.</summary>
    public bool RequirePasswordReset { get; set; }
}

/// <summary>
/// Outcome of a signup operation.
/// </summary>
public class SignupResult
{
    public bool Success { get; set; }
    public string? ProviderUserId { get; set; }
    public long? AppUserId { get; set; }
    public SignupErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static SignupResult Ok(string providerUserId, long? appUserId = null) =>
        new() { Success = true, ProviderUserId = providerUserId, AppUserId = appUserId };

    public static SignupResult Fail(SignupErrorCode code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// Typed signup errors so controllers can map them to appropriate HTTP responses without
/// depending on provider-specific exception types.
/// </summary>
public enum SignupErrorCode
{
    Unknown,
    DuplicateEmail,
    PasswordPolicyFailed,
    ProviderUnreachable,
    ProviderMisconfigured,
    ProviderUnauthorized,
    ProviderDisabled,
    ValidationFailed,
}

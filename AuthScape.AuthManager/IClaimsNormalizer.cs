namespace AuthScape.AuthManager;

/// <summary>
/// Maps an <see cref="ExternalIdentity"/> (raw upstream claims) into the canonical
/// <see cref="AuthScapeIdentity"/>. One normalizer per provider; the registry picks the right
/// one by matching <see cref="ProviderId"/>.
/// </summary>
public interface IClaimsNormalizer
{
    /// <summary>Provider this normalizer handles. Matched against <see cref="ExternalIdentity.ProviderId"/>.</summary>
    string ProviderId { get; }

    /// <summary>Translate provider-specific claim names and structures into the canonical identity shape.
    /// Implementations must populate <see cref="AuthScapeIdentity.ProviderId"/> and <see cref="AuthScapeIdentity.ExternalSub"/>
    /// at minimum.</summary>
    AuthScapeIdentity Normalize(ExternalIdentity external);
}

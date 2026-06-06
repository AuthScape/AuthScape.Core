namespace AuthScape.AccountLinking.Models;

public enum AccountLinkOutcome
{
    /// <summary>No prior AppUser found for this identity; a new AppUser was created.</summary>
    NewUserCreated = 0,

    /// <summary>An existing AppUser was found by verified email; this provider was linked to it.</summary>
    LinkedToExistingUser = 1,

    /// <summary>The (provider, externalId) was already linked; the existing AppUser was returned.</summary>
    ReturnedExistingLink = 2,

    /// <summary>
    /// Policy is NeverAutoLink and an email collision exists. A verification email was sent.
    /// No AppUser was created or modified. The caller must surface the verification UX.
    /// </summary>
    PendingManualVerification = 3,

    /// <summary>
    /// Linking was refused because the upstream did not assert email_verified and the policy
    /// requires verification. A new AppUser was created instead. Caller is informed so it can
    /// surface a notice to the user.
    /// </summary>
    EmailUnverifiedNewUserCreated = 4
}

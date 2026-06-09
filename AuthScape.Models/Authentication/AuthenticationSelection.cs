using Fido2NetLib.Objects;

namespace Models.Authentication
{
    public class AuthenticationSelection
    {
        public UserVerificationRequirement UserVerification { get; set; }
        public AuthenticatorAttachment? AuthenticatorAttachment { get; set; }
        public ResidentKeyRequirement? ResidentKey { get; set; }
    }
}

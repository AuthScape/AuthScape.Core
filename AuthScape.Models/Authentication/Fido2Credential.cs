using Fido2NetLib.Objects;

namespace Models.Authentication
{
    public class Fido2Credential
    {
        public int Id { get; set; }
        public byte[] CredentialId { get; set; }
        public byte[] PublicKey { get; set; }
        public byte[] UserHandle { get; set; }
        public uint SignatureCounter { get; set; }
        public PublicKeyCredentialType CredType { get; set; }
        public DateTime RegDate { get; set; }
        public string? AaGuid { get; set; }

        public string? DeviceName { get; set; }

        // FK to AppUser. The reverse nav lives on AppUser.Credentials so this type stays free of
        // any reference to AppUser, allowing it to ship in the AuthScape.Models NuGet itself
        // (AppUser carries plugin nav properties and so stays in the dev-view Models aggregator).
        public long UserId { get; set; }
    }
}

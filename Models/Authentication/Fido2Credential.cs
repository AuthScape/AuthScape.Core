using AuthScape.Models.Users;

namespace Models.Authentication
{
    public class Fido2Credential
    {
        public int Id { get; set; }
        public byte[] CredentialId { get; set; }
        public byte[] PublicKey { get; set; }
        public byte[] UserHandle { get; set; }
        public uint SignatureCounter { get; set; }
        public string CredType { get; set; }
        public DateTime RegDate { get; set; }
        public string? AaGuid { get; set; }

        public string? DeviceName { get; set; }

        public long UserId { get; set; }
        public AppUser User { get; set; }
    }
}

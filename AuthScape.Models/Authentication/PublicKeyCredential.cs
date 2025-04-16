namespace AuthScape.Models.Authentication
{
    public class PublicKeyCredential
    {
        public byte[] CredentialId { get; set; }
        public byte[] PublicKey { get; set; }
        public byte[] UserId { get; set; }
        public uint SignCount { get; set; }
    }
}
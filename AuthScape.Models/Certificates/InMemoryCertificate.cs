namespace AuthScape.Models.Certificates
{
    public class InMemoryCertificate
    {
        public string CertificateName { get; set; }
        public byte[] CertificateData { get; set; }
        public int ExpiryYear { get; set; }
    }
}
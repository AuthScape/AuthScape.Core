using AuthScape.SendGrid.Models;

namespace Models.Email
{
    public class AchVerificationEmail : BaseEmail
    {
        public string BankLast4 { get; set; }
        public string BankName { get; set; }
        public string ArrivalDate { get; set; }
        public string VerificationLink { get; set; }
        public string HostedVerificationUrl { get; set; }
        public string VerificationType { get; set; }
        public string EmailType { get; set; }
        public string AppName { get; set; }
    }
}

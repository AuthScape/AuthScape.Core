namespace Models.AppSettings
{
    public class SendGridAppSettings
    {
        public string APIKey { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public string AchVerificationInitialTemplateId { get; set; }
        public string AchVerificationReminderTemplateId { get; set; }
    }
}
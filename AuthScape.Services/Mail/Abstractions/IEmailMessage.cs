using System.Collections.Generic;

namespace AuthScape.Services.Mail.Abstractions
{
    /// <summary>
    /// Represents an email message with all necessary properties for sending
    /// </summary>
    public interface IEmailMessage
    {
        /// <summary>
        /// Email subject line
        /// </summary>
        string Subject { get; set; }

        /// <summary>
        /// Plain text content of the email
        /// </summary>
        string TextContent { get; set; }

        /// <summary>
        /// HTML content of the email
        /// </summary>
        string HtmlContent { get; set; }

        /// <summary>
        /// Sender email address
        /// </summary>
        string FromEmail { get; set; }

        /// <summary>
        /// Sender display name
        /// </summary>
        string FromName { get; set; }

        /// <summary>
        /// List of recipient email addresses
        /// </summary>
        List<EmailRecipient> To { get; set; }

        /// <summary>
        /// List of CC recipient email addresses
        /// </summary>
        List<EmailRecipient> Cc { get; set; }

        /// <summary>
        /// List of BCC recipient email addresses
        /// </summary>
        List<EmailRecipient> Bcc { get; set; }

        /// <summary>
        /// Reply-to email address
        /// </summary>
        string ReplyToEmail { get; set; }

        /// <summary>
        /// Reply-to display name
        /// </summary>
        string ReplyToName { get; set; }

        /// <summary>
        /// Email attachments
        /// </summary>
        List<EmailAttachment> Attachments { get; set; }

        /// <summary>
        /// Custom headers for the email
        /// </summary>
        Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Tags for categorizing/tracking emails
        /// </summary>
        List<string> Tags { get; set; }
    }

    /// <summary>
    /// Represents an email recipient
    /// </summary>
    public class EmailRecipient
    {
        public string Email { get; set; }
        public string Name { get; set; }

        public EmailRecipient() { }

        public EmailRecipient(string email, string name = null)
        {
            Email = email;
            Name = name;
        }
    }

    /// <summary>
    /// Represents an email attachment
    /// </summary>
    public class EmailAttachment
    {
        public string FileName { get; set; }
        public byte[] Content { get; set; }
        public string ContentType { get; set; }
        public string ContentId { get; set; }

        public EmailAttachment() { }

        public EmailAttachment(string fileName, byte[] content, string contentType, string contentId = null)
        {
            FileName = fileName;
            Content = content;
            ContentType = contentType;
            ContentId = contentId;
        }
    }
}

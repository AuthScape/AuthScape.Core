using AuthScape.Services.Mail.Abstractions;
using System.Collections.Generic;

namespace AuthScape.Services.Mail.Core
{
    /// <summary>
    /// Default implementation of IEmailMessage
    /// </summary>
    public class EmailMessage : IEmailMessage
    {
        public string Subject { get; set; }
        public string TextContent { get; set; }
        public string HtmlContent { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public List<EmailRecipient> To { get; set; }
        public List<EmailRecipient> Cc { get; set; }
        public List<EmailRecipient> Bcc { get; set; }
        public string ReplyToEmail { get; set; }
        public string ReplyToName { get; set; }
        public List<EmailAttachment> Attachments { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public List<string> Tags { get; set; }

        public EmailMessage()
        {
            To = new List<EmailRecipient>();
            Cc = new List<EmailRecipient>();
            Bcc = new List<EmailRecipient>();
            Attachments = new List<EmailAttachment>();
            Headers = new Dictionary<string, string>();
            Tags = new List<string>();
        }

        /// <summary>
        /// Adds a recipient to the To list
        /// </summary>
        public EmailMessage AddTo(string email, string name = null)
        {
            To.Add(new EmailRecipient(email, name));
            return this;
        }

        /// <summary>
        /// Adds a recipient to the Cc list
        /// </summary>
        public EmailMessage AddCc(string email, string name = null)
        {
            Cc.Add(new EmailRecipient(email, name));
            return this;
        }

        /// <summary>
        /// Adds a recipient to the Bcc list
        /// </summary>
        public EmailMessage AddBcc(string email, string name = null)
        {
            Bcc.Add(new EmailRecipient(email, name));
            return this;
        }

        /// <summary>
        /// Adds an attachment
        /// </summary>
        public EmailMessage AddAttachment(string fileName, byte[] content, string contentType, string contentId = null)
        {
            Attachments.Add(new EmailAttachment(fileName, content, contentType, contentId));
            return this;
        }

        /// <summary>
        /// Adds a custom header
        /// </summary>
        public EmailMessage AddHeader(string key, string value)
        {
            Headers[key] = value;
            return this;
        }

        /// <summary>
        /// Adds a tag
        /// </summary>
        public EmailMessage AddTag(string tag)
        {
            Tags.Add(tag);
            return this;
        }
    }
}

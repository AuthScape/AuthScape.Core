using System.Collections.Generic;

namespace AuthScape.Services.Mail.Abstractions
{
    /// <summary>
    /// Represents the response from an email send operation
    /// </summary>
    public interface IEmailResponse
    {
        /// <summary>
        /// Indicates whether the email was sent successfully
        /// </summary>
        bool IsSuccess { get; set; }

        /// <summary>
        /// Error message if the send operation failed
        /// </summary>
        string ErrorMessage { get; set; }

        /// <summary>
        /// Message ID returned by the email provider (if available)
        /// </summary>
        string MessageId { get; set; }

        /// <summary>
        /// HTTP status code from the provider (if applicable)
        /// </summary>
        int? StatusCode { get; set; }

        /// <summary>
        /// Additional metadata from the provider
        /// </summary>
        Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// The email provider that was used to send this message
        /// </summary>
        string Provider { get; set; }
    }
}

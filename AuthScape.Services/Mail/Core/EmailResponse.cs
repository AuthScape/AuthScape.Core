using AuthScape.Services.Mail.Abstractions;
using System.Collections.Generic;

namespace AuthScape.Services.Mail.Core
{
    /// <summary>
    /// Default implementation of IEmailResponse
    /// </summary>
    public class EmailResponse : IEmailResponse
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string MessageId { get; set; }
        public int? StatusCode { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public string Provider { get; set; }

        public EmailResponse()
        {
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        public static EmailResponse Success(string provider, string messageId = null, int? statusCode = null)
        {
            return new EmailResponse
            {
                IsSuccess = true,
                Provider = provider,
                MessageId = messageId,
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// Creates a failure response
        /// </summary>
        public static EmailResponse Failure(string provider, string errorMessage, int? statusCode = null)
        {
            return new EmailResponse
            {
                IsSuccess = false,
                Provider = provider,
                ErrorMessage = errorMessage,
                StatusCode = statusCode
            };
        }
    }
}

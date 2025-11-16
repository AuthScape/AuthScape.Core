using System.Collections.Generic;

namespace AuthScape.Models.Notifications
{
    /// <summary>
    /// Request model for sending push notifications
    /// </summary>
    public class SendPushNotificationRequest
    {
        /// <summary>
        /// User IDs to send notification to (optional, if not specified sends to all users or uses DeviceTokens)
        /// </summary>
        public List<long>? UserIds { get; set; }

        /// <summary>
        /// Specific device tokens to send to (optional)
        /// </summary>
        public List<string>? DeviceTokens { get; set; }

        /// <summary>
        /// Notification title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Notification body/message
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Optional image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Optional click action / deep link
        /// </summary>
        public string? ClickAction { get; set; }

        /// <summary>
        /// Additional data payload as key-value pairs
        /// </summary>
        public Dictionary<string, string>? Data { get; set; }

        /// <summary>
        /// Badge count for iOS
        /// </summary>
        public int? Badge { get; set; }

        /// <summary>
        /// Sound file name
        /// </summary>
        public string? Sound { get; set; }

        /// <summary>
        /// Notification priority (high, normal, low)
        /// </summary>
        public string? Priority { get; set; }
    }
}

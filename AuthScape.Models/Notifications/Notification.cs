using AuthScape.Models.Users;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.Notifications
{
    public class Notification
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public long? UserId { get; set; }
        public long? CompanyId { get; set; }
        public long? LocationId { get; set; }

        public string Title { get; set; }
        public string Message { get; set; }
        public string? LinkUrl { get; set; }

        public int? CategoryId { get; set; }
        public NotificationSeverity Severity { get; set; }

        public bool IsRead { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
        public DateTimeOffset? TriggeredAt { get; set; }

        public bool EmailSent { get; set; }
        public bool SmsSent { get; set; }
        public DateTimeOffset? EmailSentAt { get; set; }
        public DateTimeOffset? SmsSentAt { get; set; }

        public string? Metadata { get; set; } // JSON for extra data

        public NotificationCategoryConfig? Category { get; set; }
    }

    public enum NotificationSeverity
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }
}

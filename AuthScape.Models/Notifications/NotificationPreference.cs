using AuthScape.Models.Users;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.Notifications
{
    public class NotificationPreference
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public long UserId { get; set; }

        public int CategoryId { get; set; }
        public bool InAppEnabled { get; set; }
        public bool EmailEnabled { get; set; }
        public bool SmsEnabled { get; set; }

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Modified { get; set; }

        // Navigation
        public NotificationCategoryConfig Category { get; set; }
    }
}

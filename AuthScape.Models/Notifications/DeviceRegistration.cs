using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.Notifications
{
    /// <summary>
    /// Represents a registered device for push notifications
    /// </summary>
    public class DeviceRegistration
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// User ID this device belongs to
        /// </summary>
        [Required]
        public long UserId { get; set; }

        /// <summary>
        /// Device token from FCM (Android) or APNs (iOS)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string DeviceToken { get; set; }

        /// <summary>
        /// Platform type (iOS, Android, Web)
        /// </summary>
        [Required]
        public DevicePlatform Platform { get; set; }

        /// <summary>
        /// Device name/model (e.g., "iPhone 14 Pro", "Samsung Galaxy S23")
        /// </summary>
        [MaxLength(200)]
        public string? DeviceName { get; set; }

        /// <summary>
        /// OS version (e.g., "iOS 17.2", "Android 14")
        /// </summary>
        [MaxLength(50)]
        public string? OsVersion { get; set; }

        /// <summary>
        /// App version that registered this device
        /// </summary>
        [MaxLength(50)]
        public string? AppVersion { get; set; }

        /// <summary>
        /// Device locale/language (e.g., "en-US", "es-ES")
        /// </summary>
        [MaxLength(10)]
        public string? Locale { get; set; }

        /// <summary>
        /// Time zone identifier (e.g., "America/New_York")
        /// </summary>
        [MaxLength(100)]
        public string? TimeZoneId { get; set; }

        /// <summary>
        /// Whether this device is active and should receive notifications
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When the device was first registered
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Last time the device token was updated or verified
        /// </summary>
        public DateTimeOffset LastUpdatedAt { get; set; }

        /// <summary>
        /// Last time a notification was successfully sent to this device
        /// </summary>
        public DateTimeOffset? LastNotificationSentAt { get; set; }

        /// <summary>
        /// Number of failed notification attempts (for detecting invalid tokens)
        /// </summary>
        public int FailedAttempts { get; set; } = 0;

        /// <summary>
        /// Endpoint for web push notifications (for Web platform)
        /// </summary>
        [MaxLength(1000)]
        public string? WebPushEndpoint { get; set; }

        /// <summary>
        /// P256DH key for web push notifications
        /// </summary>
        [MaxLength(200)]
        public string? WebPushP256DH { get; set; }

        /// <summary>
        /// Auth key for web push notifications
        /// </summary>
        [MaxLength(200)]
        public string? WebPushAuth { get; set; }

        /// <summary>
        /// Additional metadata as JSON
        /// </summary>
        public string? Metadata { get; set; }
    }
}

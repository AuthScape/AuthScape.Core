using System.ComponentModel.DataAnnotations;

namespace AuthScape.Models.Notifications
{
    /// <summary>
    /// Request model for registering a device for push notifications
    /// </summary>
    public class RegisterDeviceRequest
    {
        /// <summary>
        /// Device token from FCM (Android) or APNs (iOS)
        /// </summary>
        [Required(ErrorMessage = "Device token is required")]
        public string DeviceToken { get; set; }

        /// <summary>
        /// Platform type (iOS, Android, Web)
        /// </summary>
        [Required(ErrorMessage = "Platform is required")]
        public DevicePlatform Platform { get; set; }

        /// <summary>
        /// Device name/model (optional)
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// OS version (optional)
        /// </summary>
        public string? OsVersion { get; set; }

        /// <summary>
        /// App version (optional)
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        /// Device locale/language (optional)
        /// </summary>
        public string? Locale { get; set; }

        /// <summary>
        /// Time zone identifier (optional)
        /// </summary>
        public string? TimeZoneId { get; set; }

        /// <summary>
        /// Web push endpoint (required for Web platform)
        /// </summary>
        public string? WebPushEndpoint { get; set; }

        /// <summary>
        /// P256DH key for web push (required for Web platform)
        /// </summary>
        public string? WebPushP256DH { get; set; }

        /// <summary>
        /// Auth key for web push (required for Web platform)
        /// </summary>
        public string? WebPushAuth { get; set; }

        /// <summary>
        /// Additional metadata as JSON (optional)
        /// </summary>
        public string? Metadata { get; set; }
    }
}

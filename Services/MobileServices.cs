using AuthScape.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Services
{
    public interface IMobileServices
    {
        Task<bool> SendPushNotificationAsync(SendPushNotificationRequest request);
        Task<bool> SendPushNotificationToUserAsync(long userId, string title, string body, Dictionary<string, string>? data = null);
        Task<bool> SendPushNotificationToUsersAsync(List<long> userIds, string title, string body, Dictionary<string, string>? data = null);
    }

    public class MobileServices : IMobileServices
    {
        private readonly DatabaseContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MobileServices> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public MobileServices(
            DatabaseContext context,
            IConfiguration configuration,
            ILogger<MobileServices> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public void SendTwoFactorCode()
        {
            // send the two factor code using Twilio
        }

        /// <summary>
        /// Send push notification based on request parameters
        /// </summary>
        public async Task<bool> SendPushNotificationAsync(SendPushNotificationRequest request)
        {
            try
            {
                List<DeviceRegistration> devices;

                // Determine which devices to send to
                if (request.DeviceTokens != null && request.DeviceTokens.Any())
                {
                    // Send to specific device tokens
                    devices = await _context.DeviceRegistrations
                        .Where(d => request.DeviceTokens.Contains(d.DeviceToken) && d.IsActive)
                        .ToListAsync();
                }
                else if (request.UserIds != null && request.UserIds.Any())
                {
                    // Send to specific users
                    devices = await _context.DeviceRegistrations
                        .Where(d => request.UserIds.Contains(d.UserId) && d.IsActive)
                        .ToListAsync();
                }
                else
                {
                    _logger.LogWarning("No target devices or users specified for push notification");
                    return false;
                }

                if (!devices.Any())
                {
                    _logger.LogWarning("No active devices found for push notification");
                    return false;
                }

                // Group devices by platform
                var iosDevices = devices.Where(d => d.Platform == DevicePlatform.iOS).ToList();
                var androidDevices = devices.Where(d => d.Platform == DevicePlatform.Android).ToList();
                var webDevices = devices.Where(d => d.Platform == DevicePlatform.Web).ToList();

                var tasks = new List<Task<bool>>();

                // Send to iOS devices via APNs
                if (iosDevices.Any())
                {
                    tasks.Add(SendToApnsAsync(iosDevices, request));
                }

                // Send to Android devices via FCM
                if (androidDevices.Any())
                {
                    tasks.Add(SendToFcmAsync(androidDevices, request));
                }

                // Send to Web devices via Web Push
                if (webDevices.Any())
                {
                    tasks.Add(SendToWebPushAsync(webDevices, request));
                }

                var results = await Task.WhenAll(tasks);
                return results.All(r => r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification");
                return false;
            }
        }

        /// <summary>
        /// Send push notification to a single user
        /// </summary>
        public async Task<bool> SendPushNotificationToUserAsync(long userId, string title, string body, Dictionary<string, string>? data = null)
        {
            return await SendPushNotificationAsync(new SendPushNotificationRequest
            {
                UserIds = new List<long> { userId },
                Title = title,
                Body = body,
                Data = data
            });
        }

        /// <summary>
        /// Send push notification to multiple users
        /// </summary>
        public async Task<bool> SendPushNotificationToUsersAsync(List<long> userIds, string title, string body, Dictionary<string, string>? data = null)
        {
            return await SendPushNotificationAsync(new SendPushNotificationRequest
            {
                UserIds = userIds,
                Title = title,
                Body = body,
                Data = data
            });
        }

        /// <summary>
        /// Send notifications to iOS devices via Apple Push Notification service (APNs)
        /// </summary>
        private async Task<bool> SendToApnsAsync(List<DeviceRegistration> devices, SendPushNotificationRequest request)
        {
            try
            {
                // APNs configuration from appsettings
                var apnsKeyId = _configuration["PushNotifications:APNs:KeyId"];
                var apnsTeamId = _configuration["PushNotifications:APNs:TeamId"];
                var apnsBundleId = _configuration["PushNotifications:APNs:BundleId"];
                var apnsKeyPath = _configuration["PushNotifications:APNs:KeyPath"];

                if (string.IsNullOrEmpty(apnsKeyId) || string.IsNullOrEmpty(apnsTeamId))
                {
                    _logger.LogWarning("APNs configuration is missing. Skipping iOS notifications.");
                    return false;
                }

                // TODO: Implement APNs JWT token generation and HTTP/2 request
                // For now, log that we would send
                _logger.LogInformation($"Would send APNs notification to {devices.Count} iOS devices");

                // Update last notification sent time
                foreach (var device in devices)
                {
                    device.LastNotificationSentAt = DateTimeOffset.Now;
                }
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending APNs notifications");
                return false;
            }
        }

        /// <summary>
        /// Send notifications to Android devices via Firebase Cloud Messaging (FCM)
        /// </summary>
        private async Task<bool> SendToFcmAsync(List<DeviceRegistration> devices, SendPushNotificationRequest request)
        {
            try
            {
                // FCM configuration from appsettings
                var fcmServerKey = _configuration["PushNotifications:FCM:ServerKey"];
                var fcmSenderId = _configuration["PushNotifications:FCM:SenderId"];

                if (string.IsNullOrEmpty(fcmServerKey))
                {
                    _logger.LogWarning("FCM configuration is missing. Skipping Android notifications.");
                    return false;
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"key={fcmServerKey}");

                var successCount = 0;

                foreach (var device in devices)
                {
                    var fcmPayload = new
                    {
                        to = device.DeviceToken,
                        notification = new
                        {
                            title = request.Title,
                            body = request.Body,
                            image = request.ImageUrl,
                            sound = request.Sound ?? "default",
                            click_action = request.ClickAction
                        },
                        data = request.Data,
                        priority = request.Priority ?? "high"
                    };

                    var response = await httpClient.PostAsJsonAsync(
                        "https://fcm.googleapis.com/fcm/send",
                        fcmPayload);

                    if (response.IsSuccessStatusCode)
                    {
                        device.LastNotificationSentAt = DateTimeOffset.Now;
                        device.FailedAttempts = 0;
                        successCount++;
                    }
                    else
                    {
                        device.FailedAttempts++;
                        _logger.LogWarning($"Failed to send FCM notification to device {device.Id}. Failed attempts: {device.FailedAttempts}");

                        // Deactivate device after 5 failed attempts
                        if (device.FailedAttempts >= 5)
                        {
                            device.IsActive = false;
                            _logger.LogWarning($"Deactivated device {device.Id} after {device.FailedAttempts} failed attempts");
                        }
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Sent FCM notifications to {successCount}/{devices.Count} Android devices");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM notifications");
                return false;
            }
        }

        /// <summary>
        /// Send notifications to web browsers via Web Push
        /// </summary>
        private async Task<bool> SendToWebPushAsync(List<DeviceRegistration> devices, SendPushNotificationRequest request)
        {
            try
            {
                // Web Push requires VAPID keys
                var vapidPublicKey = _configuration["PushNotifications:WebPush:PublicKey"];
                var vapidPrivateKey = _configuration["PushNotifications:WebPush:PrivateKey"];
                var vapidSubject = _configuration["PushNotifications:WebPush:Subject"];

                if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
                {
                    _logger.LogWarning("Web Push configuration is missing. Skipping web notifications.");
                    return false;
                }

                // TODO: Implement Web Push using WebPush library
                // For now, log that we would send
                _logger.LogInformation($"Would send Web Push notification to {devices.Count} web devices");

                // Update last notification sent time
                foreach (var device in devices)
                {
                    device.LastNotificationSentAt = DateTimeOffset.Now;
                }
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Web Push notifications");
                return false;
            }
        }
    }
}

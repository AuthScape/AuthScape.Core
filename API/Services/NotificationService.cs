using AuthScape.Core.Hubs;
using AuthScape.Models.Notifications;
using AuthScape.SendGrid;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthScape.API.Services
{
    public interface INotificationService
    {
        // Create notifications
        Task<Guid> CreateNotificationAsync(CreateNotificationRequest request);
        Task CreateBulkNotificationsAsync(IEnumerable<CreateNotificationRequest> requests);

        // Query notifications
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(long userId, bool unreadOnly = false, int take = 50);
        Task<IEnumerable<Notification>> GetCompanyNotificationsAsync(long companyId, bool unreadOnly = false, int take = 50);
        Task<IEnumerable<Notification>> GetLocationNotificationsAsync(long locationId, bool unreadOnly = false, int take = 50);
        Task<int> GetUnreadCountAsync(long userId);

        // Update notifications
        Task MarkAsReadAsync(Guid notificationId, long userId);
        Task MarkAllAsReadAsync(long userId);
        Task DeleteNotificationAsync(Guid notificationId, long userId);
        Task ClearAllNotificationsAsync(long userId);

        // Preferences
        Task<IEnumerable<NotificationPreference>> GetPreferencesAsync(long userId);
        Task UpdatePreferenceAsync(long userId, int categoryId, bool inApp, bool email, bool sms);
    }

    public class CreateNotificationRequest
    {
        public long? UserId { get; set; }
        public long? CompanyId { get; set; }
        public long? LocationId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string LinkUrl { get; set; }
        public int? CategoryId { get; set; }
        public NotificationSeverity Severity { get; set; }
        public bool SendEmail { get; set; }
        public bool SendSms { get; set; }
        public string Metadata { get; set; }
    }

    public class NotificationService : INotificationService
    {
        private readonly DatabaseContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ISendGridService _sendGridService;

        public NotificationService(
            DatabaseContext context,
            IHubContext<NotificationHub> hubContext,
            ISendGridService sendGridService)
        {
            _context = context;
            _hubContext = hubContext;
            _sendGridService = sendGridService;
        }

        public async Task<Guid> CreateNotificationAsync(CreateNotificationRequest request)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                CompanyId = request.CompanyId,
                LocationId = request.LocationId,
                Title = request.Title,
                Message = request.Message,
                LinkUrl = request.LinkUrl,
                CategoryId = request.CategoryId,
                Severity = request.Severity,
                IsRead = false,
                Created = DateTimeOffset.UtcNow,
                TriggeredAt = DateTimeOffset.UtcNow,
                Metadata = request.Metadata
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Load category for broadcasting
            if (notification.CategoryId.HasValue)
            {
                notification.Category = await _context.NotificationCategoryConfigs
                    .FindAsync(notification.CategoryId.Value);
            }

            // Send via SignalR
            await BroadcastNotificationAsync(notification);

            // Send via email if requested
            if (request.SendEmail && notification.UserId.HasValue)
            {
                await SendEmailNotificationAsync(notification);
            }

            // Send via SMS if requested
            if (request.SendSms && notification.UserId.HasValue)
            {
                await SendSmsNotificationAsync(notification);
            }

            return notification.Id;
        }

        public async Task CreateBulkNotificationsAsync(IEnumerable<CreateNotificationRequest> requests)
        {
            foreach (var request in requests)
            {
                await CreateNotificationAsync(request);
            }
        }

        private async Task BroadcastNotificationAsync(Notification notification)
        {
            var payload = new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                linkUrl = notification.LinkUrl,
                categoryId = notification.CategoryId,
                categoryName = notification.Category?.Name,
                severity = notification.Severity.ToString(),
                created = notification.Created,
                isRead = notification.IsRead
            };

            if (notification.UserId.HasValue)
            {
                await _hubContext.Clients.Group($"user_{notification.UserId.Value}")
                    .SendAsync("OnNotificationReceived", payload);
            }

            if (notification.CompanyId.HasValue)
            {
                await _hubContext.Clients.Group($"company_{notification.CompanyId.Value}")
                    .SendAsync("OnNotificationReceived", payload);
            }

            if (notification.LocationId.HasValue)
            {
                await _hubContext.Clients.Group($"location_{notification.LocationId.Value}")
                    .SendAsync("OnNotificationReceived", payload);
            }
        }

        private async Task SendEmailNotificationAsync(Notification notification)
        {
            try
            {
                var user = await _context.Users.FindAsync(notification.UserId);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    var subject = $"{notification.Severity}: {notification.Title}";
                    var body = $@"
                        <h2>{notification.Title}</h2>
                        <p>{notification.Message}</p>
                        {(!string.IsNullOrEmpty(notification.LinkUrl) ? $"<p><a href='{notification.LinkUrl}'>View Details</a></p>" : "")}
                    ";

                    await _sendGridService.SendHtmlEmail(user, subject, body);

                    notification.EmailSent = true;
                    notification.EmailSentAt = DateTimeOffset.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email notification: {ex.Message}");
            }
        }

        private async Task SendSmsNotificationAsync(Notification notification)
        {
            // TODO: Implement SMS sending via Twilio or similar service
            // For now, just mark as sent
            try
            {
                notification.SmsSent = true;
                notification.SmsSentAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send SMS notification: {ex.Message}");
            }
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(long userId, bool unreadOnly = false, int take = 50)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.Created);

            if (unreadOnly)
            {
                query = (IOrderedQueryable<Notification>)query.Where(n => !n.IsRead);
            }

            return await query.Take(take).ToListAsync();
        }

        public async Task<IEnumerable<Notification>> GetCompanyNotificationsAsync(long companyId, bool unreadOnly = false, int take = 50)
        {
            var query = _context.Notifications
                .Where(n => n.CompanyId == companyId)
                .OrderByDescending(n => n.Created);

            if (unreadOnly)
            {
                query = (IOrderedQueryable<Notification>)query.Where(n => !n.IsRead);
            }

            return await query.Take(take).ToListAsync();
        }

        public async Task<IEnumerable<Notification>> GetLocationNotificationsAsync(long locationId, bool unreadOnly = false, int take = 50)
        {
            var query = _context.Notifications
                .Where(n => n.LocationId == locationId)
                .OrderByDescending(n => n.Created);

            if (unreadOnly)
            {
                query = (IOrderedQueryable<Notification>)query.Where(n => !n.IsRead);
            }

            return await query.Take(take).ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(long userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(Guid notificationId, long userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(long userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteNotificationAsync(Guid notificationId, long userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearAllNotificationsAsync(long userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .ToListAsync();

            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<NotificationPreference>> GetPreferencesAsync(long userId)
        {
            return await _context.NotificationPreferences
                .Where(p => p.UserId == userId)
                .ToListAsync();
        }

        public async Task UpdatePreferenceAsync(long userId, int categoryId, bool inApp, bool email, bool sms)
        {
            var preference = await _context.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.CategoryId == categoryId);

            if (preference == null)
            {
                preference = new NotificationPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CategoryId = categoryId,
                    Created = DateTimeOffset.UtcNow
                };
                _context.NotificationPreferences.Add(preference);
            }

            preference.InAppEnabled = inApp;
            preference.EmailEnabled = email;
            preference.SmsEnabled = sms;
            preference.Modified = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}

using AuthScape.API.Services;
using AuthScape.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Services.Context;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthScape.API.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    //TODO: Re-enable after fixing OpenIddict
    //[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IUserManagementService _userManagementService;
        private readonly DatabaseContext _context;

        public NotificationController(
            INotificationService notificationService,
            IUserManagementService userManagementService,
            DatabaseContext context)
        {
            _notificationService = notificationService;
            _userManagementService = userManagementService;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            try
            {
                Console.WriteLine($"CreateNotification called: UserId={request.UserId}, Title={request.Title}");
                var id = await _notificationService.CreateNotificationAsync(request);
                Console.WriteLine($"Notification created with ID: {id}");
                return Ok(new { id, success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating notification: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int take = 50)
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(
                signedInUser.Id, unreadOnly, take);

            return Ok(notifications);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(signedInUser.Id);
            return Ok(new { count });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            await _notificationService.MarkAsReadAsync(request.NotificationId, signedInUser.Id);
            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(signedInUser.Id);
            return Ok(new { success = true });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteNotification([FromQuery] Guid id)
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            await _notificationService.DeleteNotificationAsync(id, signedInUser.Id);
            return Ok(new { success = true });
        }

        [HttpDelete]
        public async Task<IActionResult> ClearAllNotifications()
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            await _notificationService.ClearAllNotificationsAsync(signedInUser.Id);
            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetPreferences()
        {
            var signedInUser = await _userManagementService.GetSignedInUser();
            if (signedInUser == null)
                return Unauthorized();

            var preferences = await _notificationService.GetPreferencesAsync(signedInUser.Id);
            return Ok(preferences);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.NotificationCategoryConfigs
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    description = c.Description
                })
                .ToListAsync();

            return Ok(categories);
        }
    }

    public class MarkAsReadRequest
    {
        public Guid NotificationId { get; set; }
    }
}

using AuthScape.Models.Notifications;
using AuthScape.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Services.Context;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public class PushNotificationsController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PushNotificationsController(
            DatabaseContext context,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Register a device for push notifications
        /// </summary>
        /// <param name="request">Device registration details</param>
        /// <returns>Registration response with device ID</returns>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get the current user ID from the JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            // Validate user exists
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Validate web push requirements
            if (request.Platform == DevicePlatform.Web)
            {
                if (string.IsNullOrWhiteSpace(request.WebPushEndpoint) ||
                    string.IsNullOrWhiteSpace(request.WebPushP256DH) ||
                    string.IsNullOrWhiteSpace(request.WebPushAuth))
                {
                    return BadRequest(new { message = "Web push requires endpoint, p256dh, and auth keys" });
                }
            }

            // Check if device already exists for this user and token
            var existingDevice = await _context.DeviceRegistrations
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceToken == request.DeviceToken);

            bool isNewDevice = existingDevice == null;

            if (existingDevice != null)
            {
                // Update existing device
                existingDevice.Platform = request.Platform;
                existingDevice.DeviceName = request.DeviceName;
                existingDevice.OsVersion = request.OsVersion;
                existingDevice.AppVersion = request.AppVersion;
                existingDevice.Locale = request.Locale;
                existingDevice.TimeZoneId = request.TimeZoneId;
                existingDevice.WebPushEndpoint = request.WebPushEndpoint;
                existingDevice.WebPushP256DH = request.WebPushP256DH;
                existingDevice.WebPushAuth = request.WebPushAuth;
                existingDevice.Metadata = request.Metadata;
                existingDevice.IsActive = true;
                existingDevice.LastUpdatedAt = DateTimeOffset.Now;
                existingDevice.FailedAttempts = 0; // Reset failed attempts on re-registration

                _context.DeviceRegistrations.Update(existingDevice);
            }
            else
            {
                // Create new device registration
                var newDevice = new DeviceRegistration
                {
                    UserId = userId,
                    DeviceToken = request.DeviceToken,
                    Platform = request.Platform,
                    DeviceName = request.DeviceName,
                    OsVersion = request.OsVersion,
                    AppVersion = request.AppVersion,
                    Locale = request.Locale,
                    TimeZoneId = request.TimeZoneId,
                    WebPushEndpoint = request.WebPushEndpoint,
                    WebPushP256DH = request.WebPushP256DH,
                    WebPushAuth = request.WebPushAuth,
                    Metadata = request.Metadata,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.Now,
                    LastUpdatedAt = DateTimeOffset.Now,
                    FailedAttempts = 0
                };

                _context.DeviceRegistrations.Add(newDevice);
                existingDevice = newDevice;
            }

            await _context.SaveChangesAsync();

            return Ok(new RegisterDeviceResponse
            {
                DeviceId = existingDevice.Id,
                Success = true,
                Message = isNewDevice ? "Device registered successfully" : "Device updated successfully",
                IsNewDevice = isNewDevice
            });
        }

        /// <summary>
        /// Unregister a device from push notifications
        /// </summary>
        /// <param name="deviceToken">Device token to unregister</param>
        [HttpPost("unregister")]
        public async Task<IActionResult> UnregisterDevice([FromBody] string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
            {
                return BadRequest(new { message = "Device token is required" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            var device = await _context.DeviceRegistrations
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceToken == deviceToken);

            if (device == null)
            {
                return NotFound(new { message = "Device not found" });
            }

            // Soft delete - just mark as inactive
            device.IsActive = false;
            device.LastUpdatedAt = DateTimeOffset.Now;
            _context.DeviceRegistrations.Update(device);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Device unregistered successfully" });
        }

        /// <summary>
        /// Get all registered devices for the current user
        /// </summary>
        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            var devices = await _context.DeviceRegistrations
                .Where(d => d.UserId == userId && d.IsActive)
                .OrderByDescending(d => d.LastUpdatedAt)
                .Select(d => new
                {
                    d.Id,
                    d.Platform,
                    d.DeviceName,
                    d.OsVersion,
                    d.AppVersion,
                    d.CreatedAt,
                    d.LastUpdatedAt,
                    d.LastNotificationSentAt
                })
                .ToListAsync();

            return Ok(devices);
        }

        /// <summary>
        /// Delete a specific device registration
        /// </summary>
        /// <param name="deviceId">Device ID to delete</param>
        [HttpDelete("devices/{deviceId}")]
        public async Task<IActionResult> DeleteDevice(long deviceId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user authentication" });
            }

            var device = await _context.DeviceRegistrations
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);

            if (device == null)
            {
                return NotFound(new { message = "Device not found" });
            }

            _context.DeviceRegistrations.Remove(device);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Device deleted successfully" });
        }
    }
}

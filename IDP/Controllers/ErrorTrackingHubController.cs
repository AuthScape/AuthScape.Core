using System;
using System.Threading.Tasks;
using AuthScape.ErrorTracking.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace IDP.Controllers
{
    /// <summary>
    /// Controller to receive error notifications from the API and broadcast them via SignalR.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ErrorTrackingHubController : ControllerBase
    {
        private readonly IHubContext<ErrorTrackingHub> _hubContext;
        private readonly ILogger<ErrorTrackingHubController> _logger;

        public ErrorTrackingHubController(IHubContext<ErrorTrackingHub> hubContext, ILogger<ErrorTrackingHubController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Called by the API when a new error is logged. Broadcasts the error to all connected clients.
        /// </summary>
        [HttpPost("NotifyNewError")]
        public async Task<IActionResult> NotifyNewError([FromBody] ErrorNotification notification)
        {
            _logger.LogInformation("ErrorTrackingHubController: Received notification for error {ErrorId} (StatusCode: {StatusCode})",
                notification.Id, notification.StatusCode);

            await _hubContext.Clients.Group("error_tracking").SendAsync("NewError", notification);
            _logger.LogInformation("ErrorTrackingHubController: Broadcasted 'NewError' to 'error_tracking' group");

            if (notification.ErrorGroupId.HasValue)
            {
                await _hubContext.Clients.Group($"error_group_{notification.ErrorGroupId}").SendAsync("NewOccurrence", notification);
                _logger.LogInformation("ErrorTrackingHubController: Broadcasted 'NewOccurrence' to error group {ErrorGroupId}", notification.ErrorGroupId);
            }

            return Ok();
        }
    }

    public class ErrorNotification
    {
        public Guid Id { get; set; }
        public Guid? ErrorGroupId { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorType { get; set; }
        public int StatusCode { get; set; }
        public string? Endpoint { get; set; }
        public string? HttpMethod { get; set; }
        public string? Browser { get; set; }
        public string? OperatingSystem { get; set; }
        public string? Source { get; set; }
        public bool IsResolved { get; set; }
        public DateTimeOffset Created { get; set; }
    }
}

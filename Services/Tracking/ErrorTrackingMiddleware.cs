using AuthScape.Models.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;

namespace Services.Tracking
{
    public class ErrorTrackingMiddleware
    {
        private readonly RequestDelegate next;

        public ErrorTrackingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context /* other dependencies */)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                sw.Stop();
                await HandleExceptionAsync(context, ex, sw.ElapsedMilliseconds);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex, long responseTimeMs)
        {
            var code = HttpStatusCode.InternalServerError; // 500 if unexpected

            if (ex is NotFoundException)
            {
                code = HttpStatusCode.NotFound;
            }
            else if (ex is UnauthorizedException)
            {
                code = HttpStatusCode.Unauthorized;
            }
            else if (ex is BadRequestException)
            {
                code = HttpStatusCode.BadRequest;
            }

            // Set status code before logging
            context.Response.StatusCode = (int)code;

            // Try to get error tracking service if registered (optional dependency)
            try
            {
                // Use reflection to avoid compile-time dependency
                var errorTrackingServiceType = Type.GetType("AuthScape.ErrorTracking.Services.IErrorTrackingService, AuthScape.ErrorTracking");
                if (errorTrackingServiceType != null)
                {
                    var errorTrackingService = context.RequestServices.GetService(errorTrackingServiceType);
                    if (errorTrackingService != null)
                    {
                        // Get ErrorSource enum value
                        var errorSourceType = Type.GetType("AuthScape.Models.ErrorTracking.ErrorSource, AuthScape.Models");
                        var source = DetermineErrorSource(context, errorSourceType);

                        // Get analytics session ID if available
                        Guid? sessionId = null;
                        if (context.Request.Cookies.TryGetValue("analyticsSessionId", out var sessionIdStr))
                        {
                            Guid.TryParse(sessionIdStr, out var parsedSessionId);
                            sessionId = parsedSessionId;
                        }

                        // Call LogError method via reflection
                        var logErrorMethod = errorTrackingServiceType.GetMethod("LogError");
                        if (logErrorMethod != null)
                        {
                            var task = (Task)logErrorMethod.Invoke(errorTrackingService, new object[] { ex, context, source, responseTimeMs, sessionId });
                            await task;
                        }
                    }
                }
            }
            catch
            {
                // Don't let error tracking crash the application
            }

            var result = JsonConvert.SerializeObject(new
            {
                error = ex.Message
            });

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(result);
        }

        private object DetermineErrorSource(HttpContext context, Type errorSourceType)
        {
            // Check if request is from API or IDP based on path or host
            var path = context.Request.Path.ToString().ToLower();
            var host = context.Request.Host.ToString().ToLower();

            int sourceValue = 1; // Default to API

            if (path.StartsWith("/api/") || host.Contains("api."))
                sourceValue = 1; // API
            else if (path.StartsWith("/identity/") || path.StartsWith("/account/") || host.Contains("idp.") || host.Contains("identity."))
                sourceValue = 2; // IDP

            // Convert int to enum value
            return errorSourceType != null ? Enum.ToObject(errorSourceType, sourceValue) : sourceValue;
        }
    }
}

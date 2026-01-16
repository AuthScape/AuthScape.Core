using AuthScape.Models.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace Services.Tracking
{
    public class ErrorTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorTrackingMiddleware> _logger;

        // Cache reflection results for performance
        private static Type? _errorTrackingServiceType;
        private static Type? _errorSourceType;
        private static MethodInfo? _logErrorMethod;
        private static bool _reflectionInitialized;
        private static readonly object _lockObj = new();

        public ErrorTrackingMiddleware(RequestDelegate next, ILogger<ErrorTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await _next(context);
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
                code = HttpStatusCode.NotFound; // 404
            }
            else if (ex is UnauthorizedException)
            {
                code = HttpStatusCode.Unauthorized; // 401
            }
            else if (ex is BadRequestException)
            {
                code = HttpStatusCode.BadRequest; // 400
            }
            else if (ex is ForbiddenException)
            {
                code = HttpStatusCode.Forbidden; // 403
            }
            else if (ex is BadGatewayException)
            {
                code = HttpStatusCode.BadGateway; // 502
            }
            else if (ex is ServiceUnavailableException)
            {
                code = HttpStatusCode.ServiceUnavailable; // 503
            }
            else if (ex is NotImplementedHttpException)
            {
                code = HttpStatusCode.NotImplemented; // 501
            }
            else if (ex is GatewayTimeoutException)
            {
                code = HttpStatusCode.GatewayTimeout; // 504
            }
            else if (ex is TooManyRequestsException)
            {
                code = HttpStatusCode.TooManyRequests; // 429
            }
            else if (ex is UnprocessableEntityException)
            {
                code = HttpStatusCode.UnprocessableEntity; // 422
            }
            else if (ex is ConflictException)
            {
                code = HttpStatusCode.Conflict; // 409
            }
            else if (ex is RequestTimeoutException)
            {
                code = HttpStatusCode.RequestTimeout; // 408
            }
            else if (ex is MethodNotAllowedException)
            {
                code = HttpStatusCode.MethodNotAllowed; // 405
            }
            else if (ex is GoneException)
            {
                code = HttpStatusCode.Gone; // 410
            }

            // Set status code before logging
            context.Response.StatusCode = (int)code;

            // Try to log to error tracking service using reflection
            await TryLogErrorAsync(context, ex, responseTimeMs);

            var result = JsonConvert.SerializeObject(new
            {
                error = ex.Message,
                statusCode = (int)code,
                traceId = context.TraceIdentifier
            });

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(result);
        }

        private async Task TryLogErrorAsync(HttpContext context, Exception ex, long responseTimeMs)
        {
            try
            {
                InitializeReflection();

                if (_errorTrackingServiceType == null || _logErrorMethod == null)
                {
                    _logger.LogWarning("ErrorTracking: Service type or method not found via reflection. Type: {Type}, Method: {Method}",
                        _errorTrackingServiceType?.Name ?? "null", _logErrorMethod?.Name ?? "null");
                    return;
                }

                var errorTrackingService = context.RequestServices.GetService(_errorTrackingServiceType);
                if (errorTrackingService == null)
                {
                    _logger.LogWarning("ErrorTracking: IErrorTrackingService not registered in DI container");
                    return;
                }

                // Determine error source
                var source = GetErrorSource(context);

                // Get analytics session ID if available
                Guid? sessionId = null;
                if (context.Request.Cookies.TryGetValue("analyticsSessionId", out var sessionIdStr))
                {
                    if (Guid.TryParse(sessionIdStr, out var parsedSessionId))
                    {
                        sessionId = parsedSessionId;
                    }
                }

                _logger.LogInformation("ErrorTracking: Logging error {ExceptionType} with status {StatusCode}",
                    ex.GetType().Name, context.Response.StatusCode);

                // Call: Task<Guid> LogError(Exception exception, HttpContext httpContext, ErrorSource source, long? responseTimeMs = null, Guid? sessionId = null)
                var result = _logErrorMethod.Invoke(errorTrackingService, new object?[] { ex, context, source, responseTimeMs, sessionId });

                if (result is Task<Guid> guidTask)
                {
                    var errorId = await guidTask;
                    _logger.LogInformation("ErrorTracking: Error logged successfully with ID {ErrorId}", errorId);
                }
                else if (result is Task task)
                {
                    await task;
                    _logger.LogInformation("ErrorTracking: Error logged successfully");
                }
            }
            catch (Exception logEx)
            {
                // Don't let error tracking crash the application
                _logger.LogError(logEx, "ErrorTracking: Failed to log error to tracking system");
            }
        }

        private static void InitializeReflection()
        {
            if (_reflectionInitialized)
            {
                return;
            }

            lock (_lockObj)
            {
                if (_reflectionInitialized)
                {
                    return;
                }

                try
                {
                    // Find the ErrorTracking assembly
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var errorTrackingAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "AuthScape.ErrorTracking");

                    if (errorTrackingAssembly != null)
                    {
                        _errorTrackingServiceType = errorTrackingAssembly.GetType("AuthScape.ErrorTracking.Services.IErrorTrackingService");
                        _logErrorMethod = _errorTrackingServiceType?.GetMethod("LogError");
                    }

                    // Find ErrorSource enum in AuthScape.Models
                    var modelsAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "AuthScape.Models");
                    if (modelsAssembly != null)
                    {
                        _errorSourceType = modelsAssembly.GetType("AuthScape.Models.ErrorTracking.ErrorSource");
                    }
                }
                catch
                {
                    // Ignore reflection errors
                }

                _reflectionInitialized = true;
            }
        }

        private static object GetErrorSource(HttpContext context)
        {
            var path = context.Request.Path.ToString().ToLower();
            var host = context.Request.Host.ToString().ToLower();

            // API = 1, IDP = 2
            int sourceValue = 1;

            if (path.StartsWith("/identity/") || path.StartsWith("/account/") ||
                host.Contains("idp.") || host.Contains("identity."))
            {
                sourceValue = 2; // IDP
            }

            if (_errorSourceType != null)
            {
                return Enum.ToObject(_errorSourceType, sourceValue);
            }

            return sourceValue;
        }
    }
}

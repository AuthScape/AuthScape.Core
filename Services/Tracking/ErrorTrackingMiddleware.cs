using AuthScape.Models.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace Services.Tracking
{
    public class ErrorTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorTrackingMiddleware> _logger;
        private readonly IConfiguration _configuration;

        // Cache reflection results for performance (used when ErrorTracking is available locally - IDP)
        private static Type? _errorTrackingServiceType;
        private static Type? _errorSourceType;
        private static MethodInfo? _logErrorMethod;
        private static MethodInfo? _notifyTimingMethod;
        private static bool _reflectionInitialized;
        private static readonly object _lockObj = new();

        // Dev-only speed diagnostics: resolved once (env + config never change at runtime).
        private bool? _speedDiagnosticsEnabled;

        public ErrorTrackingMiddleware(RequestDelegate next, ILogger<ErrorTrackingMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await _next(context);
                sw.Stop();
                await TryReportTimingAsync(context, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                await HandleExceptionAsync(context, ex, sw.ElapsedMilliseconds);
                await TryReportTimingAsync(context, sw.ElapsedMilliseconds);
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

            // Try to log to error tracking service
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
                // First, try to use local ErrorTracking service (available in IDP)
                if (await TryLogErrorLocallyAsync(context, ex, responseTimeMs))
                {
                    return;
                }

                // If local service not available (API), send to IDP via HTTP
                await TryLogErrorToIdpAsync(context, ex, responseTimeMs);
            }
            catch (Exception logEx)
            {
                // Don't let error tracking crash the application
                _logger.LogError(logEx, "ErrorTracking: Failed to log error to tracking system");
            }
        }

        private async Task<bool> TryLogErrorLocallyAsync(HttpContext context, Exception ex, long responseTimeMs)
        {
            try
            {
                InitializeReflection();

                if (_errorTrackingServiceType == null || _logErrorMethod == null)
                {
                    return false;
                }

                var errorTrackingService = context.RequestServices.GetService(_errorTrackingServiceType);
                if (errorTrackingService == null)
                {
                    return false;
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

                _logger.LogInformation("ErrorTracking: Logging error {ExceptionType} with status {StatusCode} locally",
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

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task TryLogErrorToIdpAsync(HttpContext context, Exception ex, long responseTimeMs)
        {
            try
            {
                var idpUrl = _configuration["IDPUrl"];
                if (string.IsNullOrEmpty(idpUrl))
                {
                    _logger.LogWarning("ErrorTracking: IDPUrl not configured, cannot send error to IDP");
                    return;
                }

                var httpClientFactory = context.RequestServices.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
                if (httpClientFactory == null)
                {
                    _logger.LogWarning("ErrorTracking: IHttpClientFactory not registered");
                    return;
                }

                var client = httpClientFactory.CreateClient();

                // Get analytics session ID if available
                Guid? sessionId = null;
                if (context.Request.Cookies.TryGetValue("analyticsSessionId", out var sessionIdStr))
                {
                    if (Guid.TryParse(sessionIdStr, out var parsedSessionId))
                    {
                        sessionId = parsedSessionId;
                    }
                }

                // Determine error source (API = 1)
                var source = IsIdpRequest(context) ? 2 : 1;

                // Build error data for IDP endpoint
                var errorData = new
                {
                    message = ex.Message,
                    errorType = ex.GetType().Name,
                    stackTrace = ex.StackTrace,
                    url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}",
                    statusCode = context.Response.StatusCode,
                    source = source,
                    responseTimeMs = responseTimeMs,
                    sessionId = sessionId,
                    userAgent = context.Request.Headers["User-Agent"].ToString(),
                    ipAddress = context.Connection.RemoteIpAddress?.ToString(),
                    endpoint = context.Request.Path.ToString(),
                    method = context.Request.Method
                };

                var json = JsonConvert.SerializeObject(errorData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("ErrorTracking: Sending error {ExceptionType} to IDP at {IdpUrl}",
                    ex.GetType().Name, idpUrl);

                var response = await client.PostAsync($"{idpUrl}/api/ErrorTrackingHub/LogErrorFromApi", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("ErrorTracking: Error sent to IDP successfully");
                }
                else
                {
                    _logger.LogWarning("ErrorTracking: Failed to send error to IDP, status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception httpEx)
            {
                _logger.LogError(httpEx, "ErrorTracking: Failed to send error to IDP via HTTP");
            }
        }

        /// <summary>
        /// Dev-only speed diagnostics. Broadcasts this request's timing to the live
        /// `speed_diagnostics` SignalR group so the VS Code Speed Diagnostics panel can show the
        /// speed of every API call. No-op unless ASPNETCORE_ENVIRONMENT=Development and
        /// SpeedDiagnostics:Enabled=true. Never adds latency or throws into the request pipeline.
        /// </summary>
        private Task TryReportTimingAsync(HttpContext context, long responseTimeMs)
        {
            try
            {
                if (!IsSpeedDiagnosticsEnabled() || ShouldSkipTiming(context))
                {
                    return Task.CompletedTask;
                }

                var httpMethod = context.Request.Method;
                var endpoint = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
                var statusCode = context.Response.StatusCode;
                var source = IsIdpRequest(context) ? 2 : 1; // 1 = API, 2 = IDP
                var machineName = Environment.MachineName ?? string.Empty;

                // Prefer the in-process hub (IDP hosts ErrorTrackingService). The in-process
                // broadcast is cheap, so we await it.
                var localService = TryGetLocalErrorTrackingService(context);
                if (localService != null)
                {
                    return InvokeLocalNotifyTiming(localService, httpMethod, endpoint, statusCode, responseTimeMs, source, machineName);
                }

                // Otherwise (API process) forward to the IDP over HTTP, fire-and-forget so we
                // never add a network round-trip to the response time of every request.
                var idpUrl = GetIdpUrl();
                var httpClientFactory = context.RequestServices.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
                if (!string.IsNullOrEmpty(idpUrl) && httpClientFactory != null)
                {
                    _ = ReportTimingToIdpAsync(httpClientFactory, idpUrl!, httpMethod, endpoint, statusCode, responseTimeMs, source, machineName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SpeedDiagnostics: failed to report timing");
            }

            return Task.CompletedTask;
        }

        private bool IsSpeedDiagnosticsEnabled()
        {
            if (_speedDiagnosticsEnabled.HasValue)
            {
                return _speedDiagnosticsEnabled.Value;
            }

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var isDev = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
            var configValue = _configuration["SpeedDiagnostics:Enabled"];
            var enabled = isDev && string.Equals(configValue, "true", StringComparison.OrdinalIgnoreCase);

            _speedDiagnosticsEnabled = enabled;
            return enabled;
        }

        private string? GetIdpUrl()
        {
            // The error path reads top-level "IDPUrl"; the seeded appsettings nest it under
            // "AppSettings". Accept either so the API can actually reach the IDP.
            return _configuration["IDPUrl"] ?? _configuration["AppSettings:IDPUrl"];
        }

        private static bool ShouldSkipTiming(HttpContext context)
        {
            var path = context.Request.Path.HasValue
                ? context.Request.Path.Value!.ToLowerInvariant()
                : string.Empty;

            // Don't time the plumbing that delivers diagnostics (prevents a feedback loop),
            // SignalR transports, or static asset requests.
            if (path.StartsWith("/errortracking") ||
                path.StartsWith("/notifications") ||
                path.Contains("/api/errortrackinghub") ||
                path.Contains("/negotiate"))
            {
                return true;
            }

            var lastDot = path.LastIndexOf('.');
            if (lastDot > path.LastIndexOf('/'))
            {
                switch (path.Substring(lastDot))
                {
                    case ".js":
                    case ".css":
                    case ".map":
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".gif":
                    case ".svg":
                    case ".ico":
                    case ".woff":
                    case ".woff2":
                    case ".ttf":
                        return true;
                }
            }

            return false;
        }

        private object? TryGetLocalErrorTrackingService(HttpContext context)
        {
            InitializeReflection();

            if (_errorTrackingServiceType == null || _notifyTimingMethod == null)
            {
                return null;
            }

            return context.RequestServices.GetService(_errorTrackingServiceType);
        }

        private async Task InvokeLocalNotifyTiming(object service, string httpMethod, string endpoint, int statusCode, long responseTimeMs, int source, string machineName)
        {
            try
            {
                // Task NotifyTiming(string httpMethod, string endpoint, int statusCode, long responseTimeMs, int source, string machineName)
                var result = _notifyTimingMethod!.Invoke(service, new object?[] { httpMethod, endpoint, statusCode, responseTimeMs, source, machineName });
                if (result is Task task)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SpeedDiagnostics: local NotifyTiming failed");
            }
        }

        private async Task ReportTimingToIdpAsync(IHttpClientFactory httpClientFactory, string idpUrl, string httpMethod, string endpoint, int statusCode, long responseTimeMs, int source, string machineName)
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                var payload = new
                {
                    httpMethod,
                    endpoint,
                    statusCode,
                    responseTimeMs,
                    source,
                    machineName
                };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync($"{idpUrl.TrimEnd('/')}/api/ErrorTrackingHub/LogTimingFromApi", content);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SpeedDiagnostics: failed to forward timing to IDP");
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
                    // Find the AuthScape.IDP assembly (ErrorTracking is now part of IDP)
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var idpAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "AuthScape.IDP");

                    if (idpAssembly != null)
                    {
                        _errorTrackingServiceType = idpAssembly.GetType("AuthScape.IDP.Services.ErrorTracking.IErrorTrackingService");
                        _logErrorMethod = _errorTrackingServiceType?.GetMethod("LogError");
                        _notifyTimingMethod = _errorTrackingServiceType?.GetMethod("NotifyTiming");
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
            // API = 1, IDP = 2
            int sourceValue = IsIdpRequest(context) ? 2 : 1;

            if (_errorSourceType != null)
            {
                return Enum.ToObject(_errorSourceType, sourceValue);
            }

            return sourceValue;
        }

        private static bool IsIdpRequest(HttpContext context)
        {
            var path = context.Request.Path.ToString().ToLower();
            var host = context.Request.Host.ToString().ToLower();

            return path.StartsWith("/identity/") || path.StartsWith("/account/") ||
                   host.Contains("idp.") || host.Contains("identity.");
        }
    }
}

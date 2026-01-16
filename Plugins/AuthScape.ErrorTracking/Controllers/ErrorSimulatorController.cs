using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AuthScape.Models.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthScape.ErrorTracking.Controllers;

/// <summary>
/// Controller for simulating various error conditions for testing purposes.
/// Throws real exceptions that are caught by ErrorTrackingMiddleware.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class ErrorSimulatorController : ControllerBase
{
    private readonly ILogger<ErrorSimulatorController> _logger;

    public ErrorSimulatorController(ILogger<ErrorSimulatorController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Throws an exception that maps to the specified HTTP status code.
    /// The ErrorTrackingMiddleware will catch this and log it.
    /// </summary>
    [HttpPost("SimulateHttpError")]
    public IActionResult SimulateHttpError([FromBody] SimulateHttpErrorRequest request)
    {
        _logger.LogWarning("SimulateHttpError received: StatusCode={StatusCode}, Message={Message}", request.StatusCode, request.Message);

        var errorMessage = request.Message ?? GetDefaultMessage(request.StatusCode);
        var code = request.StatusCode;

        // Throw real exceptions that ErrorTrackingMiddleware will catch
        Exception exceptionToThrow = code switch
        {
            400 => new BadRequestException(errorMessage),
            401 => new UnauthorizedException(errorMessage),
            403 => new ForbiddenException(errorMessage),
            404 => new NotFoundException(errorMessage),
            405 => new MethodNotAllowedException(errorMessage),
            408 => new RequestTimeoutException(errorMessage),
            409 => new ConflictException(errorMessage),
            410 => new GoneException(errorMessage),
            422 => new UnprocessableEntityException(errorMessage),
            429 => new TooManyRequestsException(errorMessage),
            500 => new Exception(errorMessage),
            501 => new NotImplementedHttpException(errorMessage),
            502 => new BadGatewayException(errorMessage),
            503 => new ServiceUnavailableException(errorMessage),
            504 => new GatewayTimeoutException(errorMessage),
            _ => new Exception(errorMessage)
        };

        _logger.LogWarning("Throwing exception type: {ExceptionType} for status code {StatusCode}", exceptionToThrow.GetType().FullName, code);
        throw exceptionToThrow;
    }

    /// <summary>
    /// Throws a specific type of exception.
    /// The ErrorTrackingMiddleware will catch this and log it.
    /// </summary>
    [HttpPost("SimulateException")]
    public async Task<IActionResult> SimulateException([FromBody] SimulateExceptionRequest request)
    {
        _logger.LogWarning("Simulating {ExceptionType} exception: {Message}", request.ExceptionType, request.Message);

        // Add a small delay for timeout simulation
        if (request.ExceptionType == "Timeout")
        {
            await Task.Delay(100);
        }

        // Throw real exceptions - ErrorTrackingMiddleware will catch and log them
        throw request.ExceptionType switch
        {
            "NullReference" => new NullReferenceException(request.Message ?? "Simulated null reference exception"),
            "ArgumentNull" => new ArgumentNullException("parameter", request.Message ?? "Simulated argument null exception"),
            "InvalidOperation" => new InvalidOperationException(request.Message ?? "Simulated invalid operation exception"),
            "Timeout" => new TimeoutException(request.Message ?? "Simulated timeout exception"),
            "NotFound" => new NotFoundException(request.Message ?? "Simulated entity not found"),
            "Unauthorized" => new UnauthorizedException(request.Message ?? "Simulated unauthorized access"),
            "Forbidden" => new ForbiddenException(request.Message ?? "Simulated forbidden access"),
            "BadRequest" => new BadRequestException(request.Message ?? "Simulated bad request"),
            "BadGateway" => new BadGatewayException(request.Message ?? "Simulated bad gateway"),
            "ServiceUnavailable" => new ServiceUnavailableException(request.Message ?? "Simulated service unavailable"),
            "KeyNotFound" => new KeyNotFoundException(request.Message ?? "Simulated key not found"),
            "ArgumentOutOfRange" => new ArgumentOutOfRangeException("value", request.Message ?? "Simulated argument out of range"),
            "NotSupported" => new NotSupportedException(request.Message ?? "Simulated not supported operation"),
            "NotImplemented" => new NotImplementedException(request.Message ?? "Simulated not implemented"),
            "DivideByZero" => new DivideByZeroException(request.Message ?? "Simulated divide by zero"),
            "Overflow" => new OverflowException(request.Message ?? "Simulated overflow"),
            "Format" => new FormatException(request.Message ?? "Simulated format exception"),
            "IO" => new System.IO.IOException(request.Message ?? "Simulated IO exception"),
            _ => new Exception(request.Message ?? $"Simulated {request.ExceptionType} exception")
        };
    }

    /// <summary>
    /// Simulates a slow endpoint (for timeout testing).
    /// </summary>
    [HttpGet("SimulateSlow")]
    public async Task<IActionResult> SimulateSlow([FromQuery] int delayMs = 5000)
    {
        _logger.LogInformation("Simulating slow response with {DelayMs}ms delay", delayMs);

        var actualDelay = Math.Min(delayMs, 30000);
        var sw = Stopwatch.StartNew();
        await Task.Delay(actualDelay);
        sw.Stop();

        return Ok(new { success = true, message = $"Response after {sw.ElapsedMilliseconds}ms delay" });
    }

    /// <summary>
    /// Simulates a successful response (for comparison/health check).
    /// </summary>
    [HttpGet("SimulateSuccess")]
    public IActionResult SimulateSuccess()
    {
        return Ok(new
        {
            success = true,
            message = "This is a successful response",
            timestamp = DateTimeOffset.UtcNow,
            data = new { id = 1, name = "Test Item" }
        });
    }

    /// <summary>
    /// Simulates intermittent failures (throws exception randomly ~50% of the time).
    /// </summary>
    [HttpGet("SimulateIntermittent")]
    public IActionResult SimulateIntermittent()
    {
        var random = new Random();
        if (random.NextDouble() < 0.5)
        {
            _logger.LogWarning("Intermittent failure triggered");
            throw new Exception("Intermittent failure - try again");
        }

        return Ok(new { success = true, message = "Request succeeded this time" });
    }

    private static string GetDefaultMessage(int statusCode)
    {
        return statusCode switch
        {
            400 => "The request was invalid or cannot be processed",
            401 => "Authentication is required to access this resource",
            403 => "You do not have permission to access this resource",
            404 => "The requested resource was not found",
            405 => "The HTTP method is not allowed for this resource",
            408 => "The request timed out",
            409 => "The request conflicts with the current state of the resource",
            410 => "The requested resource is no longer available",
            422 => "The request was well-formed but contains semantic errors",
            429 => "Too many requests - please try again later",
            500 => "An unexpected error occurred on the server",
            501 => "The requested functionality is not implemented",
            502 => "Bad gateway - invalid response from upstream server",
            503 => "Service temporarily unavailable - please try again later",
            504 => "Gateway timeout - upstream server did not respond in time",
            _ => $"An error occurred with status code {statusCode}"
        };
    }
}

/// <summary>
/// Request model for simulating HTTP errors.
/// </summary>
public class SimulateHttpErrorRequest
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; } = 500;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Request model for simulating exceptions.
/// </summary>
public class SimulateExceptionRequest
{
    [JsonPropertyName("exceptionType")]
    public string ExceptionType { get; set; } = "Exception";

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

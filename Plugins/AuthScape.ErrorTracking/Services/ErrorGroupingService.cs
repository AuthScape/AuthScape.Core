using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AuthScape.ErrorTracking.Services;

/// <summary>
/// Service responsible for generating error signatures for grouping similar errors together.
/// Uses SHA256 hash of (ErrorType + FirstMeaningfulStackLine + Endpoint) for deduplication.
/// </summary>
public class ErrorGroupingService : IErrorGroupingService
{
    /// <summary>
    /// Generates a unique signature for an error to enable grouping of similar errors.
    /// The signature is a SHA256 hash of the error type, first meaningful stack trace line, and endpoint.
    /// </summary>
    /// <param name="errorType">The type of exception (e.g., "NullReferenceException")</param>
    /// <param name="stackTrace">The full stack trace of the error</param>
    /// <param name="endpoint">The API endpoint or page where the error occurred</param>
    /// <returns>A 64-character SHA256 hash used as the error signature</returns>
    public string GenerateErrorSignature(string errorType, string stackTrace, string endpoint)
    {
        // Extract the first meaningful line from the stack trace
        var firstMeaningfulLine = ExtractFirstMeaningfulStackLine(stackTrace);

        // Combine the error type, first meaningful stack line, and endpoint
        var signatureInput = $"{errorType}|{firstMeaningfulLine}|{endpoint}";

        // Generate SHA256 hash
        return ComputeSha256Hash(signatureInput);
    }

    /// <summary>
    /// Extracts the first meaningful line from a stack trace.
    /// Skips framework/infrastructure lines and focuses on application code.
    /// </summary>
    private string ExtractFirstMeaningfulStackLine(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return string.Empty;

        // Split stack trace into lines
        var lines = stackTrace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        // Patterns to skip (framework/infrastructure code)
        var skipPatterns = new[]
        {
            "System.",
            "Microsoft.",
            "at lambda_method",
            "at async",
            "--- End of stack trace",
            "at System.Runtime"
        };

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // Skip lines matching skip patterns
            if (skipPatterns.Any(pattern => trimmedLine.Contains(pattern)))
                continue;

            // Extract just the method signature (remove file path and line number)
            // Example: "at MyNamespace.MyClass.MyMethod(String arg) in C:\path\to\file.cs:line 42"
            // becomes: "at MyNamespace.MyClass.MyMethod(String arg)"
            var methodSignature = Regex.Replace(trimmedLine, @" in .*?:\d+$", string.Empty);

            return methodSignature;
        }

        // If no meaningful line found, return the first non-empty line
        return lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Computes a SHA256 hash of the input string.
    /// </summary>
    private string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);

        // Convert to hex string
        var builder = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Extracts a concise error message from an exception, limiting length.
    /// </summary>
    /// <param name="errorMessage">The full error message</param>
    /// <param name="maxLength">Maximum length (default 1000 chars)</param>
    /// <returns>Truncated error message</returns>
    public string GetConciseErrorMessage(string errorMessage, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return string.Empty;

        if (errorMessage.Length <= maxLength)
            return errorMessage;

        return errorMessage.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Determines if two errors should be grouped together based on their signatures.
    /// </summary>
    public bool ShouldGroupErrors(string signature1, string signature2)
    {
        return !string.IsNullOrEmpty(signature1) &&
               !string.IsNullOrEmpty(signature2) &&
               signature1.Equals(signature2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the error type name from an exception type (removes namespace).
    /// </summary>
    /// <param name="fullTypeName">Full type name with namespace</param>
    /// <returns>Simple type name</returns>
    public string GetSimpleErrorType(string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullTypeName))
            return "UnknownError";

        // Extract just the class name (remove namespace)
        var parts = fullTypeName.Split('.');
        return parts.LastOrDefault() ?? fullTypeName;
    }
}

public interface IErrorGroupingService
{
    string GenerateErrorSignature(string errorType, string stackTrace, string endpoint);
    string GetConciseErrorMessage(string errorMessage, int maxLength = 1000);
    bool ShouldGroupErrors(string signature1, string signature2);
    string GetSimpleErrorType(string fullTypeName);
}

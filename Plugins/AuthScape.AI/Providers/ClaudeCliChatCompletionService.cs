using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using AuthScape.AI.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AuthScape.AI.Providers;

/// <summary>
/// Semantic Kernel IChatCompletionService implementation that delegates to the Claude CLI.
/// Spawns "claude -p --output-format text" and pipes the prompt via stdin.
/// </summary>
public class ClaudeCliChatCompletionService : IChatCompletionService
{
    private readonly ClaudeCliOptions _options;

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();

    public ClaudeCliChatCompletionService(ClaudeCliOptions options)
    {
        _options = options;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPromptFromHistory(chatHistory);
        var response = await RunClaudeCliAsync(prompt, cancellationToken);

        return new List<ChatMessageContent>
        {
            new(AuthorRole.Assistant, response)
        };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Claude CLI doesn't support streaming natively in print mode,
        // so we run the full command and yield the result as a single chunk.
        var prompt = BuildPromptFromHistory(chatHistory);
        var response = await RunClaudeCliAsync(prompt, cancellationToken);

        yield return new StreamingChatMessageContent(AuthorRole.Assistant, response);
    }

    /// <summary>
    /// Flattens a ChatHistory into a single prompt string for the CLI.
    /// </summary>
    private static string BuildPromptFromHistory(ChatHistory chatHistory)
    {
        if (chatHistory.Count == 1)
            return chatHistory[0].Content ?? "";

        var sb = new StringBuilder();
        foreach (var message in chatHistory)
        {
            var role = message.Role == AuthorRole.System ? "System"
                     : message.Role == AuthorRole.User ? "User"
                     : "Assistant";
            sb.AppendLine($"{role}: {message.Content}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Spawns the Claude CLI process in non-interactive print mode.
    /// </summary>
    private async Task<string> RunClaudeCliAsync(string prompt, CancellationToken cancellationToken)
    {
        var args = "-p --output-format text";

        // If a specific model is configured, pass it via --model
        if (!string.IsNullOrWhiteSpace(_options.DefaultModel))
        {
            args += $" --model {_options.DefaultModel}";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.CliPath,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Write the prompt to stdin and close it so Claude knows input is complete
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        // Read output and error concurrently to avoid deadlocks
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Claude CLI exited with code {process.ExitCode}. Error: {error}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "Claude CLI returned empty output." +
                (string.IsNullOrWhiteSpace(error) ? "" : $" Stderr: {error}"));
        }

        return output;
    }
}

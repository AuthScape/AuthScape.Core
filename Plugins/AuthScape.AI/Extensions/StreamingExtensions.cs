using System.Text;
using AuthScape.AI.Models;

namespace AuthScape.AI.Extensions;

public static class StreamingExtensions
{
    /// <summary>
    /// Collects all streaming updates into a single concatenated string.
    /// </summary>
    public static async Task<string> ToStringAsync(
        this IAsyncEnumerable<StreamingChatUpdate> stream,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            if (update.Text is not null)
                sb.Append(update.Text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Collects all streaming updates into a list.
    /// </summary>
    public static async Task<List<StreamingChatUpdate>> CollectAsync(
        this IAsyncEnumerable<StreamingChatUpdate> stream,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<StreamingChatUpdate>();
        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            updates.Add(update);
        }
        return updates;
    }

    /// <summary>
    /// Streams updates and invokes a callback for each text chunk.
    /// </summary>
    public static async Task<string> StreamToAsync(
        this IAsyncEnumerable<StreamingChatUpdate> stream,
        Action<string> onChunk,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            if (update.Text is not null)
            {
                sb.Append(update.Text);
                onChunk(update.Text);
            }
        }
        return sb.ToString();
    }
}

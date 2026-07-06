using System.Text;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Discord rejects message content over 2,000 characters. Senders are expected to
///     compose within the cap; this is the transport-level backstop so an oversized
///     message degrades to a split delivery instead of a logged, silent drop.
/// </summary>
public static class DiscordMessageSplitter
{
    public const int MaxContentLength = 2000;

    public static IReadOnlyList<string> Split(string message)
    {
        if (message.Length <= MaxContentLength) return new[] { message };

        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var line in message.Split('\n'))
        {
            // Hard-wrap a pathological single line; otherwise this iterates once.
            var offset = 0;
            do
            {
                var segment = line.Substring(offset, Math.Min(MaxContentLength, line.Length - offset));
                var separatorLength = current.Length > 0 ? 1 : 0;
                if (current.Length + separatorLength + segment.Length > MaxContentLength)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    separatorLength = 0;
                }

                if (separatorLength > 0) current.Append('\n');
                current.Append(segment);
                offset += MaxContentLength;
            } while (offset < line.Length);
        }

        if (current.Length > 0) parts.Add(current.ToString());
        return parts;
    }
}

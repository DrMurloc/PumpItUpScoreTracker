using System.Text;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     What a failed run stores for the admin page: each exception's type and message, outermost
///     first, then only the stack frames from this codebase. The column holds 2,000 characters, and a
///     raw stack spends them on framework frames before it reaches the line that names the failing
///     query.
/// </summary>
internal static class FailureText
{
    public static string Of(Exception exception)
    {
        var text = new StringBuilder();
        for (var e = exception; e != null; e = e.InnerException)
            text.AppendLine($"{e.GetType().FullName}: {e.Message}");
        for (var e = exception; e != null; e = e.InnerException)
            foreach (var frame in (e.StackTrace ?? string.Empty).Split('\n'))
                if (frame.Contains(" ScoreTracker.", StringComparison.Ordinal))
                    text.AppendLine(frame.TrimEnd());
        return text.ToString().TrimEnd();
    }
}

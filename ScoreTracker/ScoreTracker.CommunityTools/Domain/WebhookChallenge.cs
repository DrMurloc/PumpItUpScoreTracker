using System.Security.Cryptography;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     The handshake that proves a maker controls the URL they typed.
///     <para>
///         We POST a random challenge and the endpoint echoes it back. Only then does the URL become
///         usable. This is the shape Slack, Stripe and GitHub all ship, so makers recognise it — and
///         it turns a typo from "a stranger receives a player's scores on a schedule" into "the save
///         failed".
///     </para>
///     <para>
///         It proves the endpoint cooperated with this maker's setup at this moment. It does not
///         prove domain ownership; that needs a DNS record or a well-known path, which is real
///         friction we have not bought yet.
///     </para>
/// </summary>
internal static class WebhookChallenge
{
    /// <summary>Named in the body so a handler can branch on it before looking for a delivery.</summary>
    public const string Type = "url_verification";

    /// <summary>
    ///     Short enough to eyeball in a log, long enough that guessing it is not a strategy. The
    ///     prefix is there so a maker who sees one in their logs can search for what it is.
    /// </summary>
    public static string Mint()
    {
        return "vfy_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    }

    /// <summary>
    ///     Whether a response body counts as the echo. Trimmed, because a handler that writes the
    ///     token with a trailing newline has done the thing we asked; and a JSON body carrying the
    ///     token is accepted too, because half of makers will reach for their framework's json helper
    ///     without thinking and being pedantic there buys nothing.
    /// </summary>
    public static bool Echoes(string? responseBody, string challenge)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return false;

        var trimmed = responseBody.Trim();
        return trimmed == challenge
               || trimmed == $"\"{challenge}\""
               || trimmed.Contains($"\"{challenge}\"", StringComparison.Ordinal);
    }
}

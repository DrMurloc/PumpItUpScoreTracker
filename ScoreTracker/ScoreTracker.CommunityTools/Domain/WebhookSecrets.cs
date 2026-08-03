using System.Security.Cryptography;
using System.Text;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     The two secrets a webhook uses, and the reason they can never be the same value.
///     <para>
///         They run in opposite directions. The <b>outbound header</b> is how a maker's server knows
///         a call is ours, so we hand it to them on every delivery — it has to be recoverable, which
///         means encrypted rather than hashed. The <b>verification secret</b> is how we know their
///         server is theirs, so it must never leave our side: we store only a hash and compare what
///         they send back.
///     </para>
///     <para>
///         Sharing one value between the two collapses the second into the first. Anyone who
///         receives a single delivery has read the header, and could then hand it back at
///         verification time as proof of an identity they do not hold — which is exactly the hole
///         echoing a challenge we sent them left open.
///     </para>
/// </summary>
internal static class WebhookSecrets
{
    /// <summary>
    ///     Named in the body so a handler can branch on it before looking for a delivery. The body
    ///     carries nothing else: a verification request that contained the answer would be proving
    ///     the endpoint can read, not that it knows anything.
    /// </summary>
    public const string VerificationType = "url_verification";

    /// <summary>
    ///     A plain SHA-256, for the same reason the API keys use one: we compare against a value the
    ///     maker holds, never derive one, and the input is high-entropy when generated. A maker who
    ///     types "hunter2" is not protected from someone who guesses it — but guessing is not a path
    ///     here, because only the tool's owner can trigger a verification attempt.
    /// </summary>
    public static string HashOf(string secret)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));
    }

    /// <summary>
    ///     Whether a response body carries the secret. Trimmed, because a handler that writes it
    ///     with a trailing newline has done the thing we asked; and a JSON body carrying it is
    ///     accepted too, because half of makers will reach for their framework's json helper without
    ///     thinking and being pedantic there buys nothing.
    ///     <para>
    ///         Comparison is fixed-time and happens on the hash, so a response that is wrong reveals
    ///         nothing about how nearly it was right.
    ///     </para>
    /// </summary>
    public static bool Answers(string? responseBody, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return false;

        foreach (var candidate in Candidates(responseBody.Trim()))
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(HashOf(candidate)),
                    Encoding.UTF8.GetBytes(expectedHash)))
                return true;

        return false;
    }

    /// <summary>
    ///     The readings of a response body that could be the secret: the whole body, the body with
    ///     its quotes stripped, and every JSON string value in it.
    /// </summary>
    private static IEnumerable<string> Candidates(string body)
    {
        yield return body;

        if (body.Length > 1 && body[0] == '"' && body[^1] == '"')
            yield return body[1..^1];

        // Deliberately a scan rather than a parse: the shape a maker returns is theirs to choose,
        // and "the secret appears as a JSON string somewhere in here" is the only rule we need.
        var start = -1;
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '"' || (i > 0 && body[i - 1] == '\\')) continue;

            if (start < 0) start = i + 1;
            else
            {
                yield return body[start..i];
                start = -1;
            }
        }
    }
}

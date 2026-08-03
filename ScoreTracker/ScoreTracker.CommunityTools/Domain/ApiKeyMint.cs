using System.Security.Cryptography;
using System.Text;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     Mints and verifies tool API keys.
///     <para>
///         Keys carry a <c>piu_scores_live_</c> prefix so secret scanners can recognise one in a
///         public repository, and are stored as a SHA-256 hash — which is what makes "shown once"
///         true rather than a UI convention. A plain SHA-256 is right here where it would be wrong
///         for a password: the input is 256 bits of our own randomness, so there is no dictionary to
///         run.
///     </para>
/// </summary>
internal static class ApiKeyMint
{
    public const string Prefix = "piu_scores_live_";

    /// <summary>
    ///     The prefix new keys used to carry. Still accepted, because the hash covers the whole
    ///     string: a key minted under the old prefix is a valid key, and rotating the prefix must
    ///     not silently 401 someone who is holding one.
    /// </summary>
    private const string LegacyPrefix = "pst_live_";

    /// <summary>The default life of a new key. "Never" is offered, warned about, and rare.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(182);

    /// <summary>How many keys a tool may have live at once, so rotation costs no downtime.</summary>
    public const int MaxActiveKeys = 2;

    public static (string Key, string Hash, string Last4) Mint()
    {
        var key = Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return (key, HashOf(key), key[^4..]);
    }

    public static string HashOf(string key)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    /// <summary>
    ///     Cheap enough to run before touching the database, so a malformed bearer token never
    ///     becomes a query.
    /// </summary>
    public static bool LooksLikeAKey(string? candidate)
    {
        return HasShape(candidate, Prefix) || HasShape(candidate, LegacyPrefix);
    }

    private static bool HasShape(string? candidate, string prefix)
    {
        return candidate is not null
               && candidate.StartsWith(prefix, StringComparison.Ordinal)
               && candidate.Length == prefix.Length + 64;
    }
}

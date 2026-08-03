using System.Buffers.Text;
using System.Text;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The opaque <c>cursor</c> value on api/v2 collections. Two payload shapes travel through one
///     codec (docs/design/api-v2-community-tools.md §4.1):
///     <list type="bullet">
///         <item>
///             an <b>offset</b>, for bounded catalog collections that change a few times a year —
///             a crawl racing a chart import is not a real scenario;
///         </item>
///         <item>
///             a <b>keyset</b> (the last row's sort key plus its id as tiebreaker), for player data,
///             where a row genuinely can be written mid-crawl and an offset would silently skip or
///             repeat it.
///         </item>
///     </list>
///     <para>
///         The token also carries a fingerprint of the filter set it was minted under. Replaying a
///         cursor against different filters is a caller bug that would otherwise return quietly
///         wrong rows; here it is a 400.
///     </para>
/// </summary>
internal readonly record struct ContinuationToken(int Offset, string? Key, Guid? Id, int Fingerprint)
{
    private const char Separator = '|';

    public static ContinuationToken FromOffset(int offset, int fingerprint)
    {
        return new ContinuationToken(offset, null, null, fingerprint);
    }

    public static ContinuationToken FromKeyset(string key, Guid id, int fingerprint)
    {
        return new ContinuationToken(0, key, id, fingerprint);
    }

    /// <summary>
    ///     Base64url so the token survives a query string without escaping. Deliberately not JSON:
    ///     a shorter token is a smaller temptation to hand-craft one, and the shape is ours to
    ///     change.
    /// </summary>
    public string Encode()
    {
        var raw = string.Join(Separator, Offset.ToString(), Key ?? string.Empty,
            Id?.ToString() ?? string.Empty, Fingerprint.ToString());
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? encoded, int expectedFingerprint, out ContinuationToken token)
    {
        token = default;
        if (string.IsNullOrWhiteSpace(encoded)) return false;

        string raw;
        try
        {
            raw = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = raw.Split(Separator);
        if (parts.Length != 4) return false;
        if (!int.TryParse(parts[0], out var offset)) return false;
        if (!int.TryParse(parts[3], out var fingerprint)) return false;
        if (fingerprint != expectedFingerprint) return false;

        Guid? id = Guid.TryParse(parts[2], out var parsedId) ? parsedId : null;
        token = new ContinuationToken(offset, parts[1].Length == 0 ? null : parts[1], id, fingerprint);
        return true;
    }

    /// <summary>
    ///     Order-sensitive so that reordering filters mints a new fingerprint — two filter sets that
    ///     differ only in argument order describe the same query, but a caller who changed one
    ///     changed their intent, and the cheap answer is to make them re-page.
    /// </summary>
    public static int FingerprintOf(params object?[] filters)
    {
        var hash = new HashCode();
        foreach (var filter in filters) hash.Add(filter?.ToString() ?? "\0");
        return hash.ToHashCode();
    }
}

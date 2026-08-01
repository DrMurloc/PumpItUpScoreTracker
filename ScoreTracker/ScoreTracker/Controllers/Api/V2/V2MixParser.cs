using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     Parses the Mix parameter on api/v2. Unlike <see cref="ApiMixParser" /> on v1, mix is
///     <b>required</b> and every mix is accepted — a legacy-mix caller gets legacy-mix data
///     (docs/design/api-v2-community-tools.md §3).
///     <para>
///         v1 defaults to Phoenix forever because it promised byte-identical responses to
///         integrations that predate Phoenix 2. A new version inherits the promise but not the
///         default: a silent default rots the day a mix is added, and the caller who omitted the
///         parameter is the one who cannot tell.
///     </para>
/// </summary>
internal static class V2MixParser
{
    /// <summary>Enum names, not display names — "Phoenix2", never "Phoenix 2".</summary>
    private static readonly string[] Names = Enum.GetNames<MixEnum>();

    public static string ValidValues => string.Join(", ", Names);

    /// <summary>
    ///     Case-insensitive over the enum names. Null or blank is a failure rather than a default,
    ///     and numeric input is rejected: "7" parsing to whichever member happens to sit at 7 is a
    ///     silent wrong answer, and the enum is append-only precisely because those positions move.
    /// </summary>
    public static bool TryParse(string? raw, out MixEnum mix)
    {
        mix = MixEnum.Phoenix;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var trimmed = raw.Trim();
        if (trimmed.Length > 0 && char.IsDigit(trimmed[0])) return false;

        return Enum.TryParse(trimmed, true, out mix) && Enum.IsDefined(mix);
    }
}

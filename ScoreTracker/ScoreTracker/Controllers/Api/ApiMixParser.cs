using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Controllers.Api;

/// <summary>
///     Parses the optional Mix parameter on the public api/* surface. Locked decision
///     (docs/design/phoenix2-implementation.md): the API defaults to Phoenix permanently — never
///     the caller's current on-site mix — so tools that don't pass it keep receiving byte-identical
///     responses. XX is rejected: legacy XX data lives outside the mix-keyed Phoenix paths.
///     (Exception: api/charts GET predates this helper and keeps its XX-accepting inline parse.)
/// </summary>
internal static class ApiMixParser
{
    public const string InvalidMessage = "Mix is invalid, valid values: Phoenix, Phoenix2";

    /// <summary>Case-insensitive; null/blank means "not provided" and yields the permanent Phoenix default.</summary>
    public static bool TryParse(string? raw, out MixEnum mix)
    {
        mix = MixEnum.Phoenix;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var trimmed = raw.Trim();
        if (trimmed.Equals(nameof(MixEnum.Phoenix), StringComparison.OrdinalIgnoreCase)) return true;

        if (trimmed.Equals(nameof(MixEnum.Phoenix2), StringComparison.OrdinalIgnoreCase))
        {
            mix = MixEnum.Phoenix2;
            return true;
        }

        // Deliberately not Enum.TryParse: that would admit XX, undefined numeric values ("7"),
        // and future enum members the API hasn't consciously opted into.
        return false;
    }
}

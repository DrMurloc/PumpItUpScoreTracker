using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     One row of piucenter's full chart enumeration (their search page's backing
    ///     table). ExternalKey is their canonical chart identifier and doubles as the
    ///     per-chart data-file name; Variant is the parsed key suffix (ARCADE, SHORTCUT,
    ///     REMIX, FULLSONG, HALFDOUBLE_*).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterChartListing(
        string ExternalKey,
        ChartType Type,
        int Level,
        string Pack,
        string Variant,
        IReadOnlyList<string> TopSkills,
        decimal Nps,
        string BpmInfo,
        decimal SustainTime,
        decimal TimeUnderTension);
}

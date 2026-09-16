using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>The Hardmode list's storage. Vertical-internal — outside callers read the Domain port.</summary>
internal interface IHardmodeChartRepository
{
    /// <summary>
    ///     Replaces the mix's whole list and its most-held end (D32) in one save. Wholesale on
    ///     purpose: a partial write would leave last week's charts qualifying beside this week's, and
    ///     nothing downstream could tell. One save for both, so a glow can never pair this week's red
    ///     with last week's green.
    /// </summary>
    Task Replace(MixEnum mix, IReadOnlyCollection<HardmodeChartRecord> charts,
        IReadOnlyCollection<HardmodeChartRecord> mostHeld, DateTimeOffset computedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HardmodeChartRecord>> Get(MixEnum mix, CancellationToken cancellationToken);

    /// <summary>
    ///     The other end of the same census: each folder's most-held charts, in the same row shape,
    ///     with <see cref="HardmodeChartRecord.FolderCut" /> holding the size of that end.
    /// </summary>
    Task<IReadOnlyList<HardmodeChartRecord>> GetMostHeld(MixEnum mix, CancellationToken cancellationToken);
}

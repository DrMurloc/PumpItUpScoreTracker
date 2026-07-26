using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     What changed between two mixes' catalogs. A chart keeps its identity across mixes
///     (one chart row, one membership per mix), so the whole diff falls out of chart ids:
///     shared ids with different levels are re-rates, unshared ids are arrivals and
///     departures.
///     Songs and charts are reported separately on purpose. A chart that left because its
///     whole song left is not news twice — it is one departure, listed once under the song.
///     <see cref="AddedCharts" /> and <see cref="RemovedCharts" /> carry only the charts
///     that came or went on their own, while the song stayed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MixDiffRecord(
    MixEnum From,
    MixEnum To,
    IReadOnlyList<MixDiffMoveRecord> Rerated,
    IReadOnlyList<MixDiffSongRecord> ArrivedSongs,
    IReadOnlyList<MixDiffSongRecord> DepartedSongs,
    IReadOnlyList<Chart> AddedCharts,
    IReadOnlyList<Chart> RemovedCharts)
{
    /// <summary>A pair with nothing to say — a mix against itself, or a catalog not yet imported.</summary>
    public static MixDiffRecord Empty(MixEnum from, MixEnum to)
    {
        return new MixDiffRecord(from, to, Array.Empty<MixDiffMoveRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<MixDiffSongRecord>(), Array.Empty<Chart>(), Array.Empty<Chart>());
    }

    public bool IsEmpty => Rerated.Count == 0 && ArrivedSongs.Count == 0 && DepartedSongs.Count == 0
                           && AddedCharts.Count == 0 && RemovedCharts.Count == 0;

    public int ChartsArrived => ArrivedSongs.Sum(s => s.Charts.Count) + AddedCharts.Count;
    public int ChartsDeparted => DepartedSongs.Sum(s => s.Charts.Count) + RemovedCharts.Count;
    public int RatedHarder => Rerated.Count(m => m.Delta > 0);
    public int RatedEasier => Rerated.Count(m => m.Delta < 0);
}

/// <summary>
///     One chart at both ends of the comparison — same chart id, different level. Both
///     sides are carried because each mix's copy knows its own level, note count and
///     legacy slot, and the page shows the before and the after side by side.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MixDiffMoveRecord(Chart Before, Chart After)
{
    public int Delta => After.Level - Before.Level;
}

/// <summary>A song that arrived or left whole, with the charts that travelled with it.</summary>
[ExcludeFromCodeCoverage]
public sealed record MixDiffSongRecord(Song Song, IReadOnlyList<Chart> Charts);

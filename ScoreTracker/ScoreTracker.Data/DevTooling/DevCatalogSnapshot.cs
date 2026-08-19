using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Data.DevTooling;

/// <summary>
///     What the reader downloads and the writer persists. Internal on purpose — these mirror the
///     public API's wire shapes for one dev tool, and nothing outside this folder should build one.
/// </summary>
internal interface IDevCatalogWriter
{
    /// <summary>
    ///     Replaces the entire local catalog in one transaction. Anything referencing a chart —
    ///     scores, saved charts — goes with it, because a chart id that no longer resolves is worse
    ///     than no data.
    /// </summary>
    Task ReplaceCatalog(DevCatalogSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces the given local user's scores. Rows arrive keyed by chart id, the one identifier
    ///     that means the same thing on both sides.
    /// </summary>
    Task ReplaceUserScores(Guid localUserId, IReadOnlyList<DevScoreRow> scores,
        CancellationToken cancellationToken = default);
}

[ExcludeFromCodeCoverage]
internal sealed record DevCatalogSnapshot(
    IReadOnlyList<DevMixRow> Mixes,
    IReadOnlyList<DevSongRow> Songs,
    IReadOnlyList<DevChartRow> Charts,
    IReadOnlyList<DevTierListRow> TierListEntries,
    IReadOnlyList<DevScoringLevelRow> ScoringLevels);

[ExcludeFromCodeCoverage]
internal sealed record DevMixRow(MixEnum Mix, string DisplayName, int SortOrder, bool IsPrimary);

/// <summary>Songs are keyed by name — the catalog has no song id on the wire.</summary>
[ExcludeFromCodeCoverage]
internal sealed record DevSongRow(string Name, string Type, string Artist, int DurationSeconds,
    string ImageUrl, decimal? MinBpm, decimal? MaxBpm);

/// <summary>
///     One chart as one mix expresses it. The same chart id appears once per mix it exists in, with
///     that mix's level and note count.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record DevChartRow(Guid ChartId, MixEnum Mix, MixEnum OriginalMix, string SongName,
    string Type, int Level, int? NoteCount, int PlayerCount, string? StepArtist, string? LegacySlot);

[ExcludeFromCodeCoverage]
internal sealed record DevTierListRow(string ListName, MixEnum Mix, Guid ChartId, string Category, int Order);

[ExcludeFromCodeCoverage]
internal sealed record DevScoringLevelRow(MixEnum Mix, Guid ChartId, double ScoringLevel);

[ExcludeFromCodeCoverage]
internal sealed record DevScoreRow(Guid ChartId, MixEnum Mix, DateTimeOffset RecordedAt, int? Score,
    string? LetterGrade, string? Plate, bool IsBroken, string? Source,
    int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses, int? MaxCombo = null);

using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Writes a catalog and a player's scores into a local database from what the public API
///     returned.
///     <para>
///         The predecessor copied raw table rows, which made every local database a mirror of
///         whatever shape production happened to have that week — and made the dev harness the one
///         consumer that could never be broken by a schema change, because it moved the schema with
///         it. This takes the same wire shapes any integrator gets, so the mapping onto local
///         columns is written down here and a schema change surfaces as a compiler error rather than
///         as a silently mangled local database.
///     </para>
///     <para>
///         Local development only. Everything it writes is replaced wholesale.
///     </para>
/// </summary>
public interface IDevCatalogSeeder
{
    /// <summary>
    ///     Replaces the entire local catalog in one transaction. Anything referencing a chart —
    ///     scores, saved charts — goes with it, because a chart id that no longer exists is worse
    ///     than no data.
    /// </summary>
    Task ReplaceCatalog(DevCatalogSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces the given local user's scores. The rows arrive keyed by chart id, which is the
    ///     one identifier that means the same thing on both sides.
    /// </summary>
    Task ReplaceUserScores(Guid localUserId, IReadOnlyList<DevScoreRow> scores,
        CancellationToken cancellationToken = default);
}

/// <summary>Everything the harness downloads before it writes anything.</summary>
[ExcludeFromCodeCoverage]
public sealed record DevCatalogSnapshot(
    IReadOnlyList<DevMixRow> Mixes,
    IReadOnlyList<DevSongRow> Songs,
    IReadOnlyList<DevChartRow> Charts,
    IReadOnlyList<DevTierListRow> TierListEntries,
    IReadOnlyList<DevScoringLevelRow> ScoringLevels);

[ExcludeFromCodeCoverage]
public sealed record DevMixRow(MixEnum Mix, string DisplayName, int SortOrder, bool IsPrimary);

/// <summary>Songs are keyed by name — the catalog has no song id on the wire.</summary>
[ExcludeFromCodeCoverage]
public sealed record DevSongRow(string Name, string Type, string Artist, int DurationSeconds,
    string ImageUrl, decimal? MinBpm, decimal? MaxBpm);

/// <summary>
///     One chart as one mix expresses it. The same chart id appears once per mix it exists in, with
///     that mix's level and note count.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DevChartRow(Guid ChartId, MixEnum Mix, MixEnum OriginalMix, string SongName,
    string Type, int Level, int? NoteCount, int PlayerCount, string? StepArtist, string? LegacySlot);

[ExcludeFromCodeCoverage]
public sealed record DevTierListRow(string ListName, MixEnum Mix, Guid ChartId, string Category, int Order);

[ExcludeFromCodeCoverage]
public sealed record DevScoringLevelRow(MixEnum Mix, Guid ChartId, double ScoringLevel);

[ExcludeFromCodeCoverage]
public sealed record DevScoreRow(Guid ChartId, MixEnum Mix, DateTimeOffset RecordedAt, int? Score,
    string? LetterGrade, string? Plate, bool IsBroken, string? Source,
    int? Perfects, int? Greats, int? Goods, int? Bads, int? Misses);

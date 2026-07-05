using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     Result of parsing an admin "bulk add charts" JSON blob
///     (schema: docs/design/new-charts-json.md). Global errors describe problems with the
///     blob as a whole; per-song entries carry their own validation errors.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BulkChartsParseResult(
    IReadOnlyList<BulkSongParseResult> Songs,
    IReadOnlyList<string> GlobalErrors);

/// <summary>
///     One song entry from the blob. <see cref="Song" /> is populated only when
///     <see cref="Errors" /> is empty.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BulkSongParseResult(
    int Index,
    string DisplayName,
    BulkSongSpec? Song,
    IReadOnlyList<string> Errors);

/// <summary>A fully validated song ready to be created, with its charts.</summary>
[ExcludeFromCodeCoverage]
public sealed record BulkSongSpec(
    Name Name,
    Name KoreanName,
    Name Artist,
    SongType Type,
    Bpm Bpm,
    TimeSpan Duration,
    Uri ImageUrl,
    IReadOnlyList<BulkChartSpec> Charts);

/// <summary>A fully validated chart ready to be created for the song.</summary>
[ExcludeFromCodeCoverage]
public sealed record BulkChartSpec(
    ChartType Type,
    DifficultyLevel Level,
    Name StepArtist,
    Name ChannelName,
    Uri VideoUrl);

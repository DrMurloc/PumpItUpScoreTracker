using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The player page's summary: identity, the headline ratings, Singles and Doubles competitive
///     levels (the page never shows the overall one), per-folder completion — and the visibility
///     the read was gated on, so the page learns in one send whether it may look and why
///     (docs/design/player-page-and-site-search.md §2). Phoenix-lineage figures come back at zero
///     on a legacy mix; the page hides them rather than printing zeros.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerProfileRecord(
    Guid UserId,
    Name PlayerName,
    Uri ProfileImage,
    Name? Country,
    PlayerVisibility Visibility,
    double Pumbility,
    int TotalRating,
    double SinglesRating,
    double DoublesRating,
    double SinglesCompetitiveLevel,
    double DoublesCompetitiveLevel,
    int HighestLevel,
    int ClearCount,
    IReadOnlyList<PlayerFolderCompletionRecord> FolderCompletion);

/// <summary>
///     One folder — a (chart type, level) pair, since S18 and D18 are different folders with
///     different standings. Passes and the folder's size, plus how many passes sit at each letter
///     grade so a bar can draw the folder's grade spectrum.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerFolderCompletionRecord(
    ChartType Type,
    int Level,
    int Passed,
    int Total,
    IReadOnlyDictionary<PhoenixLetterGrade, int> GradeCounts);

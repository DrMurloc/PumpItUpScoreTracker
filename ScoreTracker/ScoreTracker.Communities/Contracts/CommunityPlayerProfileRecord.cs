using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     The community player page's summary: identity, the headline ratings, and per-level
///     folder completion. Visible to anyone who can see the community's boards — joining a
///     community is the score-visibility consent.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityPlayerProfileRecord(
    Guid UserId,
    Name PlayerName,
    Uri ProfileImage,
    Name? Country,
    bool IsPublic,
    int Pumbility,
    int TotalRating,
    int SinglesRating,
    int DoublesRating,
    double CompetitiveLevel,
    double SinglesCompetitiveLevel,
    double DoublesCompetitiveLevel,
    int HighestLevel,
    int ClearCount,
    IReadOnlyList<CommunityFolderCompletionRecord> FolderCompletion);

/// <summary>One level folder: charts passed over charts total (singles+doubles; co-op excluded).</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityFolderCompletionRecord(int Level, int Passed, int Total);

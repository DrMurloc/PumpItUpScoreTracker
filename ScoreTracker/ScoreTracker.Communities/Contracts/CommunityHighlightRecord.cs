using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     One entry in a community big-wins feed: a crewmate's recent import and the big wins it
///     produced. Name/avatar are resolved fresh at read (always current); IsPublic drives whether
///     the row deep-links to their Sessions page (private profiles redirect anyway).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityHighlightRecord(
    Guid UserId,
    string PlayerName,
    Uri Avatar,
    bool IsPublic,
    MixEnum Mix,
    DateTimeOffset OccurredAt,
    Guid? SessionId,
    IReadOnlyList<SignificantWin> Wins);

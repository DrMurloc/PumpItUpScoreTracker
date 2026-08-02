using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     One entry in a significant-wins feed: a player's recent import and the big wins it
///     produced. Name/avatar are resolved fresh at read (always current); IsPublic drives whether
///     the row deep-links to their Sessions page (private profiles redirect anyway).
///     <para>
///         <paramref name="EventId" /> rides along because an audience that fanned one event out
///         several ways — a win in three of your shared communities — dedupes on it.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerHighlightRecord(
    Guid EventId,
    Guid UserId,
    string PlayerName,
    Uri Avatar,
    bool IsPublic,
    MixEnum Mix,
    DateTimeOffset OccurredAt,
    Guid? SessionId,
    IReadOnlyList<SignificantWin> Wins);

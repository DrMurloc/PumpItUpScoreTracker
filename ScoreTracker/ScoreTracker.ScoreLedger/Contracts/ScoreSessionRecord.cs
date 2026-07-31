using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One play session or import run, as the Undo page lists it. <paramref name="StartedAt" />
///     is wall clock — when the scores reached us — which is what the journal's OccurredAt
///     cannot tell you, because that is the official site's play date.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ScoreSessionRecord(
    Guid Id,
    Guid UserId,
    MixEnum Mix,
    string Source,
    string? AccountTag,
    string? CardId,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt,
    int ScoreCount,
    int NewCount,
    int UpscoreCount)
{
    /// <summary>
    ///     Nothing before this was recorded as a session, so nothing before it can be undone.
    ///     A guard, not a promise about what exists: sessions only start being written when the
    ///     table ships, which is later — so the copy says "before we started recording
    ///     sessions" rather than printing this date (docs/design/delete-my-data.md D4).
    /// </summary>
    public static readonly DateTimeOffset UndoFloor = new(2026, 8, 1, 5, 0, 0, TimeSpan.Zero);

    public bool CanUndo => StartedAt >= UndoFloor;
}

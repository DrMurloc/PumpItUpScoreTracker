using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Events;

/// <summary>
///     The session-snapshot event (design doc revision 2): published after the capture
///     consumer ORCHESTRATES the whole pipeline for a score batch — flags + folder
///     lamps, then the rating step, the title step, and the weekly step, in-process and
///     in order. Published always, even when nothing was flagged and even when a step
///     failed (that section is simply absent). The one Discord card renders from THIS
///     event alone: changes with their final flags (CompetitiveImprover included — it
///     no longer trails), every milestone the batch minted (lamps, ratings, titles,
///     weekly placements), and the per-title progress deltas.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ScoreHighlightsCapturedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    MixEnum Mix,
    Guid? SessionId,
    IReadOnlyList<ScoreHighlightsCapturedEvent.HighlightedChange> Changes,
    IReadOnlyList<PlayerMilestoneRecord> Milestones,
    IReadOnlyList<TitleProgressDelta> TitleProgress)
{
    public static ScoreHighlightsCapturedEvent Create(DateTimeOffset occurredAt, Guid userId, MixEnum mix,
        Guid? sessionId, IReadOnlyList<HighlightedChange> changes,
        IReadOnlyList<PlayerMilestoneRecord>? milestones = null,
        IReadOnlyList<TitleProgressDelta>? titleProgress = null)
    {
        return new ScoreHighlightsCapturedEvent(Guid.NewGuid(), occurredAt, userId, mix, sessionId, changes,
            milestones ?? Array.Empty<PlayerMilestoneRecord>(),
            titleProgress ?? Array.Empty<TitleProgressDelta>());
    }

    [ExcludeFromCodeCoverage]
    public sealed record HighlightedChange(
        Guid ChartId,
        bool IsNewPass,
        int? OldScore,
        int? NewScore,
        string? Plate,
        bool IsBroken,
        HighlightFlags Flags);
}

using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>What undoing a session would do, for the confirm dialog.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetScoreSessionUndoPreviewQuery(Guid UserId, Guid SessionId)
    : IQuery<ScoreSessionUndoPreview?>;

using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>A player's recent import attempts, newest first — what the Import Scores page reads.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetImportHistoryQuery(Guid UserId, int Take = 10)
    : IQuery<IReadOnlyList<ImportAttemptRecord>>;

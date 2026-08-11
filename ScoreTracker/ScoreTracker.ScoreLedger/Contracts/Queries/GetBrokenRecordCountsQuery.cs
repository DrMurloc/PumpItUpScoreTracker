using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Every Phoenix-scoring mix and how many of this player's records there are failed runs.
///     Mixes with none still come back, same reasoning as <see cref="GetMixesWithScoresQuery" />:
///     a card that renders only what it found is indistinguishable from a card that forgot to
///     look, and this is the one people go hunting for after they untick the import box.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetBrokenRecordCountsQuery(Guid UserId) : IQuery<IReadOnlyList<BrokenRecordCount>>;

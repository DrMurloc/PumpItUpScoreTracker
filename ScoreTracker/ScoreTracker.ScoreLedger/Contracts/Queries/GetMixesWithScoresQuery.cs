using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Every mix that can hold scores, oldest first, flagged with whether this player has any.
///     Deliberately not filtered down to what they hold: a picker that hides the empty ones is
///     indistinguishable from a picker that has forgotten they exist, which is exactly how the
///     hardcoded Phoenix/Phoenix 2/XX list read.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMixesWithScoresQuery(Guid UserId) : IQuery<IReadOnlyList<MixScoreCount>>;

using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Which mixes this player holds scores in, oldest first. Every mix is deletable — legacy
///     ones record in BestAttempt rather than PhoenixRecord, which is a storage detail and not a
///     reason to leave them out of the picker.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMixesWithScoresQuery(Guid UserId) : IQuery<IReadOnlyList<MixEnum>>;

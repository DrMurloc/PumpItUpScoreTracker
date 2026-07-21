using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Each member's co-op completion fraction (0..1): passed co-op charts over all of the
///     mix's co-op charts, ×2–×5 player counts pooled. Same guard as the boards.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityCoOpCompletionQuery(Name CommunityName, MixEnum Mix)
    : IQuery<IReadOnlyDictionary<Guid, double>>;

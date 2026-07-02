using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Member ids of a community by name; empty when the community doesn't exist.
///     No privacy gate — this mirrors the raw membership join it replaced (the
///     completion-leaderboard filter in ScoreLedger), which never checked privacy.
///     Use <see cref="GetCommunityQuery" /> when privacy semantics matter.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityMembersQuery(Name CommunityName) : IQuery<IEnumerable<Guid>>
{
}

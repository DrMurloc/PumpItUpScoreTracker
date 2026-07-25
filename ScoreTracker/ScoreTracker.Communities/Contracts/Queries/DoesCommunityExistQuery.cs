using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Name-availability check for the create flow — the same answer creating would give,
///     surfaced while typing instead of on save.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DoesCommunityExistQuery(Name CommunityName) : IQuery<bool>;

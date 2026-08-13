using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Display names for a set of community ids — what a surface outside the membership (the
///     site admin's report queue) uses to label a community it cannot reach through
///     <c>GetMyCommunitiesQuery</c>. Unknown ids are simply absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityNamesQuery(IReadOnlyCollection<Guid> CommunityIds)
    : IQuery<IReadOnlyDictionary<Guid, Name>>;

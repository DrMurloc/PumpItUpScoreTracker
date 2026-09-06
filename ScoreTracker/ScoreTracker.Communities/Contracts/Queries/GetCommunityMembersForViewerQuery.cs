using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Member ids of a community as a particular viewer may see them — the rule the site applies
///     to a roster, published so an API filter composes it rather than re-implementing it.
///     <para>
///         A public or public-with-code community answers for anyone. A private community answers
///         only for a member; for anyone else it answers <c>null</c>, exactly as an unknown name
///         does, so the two cannot be told apart and private names cannot be probed. A <c>null</c>
///         viewer is anonymous. Banned members are not members.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityMembersForViewerQuery(Name CommunityName, Guid? ViewerUserId)
    : IQuery<IReadOnlySet<Guid>?>;

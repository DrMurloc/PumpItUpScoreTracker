using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Events;

/// <summary>
///     A community was deleted, with everything Communities owned about it. Published so other
///     verticals can settle what THEY hold against the club — ChartComments archives its comments
///     and purges its reports and mutes. Carries the name as well as the id because this event is
///     the last moment the pair exists: the row is already gone, and an archive full of rows from
///     a club nothing can name would answer no question.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityDeletedEvent(Guid CommunityId, Name CommunityName);

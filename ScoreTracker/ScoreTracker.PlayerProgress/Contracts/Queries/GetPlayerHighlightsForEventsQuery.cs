namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Payloads for specific events — how an audience index turns its own rows back into wins.
///     Communities holds (EventId × CommunityId) so a World-scoped feed stays a seek rather than
///     a join over every member; it picks the events, then asks here what they were.
///     <para>
///         Order is the caller's: it already knows the one it wants, and re-sorting here would
///         throw away the ordering its index just produced.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerHighlightsForEventsQuery(IReadOnlyCollection<Guid> EventIds)
    : IQuery<IEnumerable<PlayerHighlightRecord>>;

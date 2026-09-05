using ScoreTracker.Domain.Models;

namespace ScoreTracker.Rivals.Contracts;

/// <summary>
///     One site player on your peers list (docs/design/peers-abstraction.md D18): who they are,
///     the competitive level the list is sorted on, and every reason they are a peer, so the
///     widget can tag the row rather than guess.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PeerListEntry(
    User User,
    double Level,
    bool IsRival,
    IReadOnlyList<string> Communities,
    bool IsCompetitive,
    bool IsPumbility);

/// <summary>
///     Your peers, nearest competitive level first. Board-only rivals have no site level, so they
///     ride separately and a surface appends them after the ranked rows.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PeerList(
    IReadOnlyList<PeerListEntry> Players,
    IReadOnlyList<RivalSubject> BoardOnlyRivals,
    int Total,
    double MyLevel);

/// <summary>
///     One source the viewer could tick on <c>/Account</c>, with the members it would contribute
///     per chart type — the same sets the standing reader resolves, so the dialog's "your peers
///     right now" tally is the union the colors will use. Type-blind sources (rivals, communities)
///     carry the same set twice.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PeerSourceOption(
    PeerSourceKind Kind,
    Guid? CommunityId,
    string Name,
    bool IsRegional,
    bool IsWorld,
    bool Available,
    IReadOnlySet<Guid> SinglesMembers,
    IReadOnlySet<Guid> DoublesMembers,
    int BoardOnlyRivals);

[ExcludeFromCodeCoverage]
public sealed record PeerSourceCatalog(IReadOnlyList<PeerSourceOption> Options)
{
    public static PeerSourceCatalog Empty { get; } = new(Array.Empty<PeerSourceOption>());
}

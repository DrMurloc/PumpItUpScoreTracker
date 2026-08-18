using MediatR;

namespace ScoreTracker.ChartComments.Contracts.Commands;

/// <summary>
///     Files a report. Idempotent per reporter and comment — reporting again while yours is open
///     changes nothing. The reason decides routing, and the routing is deliberately not part of
///     this contract.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReportCommentCommand(Guid CommentId, CommentReportReason Reason) : IRequest;

/// <summary>
///     Closes one report on one desk, leaving the comment standing. The site queue requires the
///     site admin; the community queue requires standing over the comment's author in that club.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DismissCommentReportCommand(Guid ReportId, CommentReportQueue Queue) : IRequest;

/// <summary>
///     Mutes a member in one community: they stay in the club and lose the mic (post, reply and
///     edit there — delete and votes are untouched). Prospective; existing comments stay unless
///     separately removed. Community moderators only — the site admin's tools are removal and the
///     account lock, never a community mute.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RestrictCommentingCommand(Guid CommunityId, Guid UserId, string? Reason) : IRequest;

/// <summary>Lifts a mute. Same ladder as imposing one.</summary>
[ExcludeFromCodeCoverage]
public sealed record LiftCommentRestrictionCommand(Guid CommunityId, Guid UserId) : IRequest;

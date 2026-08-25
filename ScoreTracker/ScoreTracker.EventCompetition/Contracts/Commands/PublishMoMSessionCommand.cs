using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands;

/// <summary>
///     Publishes the caller's draft: PublishedAt is stamped now and becomes the recorded
///     date and the tie-break clock (D18), the session freezes (D17 — a correction is
///     delete-and-resubmit), and the board ranks it. Publishing an empty draft, someone
///     else's session, or one already published is a domain error. Fires
///     MoMSessionPublishedEvent on the bus.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PublishMoMSessionCommand(Guid SessionId) : IRequest;

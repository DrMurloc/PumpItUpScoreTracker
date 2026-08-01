using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

/// <summary>
///     Asks the four verticals that hold a player's contributions to remove them. It lives here
///     rather than in a vertical because no single vertical owns the set, and the alternative —
///     Web publishing to the bus directly — would put orchestration in Presentation.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteMyContributionsCommand(Guid UserId, ContributionDeletionItems Items) : IRequest;

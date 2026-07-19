using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Lift a ban; the user is no longer a member but may join again.</summary>
[ExcludeFromCodeCoverage]
public sealed record UnbanMemberCommand(Name CommunityName, Guid UserId) : IRequest;

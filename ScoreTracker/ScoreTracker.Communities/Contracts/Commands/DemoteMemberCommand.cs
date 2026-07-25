using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Demote an admin back to a plain member.</summary>
[ExcludeFromCodeCoverage]
public sealed record DemoteMemberCommand(Name CommunityName, Guid UserId) : IRequest;

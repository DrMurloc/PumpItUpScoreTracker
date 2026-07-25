using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Ban a member or admin; the row is retained so they cannot rejoin.</summary>
[ExcludeFromCodeCoverage]
public sealed record BanMemberCommand(Name CommunityName, Guid UserId) : IRequest;

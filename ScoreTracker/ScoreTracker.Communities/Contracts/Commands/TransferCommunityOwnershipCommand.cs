using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Transfer the single creator seat; the old creator becomes an admin with all permissions.</summary>
[ExcludeFromCodeCoverage]
public sealed record TransferCommunityOwnershipCommand(Name CommunityName, Guid UserId) : IRequest;

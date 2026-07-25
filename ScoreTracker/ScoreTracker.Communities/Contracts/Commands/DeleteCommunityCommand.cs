using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Delete a community and all of its rows (creator only).</summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteCommunityCommand(Name CommunityName) : IRequest;

using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Set the fallback culture for the community's Discord notifications (creator only).</summary>
[ExcludeFromCodeCoverage]
public sealed record SetCommunityLanguageCommand(Name CommunityName, string? Culture) : IRequest;

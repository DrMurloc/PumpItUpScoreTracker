using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Change a community's privacy (creator only).</summary>
[ExcludeFromCodeCoverage]
public sealed record SetCommunityPrivacyCommand(Name CommunityName, CommunityPrivacyType PrivacyType) : IRequest;

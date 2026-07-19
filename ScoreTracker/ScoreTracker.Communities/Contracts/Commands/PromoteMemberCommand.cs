using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Promote a member to admin with the given permissions (delegation subset rule applies).</summary>
[ExcludeFromCodeCoverage]
public sealed record PromoteMemberCommand(Name CommunityName, Guid UserId, CommunityPermission Permissions) : IRequest;

using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Replace an existing admin's permission set (delegation subset rule applies).</summary>
[ExcludeFromCodeCoverage]
public sealed record SetMemberPermissionsCommand(Name CommunityName, Guid UserId, CommunityPermission Permissions)
    : IRequest;

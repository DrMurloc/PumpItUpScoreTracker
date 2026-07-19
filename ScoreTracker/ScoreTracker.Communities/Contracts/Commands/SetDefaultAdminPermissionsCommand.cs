using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>Set the default permission set applied to newly promoted admins (creator only).</summary>
[ExcludeFromCodeCoverage]
public sealed record SetDefaultAdminPermissionsCommand(Name CommunityName, CommunityPermission Permissions) : IRequest;

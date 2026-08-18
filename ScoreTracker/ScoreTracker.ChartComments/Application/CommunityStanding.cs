using MediatR;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     The two role reads every moderation decision is made of, both through Communities'
///     published contracts: the caller's own standing in a club, and every member's role there.
///     A null role means no membership row.
/// </summary>
internal static class CommunityStanding
{
    public static async Task<(CommunityRole? Role, CommunityPermission Permissions)> Mine(
        IMediator mediator, Guid communityId, CancellationToken cancellationToken)
    {
        var mine = (await mediator.Send(new GetMyCommunityRolesQuery(), cancellationToken))
            .FirstOrDefault(role => role.CommunityId == communityId);

        return mine == null ? (null, CommunityPermission.None) : (mine.Role, mine.Permissions);
    }

    public static async Task<IReadOnlyDictionary<Guid, CommunityRole>> MemberRoles(
        IMediator mediator, Guid communityId, CancellationToken cancellationToken)
    {
        return (await mediator.Send(new GetCommunityMemberRolesQuery(communityId), cancellationToken))
            .ToDictionary(member => member.UserId, member => member.Role);
    }

    public static CommunityRole? RoleOf(this IReadOnlyDictionary<Guid, CommunityRole> roles, Guid userId)
    {
        return roles.TryGetValue(userId, out var role) ? role : null;
    }
}

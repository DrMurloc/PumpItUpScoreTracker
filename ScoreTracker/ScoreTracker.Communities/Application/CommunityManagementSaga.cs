using MassTransit;
using MediatR;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Contracts.Events;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Community role/permission management: promotion, demotion, bans, ownership transfer, and
///     creator-only settings. Every handler loads the <see cref="Community" /> aggregate and calls
///     the corresponding method with the acting user — the aggregate authorizes and throws
///     <see cref="CommunityPermissionException" /> on a violation, so these handlers stay thin.
/// </summary>
internal sealed class CommunityManagementSaga :
    IRequestHandler<PromoteMemberCommand>,
    IRequestHandler<SetMemberPermissionsCommand>,
    IRequestHandler<DemoteMemberCommand>,
    IRequestHandler<BanMemberCommand>,
    IRequestHandler<UnbanMemberCommand>,
    IRequestHandler<TransferCommunityOwnershipCommand>,
    IRequestHandler<SetCommunityPrivacyCommand>,
    IRequestHandler<SetDefaultAdminPermissionsCommand>,
    IRequestHandler<SetCommunityLanguageCommand>,
    IRequestHandler<DeleteCommunityCommand>,
    IRequestHandler<GetMyCommunityRoleQuery, CommunityRoleRecord>,
    IRequestHandler<GetMyCommunityRolesQuery, IEnumerable<MyCommunityRoleRecord>>,
    IRequestHandler<GetCommunityMemberRolesQuery, IEnumerable<CommunityMemberRoleRecord>>,
    IRequestHandler<GetCommunityNamesQuery, IReadOnlyDictionary<Guid, Name>>,
    IRequestHandler<GetCommunityRosterQuery, IEnumerable<CommunityMemberRecord>>
{
    private readonly IBus _bus;
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDiscordRoleService _discordRoles;
    private readonly IMediator _mediator;
    private readonly IDiscordRoleRepository _roleConfiguration;

    public CommunityManagementSaga(ICommunityRepository communities, ICurrentUserAccessor currentUser,
        IMediator mediator, IBus bus, IDiscordRoleService discordRoles,
        IDiscordRoleRepository roleConfiguration)
    {
        _communities = communities;
        _currentUser = currentUser;
        _mediator = mediator;
        _bus = bus;
        _discordRoles = discordRoles;
        _roleConfiguration = roleConfiguration;
    }

    public Task Handle(PromoteMemberCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.PromoteToAdmin(_currentUser.User.Id, request.UserId, request.Permissions), cancellationToken);

    public Task Handle(SetMemberPermissionsCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.SetAdminPermissions(_currentUser.User.Id, request.UserId, request.Permissions),
            cancellationToken);

    public Task Handle(DemoteMemberCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.DemoteToMember(_currentUser.User.Id, request.UserId), cancellationToken);

    public Task Handle(BanMemberCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.Ban(_currentUser.User.Id, request.UserId), cancellationToken, request.UserId);

    public Task Handle(UnbanMemberCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.Unban(_currentUser.User.Id, request.UserId), cancellationToken, request.UserId);

    public async Task Handle(TransferCommunityOwnershipCommand request, CancellationToken cancellationToken)
    {
        // The recipient, not the sender. Handing a community to an account that is on its way
        // out would take the community with it a few days later.
        if (await _mediator.Send(new GetPendingAccountDeletionQuery(request.UserId), cancellationToken) != null)
            throw new DeniedFromCommunityException(
                "That player's account is scheduled for deletion, so they can't take over a community.");

        await Mutate(request.CommunityName, community =>
            community.TransferCreator(_currentUser.User.Id, request.UserId), cancellationToken);
    }

    public Task Handle(SetCommunityPrivacyCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.SetPrivacy(_currentUser.User.Id, request.PrivacyType), cancellationToken);

    public Task Handle(SetDefaultAdminPermissionsCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.SetDefaultAdminPermissions(_currentUser.User.Id, request.Permissions), cancellationToken);

    public Task Handle(SetCommunityLanguageCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.SetDefaultLanguage(_currentUser.User.Id,
                SupportedCultures.NormalizeOrNull(request.Culture)), cancellationToken);

    public async Task Handle(DeleteCommunityCommand request, CancellationToken cancellationToken)
    {
        var community = await Load(request.CommunityName, cancellationToken);
        // Deletion is a repository operation, not an aggregate state change — authorize here.
        if (community.RoleOf(_currentUser.User.Id) != CommunityRole.Creator)
            throw new CommunityPermissionException("Only the creator may delete the community.");

        // Hand the Discord roles back BEFORE the club goes: the mappings say which roles are ours
        // to take, and a moment later they will not exist. Inline rather than published for the
        // same reason — there is no second chance once the rows are gone.
        if (await _communities.GetCommunityId(request.CommunityName, cancellationToken) is { } communityId)
        {
            await _discordRoles.RevokeAll(communityId, cancellationToken);
            await _roleConfiguration.DeleteAllForCommunity(communityId, cancellationToken);
        }

        var deletedId = await _communities.DeleteCommunity(request.CommunityName, cancellationToken);

        // The last moment the id/name pair exists — the row is already gone. Other verticals
        // settle what THEY hold against the club off this fact: ChartComments archives the
        // club's comments and purges its reports and mutes.
        if (deletedId is { } id)
            await _bus.Publish(new CommunityDeletedEvent(id, request.CommunityName), cancellationToken);
    }

    public async Task<CommunityRoleRecord> Handle(GetMyCommunityRoleQuery request,
        CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
        if (community == null || !_currentUser.IsLoggedIn)
            return new CommunityRoleRecord(null, CommunityPermission.None);
        var userId = _currentUser.User.Id;
        return new CommunityRoleRecord(community.RoleOf(userId), community.PermissionsOf(userId));
    }

    public async Task<IEnumerable<MyCommunityRoleRecord>> Handle(GetMyCommunityRolesQuery request,
        CancellationToken cancellationToken) =>
        _currentUser.IsLoggedIn
            ? await _communities.GetUserRoles(_currentUser.User.Id, cancellationToken)
            : Array.Empty<MyCommunityRoleRecord>();

    public async Task<IEnumerable<CommunityMemberRoleRecord>> Handle(GetCommunityMemberRolesQuery request,
        CancellationToken cancellationToken) =>
        await _communities.GetMemberRoles(request.CommunityId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Name>> Handle(GetCommunityNamesQuery request,
        CancellationToken cancellationToken) =>
        await _communities.GetCommunityNames(request.CommunityIds, cancellationToken);

    public async Task<IEnumerable<CommunityMemberRecord>> Handle(GetCommunityRosterQuery request,
        CancellationToken cancellationToken)
    {
        var roster = await _communities.GetRoster(request.CommunityName, cancellationToken);

        // Membership is the score-visibility consent: outside viewers don't see private-profile
        // members at all. Anyone with a membership row (bans included) sees the full roster.
        if (_currentUser.IsLoggedIn && roster.Any(m => m.UserId == _currentUser.User.Id)) return roster;
        return roster.Where(m => m.IsPublic).ToArray();
    }

    /// <summary>
    ///     <paramref name="affectedUserId" /> settles that member's Discord roles afterwards. Only
    ///     the mutations that change who is IN the community pass it — a language or permission
    ///     edit changes none of the four facts a role hangs on, and a whole-roster pass for one
    ///     would be work nobody asked for.
    /// </summary>
    private async Task Mutate(Name communityName, Action<Community> action,
        CancellationToken cancellationToken, Guid? affectedUserId = null)
    {
        var community = await Load(communityName, cancellationToken);
        action(community);
        await _communities.SaveCommunity(community, cancellationToken);

        if (affectedUserId is { } userId &&
            await _communities.GetCommunityId(communityName, cancellationToken) is { } communityId)
            await _bus.Publish(new ReconcileDiscordRolesCommand(communityId, userId), cancellationToken);
    }

    private async Task<Community> Load(Name communityName, CancellationToken cancellationToken) =>
        await _communities.GetCommunityByName(communityName, cancellationToken)
        ?? throw new CommunityNotFoundException();
}

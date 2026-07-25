using MediatR;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

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
    IRequestHandler<GetCommunityRosterQuery, IEnumerable<CommunityMemberRecord>>
{
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;

    public CommunityManagementSaga(ICommunityRepository communities, ICurrentUserAccessor currentUser)
    {
        _communities = communities;
        _currentUser = currentUser;
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
            community.Ban(_currentUser.User.Id, request.UserId), cancellationToken);

    public Task Handle(UnbanMemberCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.Unban(_currentUser.User.Id, request.UserId), cancellationToken);

    public Task Handle(TransferCommunityOwnershipCommand request, CancellationToken cancellationToken) =>
        Mutate(request.CommunityName, community =>
            community.TransferCreator(_currentUser.User.Id, request.UserId), cancellationToken);

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
        await _communities.DeleteCommunity(request.CommunityName, cancellationToken);
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

    public async Task<IEnumerable<CommunityMemberRecord>> Handle(GetCommunityRosterQuery request,
        CancellationToken cancellationToken)
    {
        var roster = await _communities.GetRoster(request.CommunityName, cancellationToken);

        // Membership is the score-visibility consent: outside viewers don't see private-profile
        // members at all. Anyone with a membership row (bans included) sees the full roster.
        if (_currentUser.IsLoggedIn && roster.Any(m => m.UserId == _currentUser.User.Id)) return roster;
        return roster.Where(m => m.IsPublic).ToArray();
    }

    private async Task Mutate(Name communityName, Action<Community> action, CancellationToken cancellationToken)
    {
        var community = await Load(communityName, cancellationToken);
        action(community);
        await _communities.SaveCommunity(community, cancellationToken);
    }

    private async Task<Community> Load(Name communityName, CancellationToken cancellationToken) =>
        await _communities.GetCommunityByName(communityName, cancellationToken)
        ?? throw new CommunityNotFoundException();
}

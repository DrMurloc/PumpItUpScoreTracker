using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models.UserGroups;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

/// <summary>
///     A community of players ranked on piuscores data. For non-regional communities this is a
///     rich aggregate: role and permission changes go through its methods, each of which
///     authorizes the acting user and throws <see cref="CommunityPermissionException" /> on a
///     violation — authorization lives here, not in handlers. Regional/World communities are
///     ownerless (<see cref="OwnerId" /> = <see cref="Guid.Empty" />) and carry no roles, so every
///     management method throws for them.
///
///     Membership itself (<see cref="MemberIds" />, <see cref="InviteCodes" />,
///     <see cref="Channels" />) stays a mutable projection the join/leave/create flows write
///     directly; the role/permission/ban overlays add standing on top of it.
/// </summary>
public sealed class Community : UserGroup
{
    public const CommunityPermission DefaultAdminPermissionsSeed =
        CommunityPermission.ManageInviteLinks | CommunityPermission.ManageUsers |
        CommunityPermission.ManageChannelSubscriptions;

    // Role overlays on top of MemberIds. Admins and their permissions; who granted them; the
    // banned set (retained to block rejoin); optional join timestamps hydrated from storage.
    private readonly Dictionary<Guid, CommunityPermission> _adminPermissions = new();
    private readonly Dictionary<Guid, Guid?> _grantedBy = new();
    private readonly HashSet<Guid> _banned = new();
    private readonly Dictionary<Guid, DateTimeOffset?> _joinedAt = new();

    public Community(Name name, Guid ownerId, CommunityPrivacyType privacyType, bool isRegional) : this(name,
        ownerId, privacyType,
        Array.Empty<Guid>(),
        Array.Empty<ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), isRegional)
    {
    }

    public Community(Name name, Guid ownerId, CommunityPrivacyType privacyType, IEnumerable<Guid> memberIds,
        IEnumerable<ChannelConfiguration> channels,
        IDictionary<Guid, DateOnly?> inviteCodes, bool isRegional) : base(name)
    {
        Name = name;
        OwnerId = ownerId;
        MemberIds = memberIds.Distinct().ToHashSet();
        Channels = channels.ToList();
        InviteCodes = inviteCodes;
        PrivacyType = privacyType;
        IsRegional = isRegional;
    }

    /// <summary>
    ///     Hydration constructor (repository): explicit member standings, default admin
    ///     permissions, and notification language. Banned members do not appear in
    ///     <see cref="MemberIds" /> but are retained in the ban set.
    /// </summary>
    public Community(Name name, Guid ownerId, CommunityPrivacyType privacyType,
        IEnumerable<CommunityMember> members,
        IEnumerable<ChannelConfiguration> channels,
        IDictionary<Guid, DateOnly?> inviteCodes, bool isRegional,
        CommunityPermission defaultAdminPermissions, string? defaultLanguage) : base(name)
    {
        Name = name;
        OwnerId = ownerId;
        Channels = channels.ToList();
        InviteCodes = inviteCodes;
        PrivacyType = privacyType;
        IsRegional = isRegional;
        DefaultAdminPermissions = defaultAdminPermissions;
        DefaultLanguage = defaultLanguage;

        var memberIds = new HashSet<Guid>();
        foreach (var member in members)
        {
            _joinedAt[member.UserId] = member.JoinedAt;
            switch (member.Role)
            {
                case CommunityRole.Banned:
                    _banned.Add(member.UserId);
                    break;
                case CommunityRole.Admin:
                    memberIds.Add(member.UserId);
                    _adminPermissions[member.UserId] = member.Permissions;
                    _grantedBy[member.UserId] = member.GrantedBy;
                    break;
                default: // Creator + Member both live in the membership set
                    memberIds.Add(member.UserId);
                    break;
            }
        }

        MemberIds = memberIds;
    }

    public override Name Name { get; }
    public Guid OwnerId { get; private set; }
    public ISet<Guid> MemberIds { get; }
    public ICollection<ChannelConfiguration> Channels { get; }
    public CommunityPrivacyType PrivacyType { get; private set; }
    public IDictionary<Guid, DateOnly?> InviteCodes { get; }
    public bool IsRegional { get; }
    public bool RequiresCode => PrivacyType is CommunityPrivacyType.Private or CommunityPrivacyType.PublicWithCode;

    /// <summary>Default permission set applied to a newly promoted admin; creator-settable.</summary>
    public CommunityPermission DefaultAdminPermissions { get; private set; } = DefaultAdminPermissionsSeed;

    /// <summary>Fallback culture for this community's Discord notifications; creator-settable.</summary>
    public string? DefaultLanguage { get; private set; }

    /// <summary>Users with a retained ban row — blocked from rejoining.</summary>
    public IReadOnlyCollection<Guid> BannedUserIds => _banned;

    /// <summary>The full member projection: creator, admins (with permissions), members, and bans.</summary>
    public IReadOnlyCollection<CommunityMember> Members
    {
        get
        {
            var list = new List<CommunityMember>();
            foreach (var id in MemberIds) list.Add(Project(id));
            // A real owner is always the Creator even if a legacy row left them out of MemberIds.
            if (OwnerId != Guid.Empty && !MemberIds.Contains(OwnerId))
                list.Add(new CommunityMember(OwnerId, CommunityRole.Creator, CommunityPermission.All, null,
                    JoinedAtOf(OwnerId)));
            foreach (var id in _banned)
                list.Add(new CommunityMember(id, CommunityRole.Banned, CommunityPermission.None, null,
                    JoinedAtOf(id)));
            return list;
        }
    }

    /// <summary>The member's current role, or null if they are neither a member nor banned.</summary>
    public CommunityRole? RoleOf(Guid userId)
    {
        if (_banned.Contains(userId)) return CommunityRole.Banned;
        if (userId != Guid.Empty && userId == OwnerId) return CommunityRole.Creator;
        if (_adminPermissions.ContainsKey(userId)) return CommunityRole.Admin;
        if (MemberIds.Contains(userId)) return CommunityRole.Member;
        return null;
    }

    /// <summary>The effective permissions the user holds (Creator = all; Admin = granted; else none).</summary>
    public CommunityPermission PermissionsOf(Guid userId)
    {
        if (userId != Guid.Empty && userId == OwnerId) return CommunityPermission.All;
        return _adminPermissions.TryGetValue(userId, out var permissions) ? permissions : CommunityPermission.None;
    }

    public bool HasPermission(Guid userId, CommunityPermission permission) =>
        (PermissionsOf(userId) & permission) == permission;

    public bool IsBanned(Guid userId) => _banned.Contains(userId);

    // ----- Management (each authorizes the acting user) --------------------------------------

    /// <summary>Promote an existing member to admin with the given permissions.</summary>
    public void PromoteToAdmin(Guid actorId, Guid targetId, CommunityPermission permissions)
    {
        EnsureCanPromote(actorId, permissions);
        if (RoleOf(targetId) != CommunityRole.Member)
            throw new CommunityPermissionException("Only a current member can be promoted to admin.");
        MemberIds.Add(targetId);
        _adminPermissions[targetId] = permissions;
        _grantedBy[targetId] = actorId;
    }

    /// <summary>Replace an existing admin's permission set (delegation subset rule applies).</summary>
    public void SetAdminPermissions(Guid actorId, Guid targetId, CommunityPermission permissions)
    {
        EnsureCanPromote(actorId, permissions);
        if (RoleOf(targetId) != CommunityRole.Admin)
            throw new CommunityPermissionException("Only an admin's permissions can be edited.");
        _adminPermissions[targetId] = permissions;
        _grantedBy[targetId] = actorId;
    }

    /// <summary>Demote an admin back to a plain member.</summary>
    public void DemoteToMember(Guid actorId, Guid targetId)
    {
        if (RoleOf(targetId) != CommunityRole.Admin)
            throw new CommunityPermissionException("Only an admin can be demoted.");
        EnsureCanActOnAdmin(actorId, targetId);
        _adminPermissions.Remove(targetId);
        _grantedBy.Remove(targetId);
    }

    /// <summary>Ban a member or admin; the row is retained so they cannot rejoin.</summary>
    public void Ban(Guid actorId, Guid targetId)
    {
        if (!HasPermission(actorId, CommunityPermission.ManageUsers))
            throw new CommunityPermissionException("You cannot manage users.");
        if (targetId == OwnerId)
            throw new CommunityPermissionException("The creator cannot be banned.");
        if (RoleOf(targetId) == CommunityRole.Admin) EnsureCanActOnAdmin(actorId, targetId);
        MemberIds.Remove(targetId);
        _adminPermissions.Remove(targetId);
        _grantedBy.Remove(targetId);
        _banned.Add(targetId);
    }

    /// <summary>Lift a ban; the user is no longer a member but may join again.</summary>
    public void Unban(Guid actorId, Guid targetId)
    {
        if (!HasPermission(actorId, CommunityPermission.ManageUsers))
            throw new CommunityPermissionException("You cannot manage users.");
        if (!_banned.Contains(targetId))
            throw new CommunityPermissionException("That user is not banned.");
        _banned.Remove(targetId);
    }

    /// <summary>Transfer the single creator seat; the old creator becomes an admin with all permissions.</summary>
    public void TransferCreator(Guid actorId, Guid targetId)
    {
        EnsureCreator(actorId);
        if (targetId == OwnerId)
            throw new CommunityPermissionException("That user is already the creator.");
        if (!MemberIds.Contains(targetId) || _banned.Contains(targetId))
            throw new CommunityPermissionException("The new creator must be a current member.");
        var previousOwner = OwnerId;
        OwnerId = targetId;
        _adminPermissions.Remove(targetId);
        _grantedBy.Remove(targetId);
        _adminPermissions[previousOwner] = CommunityPermission.All;
        _grantedBy[previousOwner] = targetId;
        MemberIds.Add(targetId);
    }

    public void SetDefaultAdminPermissions(Guid actorId, CommunityPermission permissions)
    {
        EnsureCreator(actorId);
        DefaultAdminPermissions = permissions;
    }

    public void SetPrivacy(Guid actorId, CommunityPrivacyType privacyType)
    {
        EnsureCreator(actorId);
        PrivacyType = privacyType;
    }

    public void SetDefaultLanguage(Guid actorId, string? culture)
    {
        EnsureCreator(actorId);
        DefaultLanguage = culture;
    }

    // ----- Guards ----------------------------------------------------------------------------

    private void EnsureCreator(Guid actorId)
    {
        if (actorId == Guid.Empty || actorId != OwnerId)
            throw new CommunityPermissionException("Only the creator may do this.");
    }

    private void EnsureCanPromote(Guid actorId, CommunityPermission permissions)
    {
        if (!HasPermission(actorId, CommunityPermission.PromoteAdmins))
            throw new CommunityPermissionException("You cannot promote admins.");
        // Delegation rule: you may only grant permissions you yourself hold.
        if ((PermissionsOf(actorId) & permissions) != permissions)
            throw new CommunityPermissionException("You cannot grant permissions you do not hold.");
    }

    // An admin may act on another admin only if they can promote AND the target holds no
    // permission the actor lacks (you can only unwind admins you could have created). The
    // creator always can.
    private void EnsureCanActOnAdmin(Guid actorId, Guid targetId)
    {
        if (actorId == OwnerId) return;
        if (!HasPermission(actorId, CommunityPermission.PromoteAdmins))
            throw new CommunityPermissionException("You cannot manage other admins.");
        var targetPermissions = PermissionsOf(targetId);
        if ((PermissionsOf(actorId) & targetPermissions) != targetPermissions)
            throw new CommunityPermissionException("You cannot manage an admin with permissions you lack.");
    }

    private CommunityMember Project(Guid id)
    {
        if (id == OwnerId)
            return new CommunityMember(id, CommunityRole.Creator, CommunityPermission.All, null, JoinedAtOf(id));
        if (_adminPermissions.TryGetValue(id, out var permissions))
            return new CommunityMember(id, CommunityRole.Admin, permissions, _grantedBy.GetValueOrDefault(id),
                JoinedAtOf(id));
        return new CommunityMember(id, CommunityRole.Member, CommunityPermission.None, null, JoinedAtOf(id));
    }

    private DateTimeOffset? JoinedAtOf(Guid id) => _joinedAt.TryGetValue(id, out var joinedAt) ? joinedAt : null;

    public sealed record ChannelConfiguration(ulong ChannelId, bool SendNewScores, bool SendTitles,
        bool SendNewMembers)
    {
    }
}

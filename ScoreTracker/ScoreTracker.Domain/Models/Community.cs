using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models.UserGroups;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed class Community : UserGroup
{
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

    public override Name Name { get; }
    public Guid OwnerId { get; }
    public ISet<Guid> MemberIds { get; }
    public ICollection<ChannelConfiguration> Channels { get; }
    public CommunityPrivacyType PrivacyType { get; }
    public IDictionary<Guid, DateOnly?> InviteCodes { get; }
    public bool IsRegional { get; }
    public bool RequiresCode => PrivacyType is CommunityPrivacyType.Private or CommunityPrivacyType.PublicWithCode;

    // Every registered channel receives every community notification; the old per-type
    // opt-in flags were never honored by the fan-out and have been removed.
    public sealed record ChannelConfiguration(ulong ChannelId)
    {
    }
}
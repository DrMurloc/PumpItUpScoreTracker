using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class Community
    {
        public Name Name { get; }
        public Guid OwnerId { get; }
        public ISet<Guid> MemberIds { get; }
        public ICollection<ChannelConfiguration> Channels { get; }
        public CommunityPrivacyType PrivacyType { get; }
        public IDictionary<Guid, DateOnly?> InviteCodes { get; }
        public bool RequiresCode => PrivacyType is CommunityPrivacyType.Private or CommunityPrivacyType.PublicWithCode;

        public Community(Name name, Guid ownerId, CommunityPrivacyType privacyType) : this(name, ownerId, privacyType,
            Array.Empty<Guid>(),
            Array.Empty<ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>())
        {
        }

        public Community(Name name, Guid ownerId, CommunityPrivacyType privacyType, IEnumerable<Guid> memberIds,
            IEnumerable<ChannelConfiguration> channels,
            IDictionary<Guid, DateOnly?> inviteCodes)
        {
            Name = name;
            OwnerId = ownerId;
            MemberIds = memberIds.Distinct().ToHashSet();
            Channels = channels.ToList();
            InviteCodes = inviteCodes;
            PrivacyType = privacyType;
        }

        public sealed record ChannelConfiguration(ulong ChannelId, bool SendNewScores, bool SendTitles,
            bool SendNewMembers)
        {
        }
    }
}

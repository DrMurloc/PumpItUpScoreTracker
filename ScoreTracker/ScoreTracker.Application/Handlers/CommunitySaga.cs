using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class CommunitySaga : IRequestHandler<CreateCommunityCommand>, IRequestHandler<JoinCommunityCommand>,
        IRequestHandler<LeaveCommunityCommand>,
        IRequestHandler<GetCommunityLeaderboardQuery, IEnumerable<CommunityLeaderboardRecord>>,
        IRequestHandler<CreateInviteLinkCommand, Guid>,
        IRequestHandler<GetMyCommunitiesQuery, IEnumerable<CommunityOverviewRecord>>,
        IRequestHandler<GetPublicCommunitiesQuery, IEnumerable<CommunityOverviewRecord>>,
        IRequestHandler<GetCommunityQuery, Community>,
        IRequestHandler<JoinCommunityByInviteCodeCommand>,
        IRequestHandler<AddDiscordChannelToCommunityCommand>

    {
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ICommunityRepository _communities;
        private readonly IBotClient _bot;

        public CommunitySaga(ICurrentUserAccessor currentUser, ICommunityRepository communities, IBotClient bot)
        {
            _currentUser = currentUser;
            _communities = communities;
            _bot = bot;
        }

        public async Task<Unit> Handle(CreateCommunityCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
            if (community != null) throw new CommunityAlreadyExistsException(request.CommunityName);
            community = new Community(request.CommunityName, userId, request.PrivacyType);
            community.MemberIds.Add(userId);
            await _communities.SaveCommunity(community,
                cancellationToken);

            return Unit.Value;
        }

        private async Task<Community> GetCommunity(Name name, CancellationToken cancellationToken)
        {
            var community = await _communities.GetCommunityByName(name, cancellationToken);
            return community ?? throw new CommunityNotFoundException();
        }

        public async Task<Unit> Handle(JoinCommunityCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var community = await GetCommunity(request.CommunityName, cancellationToken);

            if (community.MemberIds.Contains(userId)) return Unit.Value;

            switch (community.PrivacyType)
            {
                case CommunityPrivacyType.Public:
                    community.MemberIds.Add(userId);
                    break;
                case CommunityPrivacyType.Private:
                case CommunityPrivacyType.PublicWithCode:
                    var code = request.InviteCode ??
                               throw new DeniedFromCommunityException("This community requires an invite code");
                    if (!community.InviteCodes.ContainsKey(code))
                        throw new DeniedFromCommunityException(
                            "This is not a valid community code for this community.");

                    if (community.InviteCodes.TryGetValue(code, out var expirationDate) && expirationDate <
                        new DateOnly(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day))
                        throw new DeniedFromCommunityException("This invite code is expired");

                    community.MemberIds.Add(userId);
                    break;
                default:
                    throw new DeniedFromCommunityException("Community privacy type could not be determined");
            }

            await _communities.SaveCommunity(community, cancellationToken);
            return Unit.Value;
        }

        public async Task<Unit> Handle(LeaveCommunityCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var community = await GetCommunity(request.CommunityName, cancellationToken);
            if (!community.MemberIds.Contains(userId)) return Unit.Value;

            community.MemberIds.Remove(userId);
            await _communities.SaveCommunity(community, cancellationToken);
            return Unit.Value;
        }

        public async Task<IEnumerable<CommunityLeaderboardRecord>> Handle(GetCommunityLeaderboardQuery request,
            CancellationToken cancellationToken)
        {
            var community = await GetCommunity(request.Community, cancellationToken);
            if (community.PrivacyType == CommunityPrivacyType.Private &&
                !community.MemberIds.Contains(_currentUser.User.Id))
                throw new DeniedFromCommunityException("This community is private and you must be a member to view it");

            return await _communities.GetLeaderboard(request.Community, cancellationToken);
        }

        public async Task<Guid> Handle(CreateInviteLinkCommand request, CancellationToken cancellationToken)
        {
            var community = await GetCommunity(request.CommunityName, cancellationToken);
            if (!community.MemberIds.Contains(_currentUser.User.Id))
                throw new DeniedFromCommunityException(
                    "You must be a member of a community to create invite links for it");

            var newCode = Guid.NewGuid();
            community.InviteCodes[newCode] = request.ExpirationDate;
            await _communities.SaveCommunity(community, cancellationToken);
            return newCode;
        }

        public async Task<IEnumerable<CommunityOverviewRecord>> Handle(GetMyCommunitiesQuery request,
            CancellationToken cancellationToken)
        {
            return await _communities.GetCommunities(_currentUser.User.Id, cancellationToken);
        }

        public async Task<IEnumerable<CommunityOverviewRecord>> Handle(GetPublicCommunitiesQuery request,
            CancellationToken cancellationToken)
        {
            return await _communities.GetPublicCommunities(cancellationToken);
        }

        public async Task<Community> Handle(GetCommunityQuery request, CancellationToken cancellationToken)
        {
            var community = await GetCommunity(request.CommunityName, cancellationToken);
            if (community.PrivacyType == CommunityPrivacyType.Private &&
                !(_currentUser.IsLoggedIn && community.MemberIds.Contains(_currentUser.User.Id)))
                throw new CommunityNotFoundException();

            return community;
        }

        public async Task<Unit> Handle(JoinCommunityByInviteCodeCommand request, CancellationToken cancellationToken)
        {
            var community = await _communities.GetCommunityByInviteCode(request.InviteCode, cancellationToken);
            if (community == null) throw new CommunityNotFoundException();
            await Handle(new JoinCommunityCommand(community.Value, request.InviteCode), cancellationToken);
            return Unit.Value;
        }

        private async Task<Community> LoadCommunity(Name? communityName, Guid? inviteCode,
            CancellationToken cancellationToken)
        {
            if (inviteCode != null)
                communityName = await _communities.GetCommunityByInviteCode(inviteCode.Value, cancellationToken) ??
                                throw new CommunityNotFoundException();

            if (communityName == null)
                throw new InvalidOperationException("Community Name must be provided if invite code is not used");

            var community = await _communities.GetCommunityByName(communityName.Value, cancellationToken) ??
                            throw new CommunityNotFoundException();
            if (community.RequiresCode && (inviteCode == null || !community.InviteCodes.ContainsKey(inviteCode.Value) ||
                                           community.InviteCodes[inviteCode.Value] <
                                           new DateOnly(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month,
                                               DateTimeOffset.Now.Day)))
                throw new CommunityNotFoundException();

            return community;
        }

        public async Task<Unit> Handle(AddDiscordChannelToCommunityCommand request, CancellationToken cancellationToken)
        {
            var community = await LoadCommunity(request.CommunityName, request.InviteCode, cancellationToken);

            foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId))
                community.Channels.Remove(existingChannel);

            community.Channels.Add(new Community.ChannelConfiguration(request.ChannelId, request.SendScores,
                request.SendTitles, request.SendNewMembers));
            await _communities.SaveCommunity(community, cancellationToken);

            await _bot.SendMessage(
                $"This channel was updated to receive notifications for the {community.Name} community in PIU Scores!",
                request.ChannelId, cancellationToken);
            return Unit.Value;
        }
    }
}

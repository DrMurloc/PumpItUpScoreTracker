using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
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
        IRequestHandler<AddDiscordChannelToCommunityCommand>,
        IRequestHandler<RemoveDiscordChannelFromCommunityCommand>,
        IConsumer<PlayerRatingsImprovedEvent>,
        IConsumer<PlayerScoreUpdatedEvent>,
        IConsumer<NewTitlesAcquiredEvent>

    {
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ICommunityRepository _communities;
        private readonly IUserRepository _users;
        private readonly IBotClient _bot;
        private readonly IChartRepository _charts;
        private readonly IPhoenixRecordRepository _scores;

        public CommunitySaga(ICurrentUserAccessor currentUser, ICommunityRepository communities, IBotClient bot,
            IUserRepository users, IChartRepository charts, IPhoenixRecordRepository scores)
        {
            _currentUser = currentUser;
            _communities = communities;
            _bot = bot;
            _users = users;
            _charts = charts;
            _scores = scores;
        }

        public async Task Handle(CreateCommunityCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
            if (community != null) throw new CommunityAlreadyExistsException(request.CommunityName);
            community = new Community(request.CommunityName, userId, request.PrivacyType);
            community.MemberIds.Add(userId);
            await _communities.SaveCommunity(community,
                cancellationToken);
        }

        private async Task<Community> GetCommunity(Name name, CancellationToken cancellationToken)
        {
            var community = await _communities.GetCommunityByName(name, cancellationToken);
            return community ?? throw new CommunityNotFoundException();
        }

        public async Task Handle(JoinCommunityCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var community = await GetCommunity(request.CommunityName, cancellationToken);

            if (community.MemberIds.Contains(userId)) return;

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
        }

        public async Task Handle(LeaveCommunityCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var community = await GetCommunity(request.CommunityName, cancellationToken);
            if (!community.MemberIds.Contains(userId)) return;

            community.MemberIds.Remove(userId);
            await _communities.SaveCommunity(community, cancellationToken);
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

        public async Task Handle(JoinCommunityByInviteCodeCommand request, CancellationToken cancellationToken)
        {
            var community = await _communities.GetCommunityByInviteCode(request.InviteCode, cancellationToken);
            if (community == null) throw new CommunityNotFoundException();
            await Handle(new JoinCommunityCommand(community.Value, request.InviteCode), cancellationToken);
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

        public async Task Handle(AddDiscordChannelToCommunityCommand request, CancellationToken cancellationToken)
        {
            var community = await LoadCommunity(request.CommunityName, request.InviteCode, cancellationToken);

            foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId).ToArray())
                community.Channels.Remove(existingChannel);

            community.Channels.Add(new Community.ChannelConfiguration(request.ChannelId, request.SendScores,
                request.SendTitles, request.SendNewMembers));
            await _communities.SaveCommunity(community, cancellationToken);

            await _bot.SendMessage(
                $"This channel was updated to receive notifications for the {community.Name} community in PIU Scores!",
                request.ChannelId, cancellationToken);
        }

        private async Task SendToCommunityDiscords(Guid userId, string message, CancellationToken cancellationToken)
        {
            var communities =
                await _communities.GetCommunities(userId, cancellationToken);
            foreach (var communityName in communities.Select(c => c.CommunityName))
            {
                var community = await _communities.GetCommunityByName(communityName, cancellationToken);
                if (community == null) continue;

                var channelIds = community.Channels.Where(s => s.SendNewScores).Select(c => c.ChannelId);
                await _bot.SendMessages(new[] { message }, channelIds, cancellationToken);
            }
        }

        public async Task Consume(ConsumeContext<PlayerRatingsImprovedEvent> context)
        {
            var user = await _users.GetUser(context.Message.UserId, context.CancellationToken);
            if (user == null) return;

            var message = $"**{user.Name}**'s top 50 rating has improved!";
            if (context.Message.NewTop50 > context.Message.OldTop50)
                message += $@"
- Top 50 improved to {context.Message.NewTop50} (+{context.Message.NewTop50 - context.Message.OldTop50})";
            if (context.Message.NewSinglesTop50 > context.Message.OldSinglesTop50)
                message += $@"
- Top 50 Singles to {context.Message.NewSinglesTop50} (+{context.Message.NewSinglesTop50 - context.Message.OldSinglesTop50})";
            if (context.Message.NewDoublesTop50 > context.Message.OldDoublesTop50)
                message += $@"
- Top 50 Doubles improved to {context.Message.NewDoublesTop50} (+{context.Message.NewDoublesTop50 - context.Message.OldDoublesTop50})";


            if (context.Message.NewCompetitive > context.Message.OldCompetitive &&
                context.Message.NewCompetitive.ToString("0.00000") !=
                context.Message.OldCompetitive.ToString("0.00000"))
                message += $@"
- Competitive Level improved to {context.Message.NewCompetitive:0.00000} (+{context.Message.NewCompetitive - context.Message.OldCompetitive:0.00000})";
            if (context.Message.NewSinglesCompetitive > context.Message.OldSinglesCompetitive &&
                context.Message.NewSinglesCompetitive.ToString("0.00000") !=
                context.Message.OldSinglesCompetitive.ToString("0.00000"))
                message += $@"
- Singles Competitive Level improved to {context.Message.NewSinglesCompetitive:0.00000} (+{context.Message.NewSinglesCompetitive - context.Message.OldSinglesCompetitive:0.00000})";
            if (context.Message.NewDoublesCompetitive > context.Message.OldDoublesCompetitive &&
                context.Message.NewDoublesCompetitive.ToString("0.00000") !=
                context.Message.OldDoublesCompetitive.ToString("0.00000"))
                message += $@"
- Doubles Competitive Level improved to {context.Message.NewDoublesCompetitive:0.00000} (+{context.Message.NewDoublesCompetitive - context.Message.OldDoublesCompetitive:0.00000})";
            await SendToCommunityDiscords(context.Message.UserId, message, context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<PlayerScoreUpdatedEvent> context)
        {
            var newChartIds = context.Message.NewChartIds.Distinct().ToArray();
            var upscoreChartScores = context.Message.UpscoredChartIds;
            var user = await _users.GetUser(context.Message.UserId, context.CancellationToken);
            if (user == null) return;
            var scores =
                (await _scores.GetRecordedScores(context.Message.UserId, context.CancellationToken))
                .Where(s => s.Score != null)
                .ToDictionary(s =>
                    s.ChartId);
            var charts = (await _charts.GetCharts(MixEnum.Phoenix,
                chartIds: newChartIds.Concat(upscoreChartScores.Keys).Distinct(),
                cancellationToken: context.CancellationToken)).ToDictionary(c => c.Id);


            var newCharts = newChartIds.Where(c => scores.TryGetValue(c, out var score) && score.Score != null)
                .Select(id => charts[id])
                .ToArray();

            var count = newCharts.Count();
            var message = "";
            if (count > 0)
            {
                message += $"**{user.Name}** passed:";
                foreach (var chart in newCharts.OrderByDescending(c => c.Level)
                             .ThenByDescending(c => (int)(scores[c.Id].Score ?? 0)).Take(5))
                    message += $@"
- {chart.Song.Name} {chart.DifficultyString}: {(int)scores[chart.Id].Score!.Value:N0} ({scores[chart.Id].Score!.Value.LetterGrade.GetName()}) {scores[chart.Id].Plate?.GetShorthand()}";
                if (count > 5)
                    message += $@"
And {count - 5} others!";
            }

            var upscoreCharts = upscoreChartScores
                .Where(kv => scores.TryGetValue(kv.Key, out var score) && score.Score != null)
                .Select(kv => (charts[kv.Key], kv.Value)).ToArray();
            count = upscoreCharts.Count();
            if (count > 0)
            {
                if (!string.IsNullOrWhiteSpace(message))
                    message += @"
";
                message += $"**{user.Name}** upscored:";
                foreach (var item in upscoreCharts.OrderByDescending(c => c.Item1.Level)
                             .ThenByDescending(c => upscoreChartScores[c.Item1.Id] - scores[c.Item1.Id].Score!.Value))
                {
                    message += $@"
- {item.Item1.Song.Name} {item.Item1.DifficultyString}: {(int)scores[item.Item1.Id].Score!.Value:N0} ({(scores[item.Item1.Id].Score!.Value - item.Value < 0 ? '-' : '+')}{scores[item.Item1.Id].Score!.Value - item.Value:N0} ";
                    var oldLetter = PhoenixScore.From(item.Value).LetterGrade;
                    if (oldLetter != scores[item.Item1.Id].Score!.Value.LetterGrade)
                        message +=
                            $"{oldLetter.GetName()} > ";

                    message +=
                        $"{scores[item.Item1.Id].Score!.Value.LetterGrade.GetName()}) {scores[item.Item1.Id].Plate?.GetShorthand()}";
                }
            }

            if (string.IsNullOrWhiteSpace(message)) return;
            await SendToCommunityDiscords(user.Id, message, context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<NewTitlesAcquiredEvent> context)
        {
            var user = await _users.GetUser(context.Message.UserId, context.CancellationToken);
            var message = $"**{user.Name}** completed the Titles:";
            foreach (var title in context.Message.Titles.OrderBy(t => t))
                message += $@"
- {title}";

            await SendToCommunityDiscords(user.Id, message, context.CancellationToken);
        }

        public async Task Handle(RemoveDiscordChannelFromCommunityCommand request, CancellationToken cancellationToken)
        {
            var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken) ??
                            throw new CommunityNotFoundException();

            foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId).ToArray())
                community.Channels.Remove(existingChannel);

            await _communities.SaveCommunity(community, cancellationToken);

            await _bot.SendMessage(
                $"This channel was **removed** to receive notifications for the {community.Name} community in PIU Scores",
                request.ChannelId, cancellationToken);
        }
    }
}

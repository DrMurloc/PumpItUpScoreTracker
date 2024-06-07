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
using ScoreTracker.PersonalProgress.Queries;

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
        IRequestHandler<GetPhoenixRecordsForCommunityQuery, IEnumerable<UserPhoenixScore>>,
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
        private readonly IMediator _mediator;

        public CommunitySaga(ICurrentUserAccessor currentUser, ICommunityRepository communities, IBotClient bot,
            IUserRepository users, IChartRepository charts, IPhoenixRecordRepository scores, IMediator mediator)
        {
            _currentUser = currentUser;
            _communities = communities;
            _bot = bot;
            _users = users;
            _charts = charts;
            _scores = scores;
            _mediator = mediator;
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
            if (community.PrivacyType == CommunityPrivacyType.Private && !(_currentUser.IsLoggedIn &&
                                                                           community.MemberIds.Contains(_currentUser
                                                                               .User.Id)))
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
            if (_currentUser.IsLoggedIn)
                return await _communities.GetCommunities(_currentUser.User.Id, cancellationToken);

            return await _communities.GetPublicCommunities(cancellationToken);
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

        private async Task SendToCommunityDiscords(Guid userId, string messages, CancellationToken cancellationToken)
        {
            SendToCommunityDiscords(userId, new[] { messages }, cancellationToken);
        }

        private async Task SendToCommunityDiscords(Guid userId, string[] messages, CancellationToken cancellationToken)
        {
            var communities =
                await _communities.GetCommunities(userId, cancellationToken);
            foreach (var communityName in communities.Select(c => c.CommunityName))
            {
                var community = await _communities.GetCommunityByName(communityName, cancellationToken);
                if (community == null) continue;

                var channelIds = community.Channels.Where(s => s.SendNewScores).Select(c => c.ChannelId);
                foreach (var message in messages)
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
                context.Message.NewCompetitive.ToString("0.000") !=
                context.Message.OldCompetitive.ToString("0.000"))
                message += $@"
- Competitive Level improved to {context.Message.NewCompetitive:0.00000} (+{context.Message.NewCompetitive - context.Message.OldCompetitive:0.000})";
            if (context.Message.NewSinglesCompetitive > context.Message.OldSinglesCompetitive &&
                context.Message.NewSinglesCompetitive.ToString("0.000") !=
                context.Message.OldSinglesCompetitive.ToString("0.000"))
                message += $@"
- Singles Competitive Level improved to {context.Message.NewSinglesCompetitive:0.000} (+{context.Message.NewSinglesCompetitive - context.Message.OldSinglesCompetitive:0.000})";
            if (context.Message.NewDoublesCompetitive > context.Message.OldDoublesCompetitive &&
                context.Message.NewDoublesCompetitive.ToString("0.000") !=
                context.Message.OldDoublesCompetitive.ToString("0.000"))
                message += $@"
- Doubles Competitive Level improved to {context.Message.NewDoublesCompetitive:0.000} (+{context.Message.NewDoublesCompetitive - context.Message.OldDoublesCompetitive:0.000})";
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
            var messages = new List<string>();
            var message = "";

            var top50 = (await _mediator.Send(new GetTop50CompetitiveQuery(context.Message.UserId, ChartType.Single)))
                .Concat(await _mediator.Send(new GetTop50CompetitiveQuery(context.Message.UserId, ChartType.Double)))
                .Select(c => c.ChartId).Distinct().ToHashSet();

            if (count > 0)
            {
                message += $"**{user.Name}** passed:";
                foreach (var chart in newCharts.OrderByDescending(c => c.Level)
                             .ThenByDescending(c => (int)(scores[c.Id].Score ?? 0)).Take(10))
                {
                    var crown = top50.Contains(chart.Id) ? " :crown:" : "";
                    message += $@"
#DIFFICULTY|{chart.DifficultyString}#{crown} {chart.Song.Name}: {(int)scores[chart.Id].Score!.Value:N0} #LETTERGRADE|{scores[chart.Id].Score!.Value.LetterGrade}##PLATE|{scores[chart.Id].Plate}#";
                }

                if (count > 10)
                    message += $@"
And {count - 10} others!";
                messages.Add(message);
                message = "";
                foreach (var (type, level) in newCharts.GroupBy(c => (c.Type, c.Level)).Select(c => c.Key)
                             .OrderByDescending(g => g.Level).ThenBy(g => g.Type))
                {
                    var totalCount = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix, level, type))).Count();
                    var currentCount = await _scores.GetClearCount(context.Message.UserId, type, level,
                        context.CancellationToken);
                    message += $@"
#DIFFICULTY|{type.GetShortHand()}{level}# {currentCount}/{totalCount} ({100.0 * currentCount / totalCount:0.0}%)";
                }

                messages.Add(message);
            }


            message = "";
            var upscoreCharts = upscoreChartScores
                .Where(kv => scores.TryGetValue(kv.Key, out var score) && score.Score != null)
                .Select(kv => (charts[kv.Key], kv.Value)).ToArray();
            count = upscoreCharts.Count();
            if (count > 0)
            {
                message += $"**{user.Name}** upscored:";
                var current = 0;
                foreach (var item in upscoreCharts.OrderByDescending(c => c.Item1.Level)
                             .ThenByDescending(c => upscoreChartScores[c.Item1.Id] - scores[c.Item1.Id].Score!.Value))
                {
                    current++;
                    if (current == 10)
                    {
                        messages.Add(message);
                        current = 1;
                        message = "";
                    }

                    var crown = top50.Contains(item.Item1.Id) ? " :crown:" : "";
                    message += $@"
#DIFFICULTY|{item.Item1.DifficultyString}#{crown} {item.Item1.Song.Name}: {(int)scores[item.Item1.Id].Score!.Value:N0}
  {(scores[item.Item1.Id].Score!.Value - item.Value < 0 ? '-' : '+')}{scores[item.Item1.Id].Score!.Value - item.Value:N0} ";
                    var oldLetter = PhoenixScore.From(item.Value).LetterGrade;
                    if (oldLetter != scores[item.Item1.Id].Score!.Value.LetterGrade)
                        message +=
                            $"#LETTERGRADE|{oldLetter}# > ";

                    message +=
                        $"#LETTERGRADE|{scores[item.Item1.Id].Score!.Value.LetterGrade}##PLATE|{scores[item.Item1.Id].Plate}#";
                }
            }

            if (!string.IsNullOrWhiteSpace(message)) messages.Add(message);
            if (!messages.Any()) return;
            await SendToCommunityDiscords(user.Id, messages.ToArray(), context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<NewTitlesAcquiredEvent> context)
        {
            var user = await _users.GetUser(context.Message.UserId, context.CancellationToken);
            var message = string.Empty;
            if (context.Message.NewTitles.Any())
            {
                var count = 0;
                message = $"**{user.Name}** completed the Titles:";
                foreach (var title in context.Message.NewTitles.OrderBy(t => t))
                {
                    message += $@"
- {title}";

                    count++;
                    if (count != 10) continue;

                    await SendToCommunityDiscords(user.Id, message,
                        context.CancellationToken);
                    message = "";
                    count = 0;
                }

                await SendToCommunityDiscords(user.Id, message,
                    context.CancellationToken);
            }

            if (context.Message.ParagonUpgrades.Any())
            {
                message = $"**{user.Name}** Advanced their Paragon Title Levels:";
                var count = 0;
                foreach (var upgradedTitle in context.Message.ParagonUpgrades.OrderBy(t => t))
                {
                    var emoji = upgradedTitle.Value == "PG"
                        ? "#PLATE|PerfectGame#"
                        : "#LETTERGRADE|" + upgradedTitle.Value + "#";
                    message += $@"
- {upgradedTitle} {emoji}";
                    count++;
                    if (count != 10) continue;

                    await SendToCommunityDiscords(user.Id,
                        message, context.CancellationToken);
                }

                await SendToCommunityDiscords(user.Id,
                    message, context.CancellationToken);
            }
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

        public async Task<IEnumerable<UserPhoenixScore>> Handle(GetPhoenixRecordsForCommunityQuery request,
            CancellationToken cancellationToken)
        {
            var community = await GetCommunity(request.CommuityName, cancellationToken);
            return await _scores.GetPhoenixScores(community.MemberIds, request.ChartId, cancellationToken);
        }
    }
}

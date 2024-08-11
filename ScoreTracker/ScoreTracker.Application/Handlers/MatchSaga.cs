using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Events;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Handlers;

public sealed class MatchSaga : IRequestHandler<GetMatchQuery, MatchView>,
    IRequestHandler<UpdateMatchCommand>,
    IRequestHandler<DrawChartsCommand>,
    IRequestHandler<ResolveMatchCommand>,
    IRequestHandler<GetAllMatchesQuery, IEnumerable<MatchView>>,
    IRequestHandler<SaveRandomSettingsCommand>,
    IRequestHandler<GetAllRandomSettingsQuery, IEnumerable<(Name name, RandomSettings settings)>>,
    IRequestHandler<GetMatchLinksQuery, IEnumerable<MatchLink>>,
    IRequestHandler<GetMatchLinksFromMatchQuery, IEnumerable<MatchLink>>,
    IRequestHandler<CreateMatchLinkCommand>,
    IRequestHandler<DeleteMatchLinkCommand>,
    IRequestHandler<FinishCardDrawCommand>,
    IRequestHandler<PingMatchCommand>,
    IRequestHandler<GetMatchPlayersQuery, IEnumerable<MatchPlayer>>,
    IRequestHandler<FinalizeMatchCommand>,
    IRequestHandler<UpdateMatchScoresCommand>

{
    private readonly IAdminNotificationClient _admins;
    private readonly IBotClient _bot;
    private readonly IChartRepository _charts;
    private readonly IMatchRepository _matchRepository;
    private readonly IMediator _mediator;
    private readonly IQualifiersRepository _qualifiers;

    public MatchSaga(IMediator mediator, IMatchRepository matchRepository, IAdminNotificationClient admins,
        IChartRepository charts, IBotClient bot, IQualifiersRepository tournaments)
    {
        _matchRepository = matchRepository;
        _mediator = mediator;
        _charts = charts;
        _admins = admins;
        _bot = bot;
        _qualifiers = tournaments;
    }

    public async Task Handle(CreateMatchLinkCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveMatchLink(request.TournamentId, request.MatchLink, cancellationToken);
    }

    public async Task Handle(DeleteMatchLinkCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.DeleteMatchLink(request.LinkId,
            cancellationToken);
    }

    private static readonly Guid[] EasyCoOp =
    {
        new("f63df608-c82e-4e6c-b276-3766e49ad092"),
        new("78d4f01f-ac2a-4649-92fe-a3a4fa0d3e7d"),
        new("560d5400-7ef7-44be-8bd2-b986cf0667ad"),
        new("f5b275a8-1659-4245-9904-784696035a90"),
        new("97466c03-968f-4080-b392-dd03a6d3dd1a"),
        new("fa1c4725-4b2c-4d76-8737-6095e43aa36e"),
        new("817e653f-ca5b-41e5-83b6-448ff5e2f14c"),
        new("85db4afb-f0bf-4f83-bd21-55db4d4adcc9"),
        new("ed9bd8be-5f8c-42f8-940d-ea558565e4e3"),
        new("bdd46c83-e1c5-4c2a-928a-d96f46997e11"),
        new("cd97483b-8c7b-430a-9d5d-c1b5fa47a56d"),
        new("a93f4c00-c208-453e-ad8a-8dd270cd573c"),
        new("62378562-d56e-4dfb-a73d-c939e158ebb2"),
        new("d257f374-8f18-4883-8899-68fccec7471b"),
        new("e7b81d3b-d00e-4067-b3e0-edd7f44eba1a"),
        new("177b4e75-7c2a-4299-8867-6f6b90791738"),
        new("176cb495-7f10-4046-ac71-c728537a2815"),
        new("1e0816bc-3c5e-430f-bbb4-15f8a3a2c71c"),
        new("b3dc9799-a2b0-48ad-8c03-4548b26464da"),
        new("a2474465-3eee-4255-9089-0678da56a673"),
        new("6a6c36d1-1859-4749-afd5-24b9d01f56db"),
        new("cd97483b-8c7b-430a-9d5d-c1b5fa47a56d"),
        new("3878a273-b5bf-4a81-9fc0-c9e1c0626d87"),
        new("852bb004-efb5-4750-90f7-36158c72c277"),
        new("9860c1e1-3d6b-427f-9d40-6f3e6e7b8d42"),
        new("3c7878d6-c383-46f0-902b-1400b93af878"),
        new("20371c59-c0c1-43b2-b6b4-61d0adec525d")
    };

    private static readonly Guid[] MediumCoOp =
    {
        new("2193c69a-600d-4add-8d0f-dde170c65478"),
        new("bd0fc35b-9b22-4b6d-8a79-f62b6f0120e1"),
        new("8afd3e97-1ac9-47a8-a04c-8fe1d8d0994e"),
        new("82bb4db1-a89b-48bd-9186-6ae10aa6e975"),
        new("755e6413-ca17-4c1e-b519-595fc4e6a148"),
        new("60a8d02b-4817-47a1-a368-716f91ab747f"),
        new("49e71c60-7fc7-43c4-9027-e1158bd29e2c"),
        new("a76c05b6-6b8d-4031-91d0-739777e1cf07"),
        new("6a6f554c-ad84-4144-948c-acd57ef9eabf"),
        new("53ec5f4e-bda0-4b1d-bb2b-8adc933535b5"),
        new("3f4adb80-ed5e-460d-bd70-a02dd3967d6c"),
        new("0840ec69-009a-475f-9c83-6fe5196b0194"),
        new("baae472a-d6aa-4b42-8cb7-059f8c36ce1d"),
        new("8c370fc7-67ea-4d49-a18d-0232217fcf29"),
        new("dd3df26b-e089-4733-bdf6-485c78efddf7"),
        new("f0b650dc-4f71-41af-85f6-9715c86efbd4"),
        new("a8607284-8ff8-4aaf-b189-7c260beb8b0d"),
        new("05f00403-b1d4-4623-b445-800415a6d2db"),
        new("23658923-92d7-4fa5-beab-6a120c6ed8a8"),
        new("179dfb00-4d47-4b46-93a7-79d973425889"),
        new("78da9a72-fb22-40f2-95bf-a1ee813998ec"),
        new("907f270d-fe70-4adb-9422-9635b19fc9bf"),
        new("cefdfda8-111e-415c-9d52-70e409af98a7"),
        new("f8569c0a-e8c0-496e-ab65-b6f492c53583"),
        new("ff6792fd-8af7-459f-85af-a60408fe00a8"),
        new("04e8e48b-7eb9-4079-9aff-df4a9d4a1143"),
        new("0c43f1c9-b26a-447a-9133-364269bfcf29"),
        new("993e6919-db56-43ea-985d-15f635d073fb"),
        new("fa7f75c0-e1a3-4551-b700-6834dd131bd9"),
        new("8898878e-8fc5-4b69-9737-d12f9e298218"),
        new("fae3d979-f581-4bba-8df7-856e9d26fd2f"),
        new("e9f37c9e-87cb-4497-ac08-8c8adb6d1086"),
        new("3de8c1d3-c1dc-465e-a486-6bd469f7ab5b"),
        new("1a0ee10f-b65e-4737-a722-76533e8739ff")
    };

    private static readonly Guid[] HardCoOp =
    {
        new("0b176126-df68-4235-9b33-ab61d63681b0"),
        new("7978d598-6fea-4342-9ed5-7d949403f00e"),
        new("76bd2d32-9e67-4fd2-8ddb-fd0dd6fb05cb"),
        new("7312785c-5f52-4b47-8000-e05caab81e91"),
        new("864d6818-6a02-464d-b183-f649517a4d50"),
        new("aac2b8a4-d8b2-4577-8266-06fcd8973fd0"),
        new("961e5794-e285-49b2-9d94-bf21f22edd86"),
        new("a5f97101-9c1b-4236-b1d6-9b11018f2790"),
        new("dfc9dc45-f94e-4cb3-a0f3-4ca765e05160"),
        new("e78ca23e-ebcc-46c7-afb2-69babb9fafb1"),
        new("64ede87b-2f99-483d-9ad9-aeeff868c6df"),
        new("496f7bbb-fb3f-49c7-9b4a-59877c291fe4"),
        new("0c19072c-5cdd-4f0d-8fd2-deb16e5a4345"),
        new("48164357-edbe-40ca-aa95-5a54ec1fff8a"),
        new("4bfcfac6-927a-49cb-8b28-3322a62eb8d4"),
        new("8d0d24c6-b055-4432-87c6-0cc189bd0dd4"),
        new("5acf4c94-7a0a-4c43-a0e4-f153d7c5338b"),
        new("e9e96c7b-dc6a-431a-a8f6-6003d2d9acd9"),
        new("93320007-889c-41a8-872d-94ccca1f4a03"),
        new("2caa9d80-c967-472d-8613-6e9b2ab5890c")
    };

    private static readonly Guid[] VeryHardCoOp =
    {
        new("27be13db-57ab-4d5e-8312-0bcff217789a"),
        new("56b09f28-dfc5-4cb3-9448-3258d2ffae9a"),
        new("ad379aad-d756-459d-aa61-508ed263e34a"),
        new("7eb6c4b3-8a9a-46f6-becd-1f45e62842d2"),
        new("e5b77789-c929-4e12-9cc6-7c074ceb0520"),
        new("5abbb966-02c5-4a41-a0ad-548368539a7c"),
        new("1c83fc3d-6847-4f4c-a510-7e10d7a94b61"),
        new("e45cc045-cc81-4936-b5c6-5c80dbadff88"),
        new("cd8b431a-d7dd-41e6-a81b-cbeb763f2f4c"),
        new("c0db8cc8-28f7-48bd-a3a3-49a3efc089ec"),
        new("5ca6fb97-6d1c-4544-8864-5cbc1ea52ffa"),
        new("eaf46dd9-0a68-42d4-aefa-6dbd88a3177a")
    };

    public async Task Handle(DrawChartsCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);

        var settings =
            await _matchRepository.GetRandomSettings(request.TournamentId, match.RandomSettings, cancellationToken);
        switch (match.RandomSettings.ToString())
        {
            case "CoOp Easy":
                settings.ChartIds = EasyCoOp.Distinct().ToHashSet();
                break;
            case "CoOp Medium":
                settings.ChartIds = MediumCoOp.Distinct().ToHashSet();
                break;
            case "CoOp Easy+Medium":
                settings.ChartIds = EasyCoOp.Concat(MediumCoOp).Distinct().ToHashSet();
                break;
            case "CoOp Hard":
                settings.ChartIds = HardCoOp.Distinct().ToHashSet();
                break;
            case "CoOp Very Hard":
                settings.ChartIds = VeryHardCoOp.Distinct().ToHashSet();
                break;
        }

        var charts = await _mediator.Send(new GetRandomChartsQuery(settings), cancellationToken);
        var newMatch = match with
        {
            ProtectedCharts = Array.Empty<Guid>(),
            VetoedCharts = Array.Empty<Guid>(),
            ActiveCharts = charts.Select(c => c.Id).ToArray(), State = MatchState.CardDraw,

            LastUpdated = match.State == MatchState.CardDraw ? match.LastUpdated : DateTimeOffset.Now
        };
        await _matchRepository.SaveMatch(request.TournamentId, newMatch, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, newMatch), cancellationToken);
    }

    public async Task Handle(FinalizeMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);

        var links = await _matchRepository.GetMatchLinksByFromMatchName(request.TournamentId, match.MatchName,
            cancellationToken);
        foreach (var link in links)
        {
            var nextMatch = await _matchRepository.GetMatch(request.TournamentId, link.ToMatch, cancellationToken);
            var progressers = link.IsWinners
                ? match.FinalPlaces.Skip(link.Skip).Take(link.PlayerCount)
                : match.FinalPlaces.Reverse().Skip(link.Skip).Take(link.PlayerCount);

            foreach (var player in progressers)
            {
                if (nextMatch.Players.Contains(player)) continue;
                var nextIndex = nextMatch.Players.Select((p, i) => (p, i))
                    .Where(p => p.p.ToString().StartsWith("Unknown ", StringComparison.OrdinalIgnoreCase))
                    .Select(p => (int?)p.i)
                    .FirstOrDefault();
                if (nextIndex == null)
                {
                    await _admins.NotifyAdmin(
                        $"Player {player} Couldn't progress from {match.MatchName} to {nextMatch.MatchName}",
                        cancellationToken);
                    continue;
                }

                nextMatch.Players[nextIndex.Value] = player;
            }

            await _matchRepository.SaveMatch(request.TournamentId, nextMatch, cancellationToken);
            await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, nextMatch), cancellationToken);
        }

        var newMatchState = match with
        {
            State = MatchState.Completed,
            LastUpdated = match.State == MatchState.Completed ? match.LastUpdated : DateTimeOffset.Now
        };
        await _matchRepository.SaveMatch(request.TournamentId, newMatchState, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, newMatchState), cancellationToken);


        try
        {
            var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);

            await _bot.SendMessage($@"# {newMatchState.MatchName} Completed! #
- {string.Join("\r\n- ", newMatchState.FinalPlaces.Select(p => $"{p} ({newMatchState.Points[p].Sum()})"))}",
                config.NotificationChannel,
                cancellationToken);
        }
        catch (Exception)
        {
            //Ignored
        }
    }

    public async Task Handle(FinishCardDrawCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var updatedMatch = match with
        {
            State = MatchState.Ready,
            LastUpdated = match.State == MatchState.Ready ? match.LastUpdated : DateTimeOffset.Now,
            Scores = match.Players.ToDictionary(p => p.ToString(),
                p => match.ActiveCharts.Select(c => PhoenixScore.From(0)).ToArray()),
            Points = match.Players.ToDictionary(p => p.ToString(), p => match.ActiveCharts.Select(c => 0).ToArray())
        };
        await _matchRepository.SaveMatch(request.TournamentId, updatedMatch, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, updatedMatch), cancellationToken);

        var charts = await _charts.GetCharts(MixEnum.Phoenix, chartIds: updatedMatch.ActiveCharts,
            cancellationToken: cancellationToken);
        var vetoes = await _charts.GetCharts(MixEnum.Phoenix, chartIds: updatedMatch.VetoedCharts,
            cancellationToken: cancellationToken);
        try
        {
            var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);

            await _bot.SendMessage($@"# {updatedMatch.MatchName} Card Draw Complete! #
({string.Join(", ", updatedMatch.Players)})
- {string.Join("\r\n- ", charts.Select(c => c.Song.Name + " " + c.DifficultyString))}
- {string.Join("\r\n- ", vetoes.Select(v => "~~" + v.Song.Name + " " + v.DifficultyString + "~~"))}",
                config.NotificationChannel,
                cancellationToken);
        }
        catch (Exception)
        {
            //Ignored
        }
    }

    public async Task<IEnumerable<MatchView>> Handle(GetAllMatchesQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllMatches(request.TournamentId, cancellationToken);
    }

    public async Task<IEnumerable<(Name name, RandomSettings settings)>> Handle(GetAllRandomSettingsQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllRandomSettings(request.TournamentId, cancellationToken);
    }

    public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksFromMatchQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatchLinksByFromMatchName(request.TournamentId, request.FromMatchName,
            cancellationToken);
    }

    public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllMatchLinks(request.TournamentId, cancellationToken);
    }

    public async Task<IEnumerable<MatchPlayer>> Handle(GetMatchPlayersQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatchPlayers(request.TournamentId, cancellationToken);
    }

    public async Task<MatchView> Handle(GetMatchQuery request, CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
    }

    public async Task Handle(PingMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);

        var message = $"The PIU TOs are requesting the players for {match.MatchName}";

        var playerDiscords = (await _matchRepository.GetMatchPlayers(request.TournamentId, cancellationToken))
            .ToDictionary(p => p.Name.ToString(), p => p.DiscordId, StringComparer.OrdinalIgnoreCase);
        if (match.State == MatchState.NotStarted && match.Players.All(p => !p.ToString().StartsWith("Unknown ")))
            message = $"Card draw is ready for {match.MatchName}";
        if (match.State == MatchState.Ready)
            message = $"{match.MatchName} is ready to play!";
        var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);

        await _bot.SendMessage(
            $@"{message}
{string.Join(", ", match.Players.Where(p => !p.ToString().StartsWith("Unknown ")).Select(p => $"<@{playerDiscords[p]}> ({p})"))}, please report to PIU TOs",
            config.NotificationChannel,
            cancellationToken);
    }

    public async Task Handle(ResolveMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var newMatchState = match.CalculatePoints() with
        {
            State = MatchState.Finalizing,
            LastUpdated = match.State == MatchState.Finalizing ? match.LastUpdated : DateTimeOffset.Now
        };
        await _matchRepository.SaveMatch(request.TournamentId, newMatchState, cancellationToken);

        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, newMatchState), cancellationToken);
    }

    public async Task Handle(SaveRandomSettingsCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveRandomSettings(request.TournamentId, request.SettingsName, request.Settings,
            cancellationToken);
    }

    public async Task Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveMatch(request.TournamentId, request.NewView, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, request.NewView), cancellationToken);
    }

    public async Task Handle(UpdateMatchScoresCommand request, CancellationToken cancellationToken)
    {
        var oldMatch = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var songWasComplete = oldMatch.Scores.Values.All(s => s[request.ChartIndex] > 0);
        var newScores = oldMatch.Scores.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        newScores[request.Player][request.ChartIndex] = request.NewScore;
        var match = oldMatch with { Scores = newScores };
        match = match.CalculatePoints();

        await _matchRepository.SaveMatch(request.TournamentId, match, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, match), cancellationToken);
        if (songWasComplete) return;

        var songIsComplete = match.Scores.Values.All(s => s[request.ChartIndex] > 0);

        if (!songIsComplete) return;
        var chart = await _charts.GetChart(MixEnum.Phoenix, match.ActiveCharts[request.ChartIndex],
            cancellationToken);

        var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
        var message = $@"**{match.MatchName}**
{chart.Song.Name} #DIFFICULTY|{chart.DifficultyString}# Completed:";
        foreach (var player in match.Players.OrderByDescending(p => match.Scores[p][request.ChartIndex]))
        {
            var score = match.Scores[player][request.ChartIndex];
            message +=
                @$"
- {player} - {score} #LETTERGRADE|{score.LetterGrade}#  ({match.Points[player][request.ChartIndex]} Points)";
        }

        await _bot.SendMessage(
            message,
            config.NotificationChannel,
            cancellationToken);
    }
}
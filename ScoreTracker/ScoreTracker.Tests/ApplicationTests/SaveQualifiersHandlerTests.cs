using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SaveQualifiersHandlerTests
{
    private const ulong NotificationChannel = 12345UL;

    private static QualifiersConfiguration Config(IEnumerable<Chart> charts, int playCount = 2) =>
        new(charts, new Dictionary<Guid, int>(), Name.From("Score"), NotificationChannel, playCount, null, false);

    private static UserQualifiers BuildEntry(QualifiersConfiguration config, string userName,
        IDictionary<Guid, PhoenixScore> submissions)
    {
        var entry = new UserQualifiers(config, false, Name.From(userName), Guid.NewGuid(),
            new Dictionary<Guid, UserQualifiers.Submission>());
        foreach (var kv in submissions)
            entry.AddPhoenixScore(kv.Key, kv.Value, null);
        return entry;
    }

    [Fact]
    public async Task PersistsQualifiersAndAnnouncesFirstSubmissionAsNewChallenger()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var tournamentId = Guid.NewGuid();
        var qualifiers = BuildEntry(config, "newcomer", new Dictionary<Guid, PhoenixScore> { [chart.Id] = 950000 });

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var bot = new Mock<IBotClient>();

        // Old leaderboard: empty (no prior submission for this user)
        qualifiersRepo.SetupSequence(r =>
                r.GetAllUserQualifiers(tournamentId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserQualifiers>())
            .ReturnsAsync(new[] { qualifiers });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var handler = new SaveQualifiersHandler(qualifiersRepo.Object, bot.Object);

        await handler.Handle(new SaveQualifiersCommand(tournamentId, qualifiers), CancellationToken.None);

        qualifiersRepo.Verify(
            r => r.SaveQualifiers(tournamentId, qualifiers, It.IsAny<CancellationToken>()),
            Times.Once);
        bot.Verify(
            b => b.SendMessage(It.Is<string>(m => m.Contains("new challenger") && m.Contains("newcomer")),
                NotificationChannel, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnnouncesProgressionWhenLeaderboardPlaceImproves()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart }, playCount: 1);
        var tournamentId = Guid.NewGuid();

        // Other player at 970,000 — top of board initially
        var leader = BuildEntry(config, "leader", new Dictionary<Guid, PhoenixScore> { [chart.Id] = 970000 });
        // Our player starts at 900,000 (2nd) and improves to 990,000 (1st)
        var playerId = Guid.NewGuid();
        var oldPlayer = new UserQualifiers(config, false, Name.From("hero"), playerId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        oldPlayer.AddPhoenixScore(chart.Id, 900000, null);
        var newPlayer = new UserQualifiers(config, false, Name.From("hero"), playerId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        newPlayer.AddPhoenixScore(chart.Id, 990000, null);

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var bot = new Mock<IBotClient>();
        qualifiersRepo.SetupSequence(r =>
                r.GetAllUserQualifiers(tournamentId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { leader, oldPlayer })
            .ReturnsAsync(new[] { newPlayer, leader });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var handler = new SaveQualifiersHandler(qualifiersRepo.Object, bot.Object);

        await handler.Handle(new SaveQualifiersCommand(tournamentId, newPlayer), CancellationToken.None);

        bot.Verify(
            b => b.SendMessage(It.Is<string>(m => m.Contains("hero") && m.Contains("progressed to 1")),
                NotificationChannel, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DoesNotAnnounceWhenSubmissionCountIsBelowConfiguredPlayCount()
    {
        // PlayCount = 3 but submitter only has 1 submission → progression branch suppressed
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chartA, chartB }, playCount: 3);
        var tournamentId = Guid.NewGuid();

        var playerId = Guid.NewGuid();
        var leader = BuildEntry(config, "leader",
            new Dictionary<Guid, PhoenixScore> { [chartA.Id] = 980000, [chartB.Id] = 970000 });
        var oldPlayer = new UserQualifiers(config, false, Name.From("hero"), playerId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        oldPlayer.AddPhoenixScore(chartA.Id, 900000, null);
        var newPlayer = new UserQualifiers(config, false, Name.From("hero"), playerId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        newPlayer.AddPhoenixScore(chartA.Id, 950000, null);

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var bot = new Mock<IBotClient>();
        qualifiersRepo.SetupSequence(r =>
                r.GetAllUserQualifiers(tournamentId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { leader, oldPlayer })
            .ReturnsAsync(new[] { leader, newPlayer });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var handler = new SaveQualifiersHandler(qualifiersRepo.Object, bot.Object);

        await handler.Handle(new SaveQualifiersCommand(tournamentId, newPlayer), CancellationToken.None);

        bot.Verify(
            b => b.SendMessage(It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

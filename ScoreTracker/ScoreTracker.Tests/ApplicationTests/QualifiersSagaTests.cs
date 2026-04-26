using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class QualifiersSagaTests
{
    private static QualifiersConfiguration Config(IEnumerable<Chart> charts) =>
        new(charts, new Dictionary<Guid, int>(), Name.From("Score"), 0, 1, null, false);

    private static Mock<ConsumeContext<RecentScoreImportedEvent>> ContextOf(RecentScoreImportedEvent message)
    {
        var ctx = new Mock<ConsumeContext<RecentScoreImportedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task SavesQualifiersWhenImportedScoreBeatsPreviousSubmission()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existing = new UserQualifiers(config, false, Name.From("hero"), userId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        existing.AddPhoenixScore(chart.Id, 900000, null);

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var userRepo = new Mock<IUserRepository>();
        var mediator = new Mock<IMediator>();

        qualifiersRepo.Setup(r => r.GetRegisteredTournaments(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tournamentId });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        qualifiersRepo.Setup(r => r.GetQualifiers(tournamentId, userId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var saga = new QualifiersSaga(qualifiersRepo.Object, userRepo.Object,
            NullLogger<QualifiersSaga>.Instance, mediator.Object);

        var entry = new RecentScoreImportedEvent.Entry(chart.Id, 950000, "PerfectGame", false);
        var message = new RecentScoreImportedEvent(userId, new[] { entry });

        await saga.Consume(ContextOf(message).Object);

        mediator.Verify(m => m.Send(
                It.Is<SaveQualifiersCommand>(c =>
                    c.TournamentId == tournamentId && c.Qualifiers.Submissions[chart.Id].Score == (PhoenixScore)950000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DoesNotSaveWhenImportedScoreIsNotBetter()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existing = new UserQualifiers(config, false, Name.From("hero"), userId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        existing.AddPhoenixScore(chart.Id, 950000, null);

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var userRepo = new Mock<IUserRepository>();
        var mediator = new Mock<IMediator>();

        qualifiersRepo.Setup(r => r.GetRegisteredTournaments(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tournamentId });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        qualifiersRepo.Setup(r => r.GetQualifiers(tournamentId, userId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var saga = new QualifiersSaga(qualifiersRepo.Object, userRepo.Object,
            NullLogger<QualifiersSaga>.Instance, mediator.Object);

        var entry = new RecentScoreImportedEvent.Entry(chart.Id, 800000, "PerfectGame", false);
        var message = new RecentScoreImportedEvent(userId, new[] { entry });

        await saga.Consume(ContextOf(message).Object);

        mediator.Verify(m => m.Send(It.IsAny<SaveQualifiersCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatesNewQualifiersEntryWhenUserHasNoneAndUserLookupSucceeds()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).WithName("hero").Build();

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var userRepo = new Mock<IUserRepository>();
        var mediator = new Mock<IMediator>();

        qualifiersRepo.Setup(r => r.GetRegisteredTournaments(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tournamentId });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        qualifiersRepo.Setup(r => r.GetQualifiers(tournamentId, userId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserQualifiers?)null);
        userRepo.Setup(r => r.GetUser(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var saga = new QualifiersSaga(qualifiersRepo.Object, userRepo.Object,
            NullLogger<QualifiersSaga>.Instance, mediator.Object);

        var entry = new RecentScoreImportedEvent.Entry(chart.Id, 900000, "PerfectGame", false);
        var message = new RecentScoreImportedEvent(userId, new[] { entry });

        await saga.Consume(ContextOf(message).Object);

        mediator.Verify(m => m.Send(
                It.Is<SaveQualifiersCommand>(c =>
                    c.TournamentId == tournamentId && c.Qualifiers.UserId == userId &&
                    c.Qualifiers.Submissions[chart.Id].Score == (PhoenixScore)900000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SkipsTournamentWhenUserLookupFailsForNewEntry()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var qualifiersRepo = new Mock<IQualifiersRepository>();
        var userRepo = new Mock<IUserRepository>();
        var mediator = new Mock<IMediator>();

        qualifiersRepo.Setup(r => r.GetRegisteredTournaments(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tournamentId });
        qualifiersRepo.Setup(r => r.GetQualifiersConfiguration(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        qualifiersRepo.Setup(r => r.GetQualifiers(tournamentId, userId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserQualifiers?)null);
        userRepo.Setup(r => r.GetUser(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var saga = new QualifiersSaga(qualifiersRepo.Object, userRepo.Object,
            NullLogger<QualifiersSaga>.Instance, mediator.Object);

        var entry = new RecentScoreImportedEvent.Entry(chart.Id, 900000, "PerfectGame", false);
        var message = new RecentScoreImportedEvent(userId, new[] { entry });

        await saga.Consume(ContextOf(message).Object);

        mediator.Verify(m => m.Send(It.IsAny<SaveQualifiersCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

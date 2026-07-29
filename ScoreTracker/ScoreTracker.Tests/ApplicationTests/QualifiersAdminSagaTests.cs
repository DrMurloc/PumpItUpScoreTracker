using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;
using ScoreTracker.EventCompetition.Infrastructure;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class QualifiersAdminSagaTests
{
    private static readonly DateTimeOffset SubmittedAt = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Uri Photo = new("https://example.invalid/shot.png");
    private static readonly Guid TournamentId = Guid.NewGuid();

    private static QualifiersConfiguration Config(IEnumerable<Chart> charts, int playCount = 2) =>
        new(charts, new Dictionary<Guid, int>(), Name.From("Score"), 0, playCount, null, false);

    private sealed record Harness(
        QualifiersAdminSaga Saga,
        Mock<IQualifiersRepository> Repository,
        Mock<IMediator> Mediator);

    private static Harness Build(QualifiersConfiguration config, IEnumerable<UserQualifiers> entries,
        TournamentRole? role, bool loggedIn = true, bool isAdmin = false, Guid? userId = null)
    {
        var actingUser = userId ?? Guid.NewGuid();
        var repository = new Mock<IQualifiersRepository>();
        var mediator = new Mock<IMediator>();
        var currentUser = new Mock<ICurrentUserAccessor>();

        currentUser.SetupGet(c => c.IsLoggedIn).Returns(loggedIn);
        currentUser.SetupGet(c => c.IsLoggedInAsAdmin).Returns(isAdmin);
        currentUser.SetupGet(c => c.User)
            .Returns(new UserBuilder().WithId(actingUser).WithName("organizer").Build());

        repository.Setup(r => r.GetQualifiersConfiguration(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        repository.Setup(r => r.GetAllUserQualifiers(TournamentId, config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        repository.Setup(r => r.GetFirstSubmissionDates(TournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, DateTimeOffset>());

        mediator.Setup(m => m.Send(It.IsAny<GetTournamentRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role == null
                ? Array.Empty<UserTournamentRole>()
                : new[] { new UserTournamentRole(TournamentId, actingUser, role.Value) });
        mediator.Setup(m => m.Send(It.IsAny<GetAllTournamentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TournamentRecord>());

        return new Harness(new QualifiersAdminSaga(repository.Object, mediator.Object, currentUser.Object),
            repository, mediator);
    }

    private static UserQualifiers Entry(QualifiersConfiguration config, string name, Guid? userId,
        params (Guid ChartId, int Score, bool Manual)[] plays)
    {
        var entry = new UserQualifiers(config, Name.From(name), userId,
            new Dictionary<Guid, UserQualifiers.Submission>());
        foreach (var play in plays)
            if (play.Manual) entry.AddManualScore(play.ChartId, play.Score, Photo, SubmittedAt);
            else entry.AddImportedScore(play.ChartId, play.Score, SubmittedAt);
        return entry;
    }

    [Fact]
    public async Task ANonOrganizerCannotReadTheAdminView()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var harness = Build(config, Array.Empty<UserQualifiers>(), role: null);

        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None));
    }

    [Fact]
    public async Task AnAssistantIsNotEnoughToReadTheAdminView()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var harness = Build(config, Array.Empty<UserQualifiers>(), TournamentRole.Assistant);

        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None));
    }

    [Fact]
    public async Task ANonOrganizerCannotDeleteAnEntry()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var harness = Build(config, Array.Empty<UserQualifiers>(), role: null);

        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            harness.Saga.Handle(new DeleteQualifierEntryCommand(TournamentId, Name.From("victim")),
                CancellationToken.None));

        harness.Repository.Verify(r => r.DeleteQualifiers(It.IsAny<Guid>(), It.IsAny<Name>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnOrganizerCanDeleteAnEntry()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var harness = Build(config, Array.Empty<UserQualifiers>(), TournamentRole.TournamentOrganizer);

        await harness.Saga.Handle(new DeleteQualifierEntryCommand(TournamentId, Name.From("dupe")),
            CancellationToken.None);

        harness.Repository.Verify(r => r.DeleteQualifiers(TournamentId,
            It.Is<Name>(n => n == Name.From("dupe")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASiteAdminIsTreatedAsAnOrganizer()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var harness = Build(config, Array.Empty<UserQualifiers>(), role: null, isAdmin: true);

        await harness.Saga.Handle(new DeleteQualifierEntryCommand(TournamentId, Name.From("dupe")),
            CancellationToken.None);

        harness.Repository.Verify(r => r.DeleteQualifiers(TournamentId, It.IsAny<Name>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletingOneSubmissionSavesTheEntryWithoutAnnouncingAPlacement()
    {
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chartA, chartB });
        var entry = Entry(config, "player", null, (chartA.Id, 950000, true), (chartB.Id, 960000, true));
        var harness = Build(config, new[] { entry }, TournamentRole.HeadTournamentOrganizer);
        harness.Repository.Setup(r => r.GetQualifiers(TournamentId, It.IsAny<Name>(), config,
            It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        await harness.Saga.Handle(
            new DeleteQualifierSubmissionCommand(TournamentId, Name.From("player"), chartA.Id),
            CancellationToken.None);

        Assert.Single(entry.Submissions);
        harness.Repository.Verify(r => r.SaveQualifiers(TournamentId, entry, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Mediator.Verify(m => m.Send(It.IsAny<SaveQualifiersCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TheAdminViewCarriesPhotosAndSourcesForEverySubmission()
    {
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chartA, chartB }, playCount: 1);
        var entry = Entry(config, "player", Guid.NewGuid(),
            (chartA.Id, 950000, true), (chartB.Id, 960000, false));
        var harness = Build(config, new[] { entry }, TournamentRole.TournamentOrganizer);

        var view = await harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None);

        var plays = view.Entries.Single().Plays;
        // Both submissions, even though only one of them counts toward the total.
        Assert.Equal(2, plays.Count);
        Assert.Contains(plays, p => p.Source == SubmissionSource.Manual && p.PhotoUrl == Photo);
        Assert.Contains(plays, p => p.Source == SubmissionSource.OfficialImport && p.PhotoUrl == null);
        Assert.Equal(1, view.PhotoCount);
    }

    [Fact]
    public async Task EntrantsWithNoScoresAreListedSeparatelyRatherThanRanked()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var scored = Entry(config, "played", Guid.NewGuid(), (chart.Id, 950000, true));
        var empty = Entry(config, "registered-only", Guid.NewGuid());
        var harness = Build(config, new[] { scored, empty }, TournamentRole.TournamentOrganizer);

        var view = await harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None);

        Assert.Single(view.Entries);
        Assert.Equal(Name.From("registered-only"), Assert.Single(view.WithoutScores));
    }

    [Fact]
    public async Task AnAnonymousEntryAlongsideTheSamePlayersAccountEntryIsFlagged()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var signedIn = Entry(config, "chezmix", Guid.NewGuid(), (chart.Id, 960000, true));
        var anonymous = Entry(config, "chez_mix", null, (chart.Id, 900000, true));
        var harness = Build(config, new[] { signedIn, anonymous }, TournamentRole.TournamentOrganizer);

        var view = await harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None);

        var group = Assert.Single(view.Duplicates);
        Assert.Equal(2, group.Entries.Count);
        // The account entry sorts first: it is the one to keep.
        Assert.True(group.Entries[0].HasAccount);
    }

    [Fact]
    public async Task TwoDistinctPlayersAreNotFlaggedAsDuplicates()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var one = Entry(config, "alpha", Guid.NewGuid(), (chart.Id, 960000, true));
        var two = Entry(config, "beta", null, (chart.Id, 900000, true));
        var harness = Build(config, new[] { one, two }, TournamentRole.TournamentOrganizer);

        var view = await harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None);

        Assert.Empty(view.Duplicates);
    }

    [Fact]
    public async Task TwoAnonymousEntriesWithTheSameNameAreNotFlagged()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chart });
        var one = Entry(config, "guest", null, (chart.Id, 960000, true));
        var two = Entry(config, "guest_", null, (chart.Id, 900000, true));
        var harness = Build(config, new[] { one, two }, TournamentRole.TournamentOrganizer);

        var view = await harness.Saga.Handle(new GetQualifiersAdminQuery(TournamentId), CancellationToken.None);

        // Without an account on one side there is nothing to reconcile them against, so this is
        // left alone rather than guessed at.
        Assert.Empty(view.Duplicates);
    }
}

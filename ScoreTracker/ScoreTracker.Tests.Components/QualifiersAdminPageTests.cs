using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MudBlazor;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;
using AdminPage = ScoreTracker.Web.Pages.Competition.QualifiersAdmin;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The organiser screen. Photos exist here and nowhere else, an imported score is marked as
///     needing none, and a non-organiser gets a refusal rather than the field.
/// </summary>
public sealed class QualifiersAdminPageTests : ComponentTestBase
{
    private static readonly Guid TournamentId = Guid.NewGuid();
    private static readonly Uri Photo = new("https://piu.test/qualifiers/shot.png");
    private static readonly DateTimeOffset FirstSeen = new(2026, 2, 6, 20, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IAdminNotificationClient> _notifications = new();
    private readonly Mock<IDateTimeOffsetAccessor> _clock = new();

    public QualifiersAdminPageTests()
    {
        _clock.SetupGet(c => c.Now).Returns(new DateTimeOffset(2026, 2, 8, 12, 0, 0, TimeSpan.Zero));
        Services.AddSingleton(_clock.Object);
        Services.AddSingleton(_notifications.Object);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddScoped<ChartScoringLevels>();
    }

    private static Chart BuildChart(string name, int level)
    {
        var song = new Song(name, SongType.Arcade, new Uri($"https://piu.test/{name}.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Double, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null);
    }

    private static QualifiersConfiguration Config(IEnumerable<Chart> charts) =>
        new(charts, new Dictionary<Guid, int>(), Name.From("Score"), 0, 2, null, false);

    private void GivenView(QualifierAdminView view)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetQualifiersAdminQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        Services.AddSingleton(_mediator.Object);
    }

    private void GivenRefused()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetQualifiersAdminQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("manage this tournament's qualifiers"));
        Services.AddSingleton(_mediator.Object);
    }

    // The confirm and photo dialogs are MudDialogs, which need a provider mounted alongside the
    // page before their content renders at all.
    private IRenderedFragment Render()
    {
        this.RenderInteractive();
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AdminPage>(1);
            builder.AddAttribute(2, nameof(AdminPage.TournamentId), TournamentId);
            builder.CloseComponent();
        });
    }

    private static QualifierAdminEntry Entry(string name, bool hasAccount,
        params QualifierAdminPlay[] plays) =>
        new(Name.From(name), hasAccount, plays.Sum(p => p.Rating), FirstSeen, plays);

    private static QualifierAdminPlay ManualPlay(Chart chart, int score = 960000, double rating = 900) =>
        new(chart, score, rating, SubmissionSource.Manual, Photo, FirstSeen);

    private static QualifierAdminPlay ImportedPlay(Chart chart, int score = 970000, double rating = 950) =>
        new(chart, score, rating, SubmissionSource.OfficialImport, null, FirstSeen);

    [Fact]
    public void ANonOrganizerIsRefusedRatherThanShownTheField()
    {
        GivenRefused();

        var page = Render();

        Assert.Contains("Qualifiers Admin Denied", page.Markup);
        Assert.Empty(page.FindAll(".qual-entry"));
        Assert.Empty(page.FindAll(".qual-tally"));
    }

    [Fact]
    public void TheTalliesCountEntriesScoresAndPhotos()
    {
        var chartA = BuildChart("Alpha", 22);
        var chartB = BuildChart("Beta", 23);
        var entry = Entry("player", true, ManualPlay(chartA), ImportedPlay(chartB));
        GivenView(new QualifierAdminView(Config(new[] { chartA, chartB }), Name.From("Test Cup"),
            new[] { entry }, new[] { Name.From("no-score") },
            Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();

        var values = page.FindAll(".qual-tally-value").Select(e => e.TextContent.Trim()).ToArray();
        // entries (1 scored + 1 without), scored, photos (only the manual one), duplicates
        Assert.Equal(new[] { "2", "1", "1", "0" }, values);
    }

    [Fact]
    public void AnImportedScoreShowsAsNeedingNoPhoto()
    {
        var chart = BuildChart("Alpha", 22);
        var entry = Entry("player", true, ImportedPlay(chart));
        GivenView(new QualifierAdminView(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();
        page.FindAll(".qual-entry-tail button")[1].Click();

        // The official site is the evidence, so there is no photo button to offer.
        Assert.NotEmpty(page.FindAll(".qual-no-photo"));
        Assert.Empty(page.FindAll("[aria-label='View Photo']"));
    }

    [Fact]
    public void AManualScoreOffersItsPhoto()
    {
        var chart = BuildChart("Alpha", 22);
        var entry = Entry("player", false, ManualPlay(chart));
        GivenView(new QualifierAdminView(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();
        page.FindAll(".qual-entry-tail button")[1].Click();

        Assert.NotEmpty(page.FindAll("[aria-label='View Photo']"));
        Assert.Empty(page.FindAll(".qual-no-photo"));
    }

    [Fact]
    public void SubmissionsStayClosedUntilTheRowIsOpened()
    {
        var chart = BuildChart("Alpha", 22);
        var entry = Entry("player", true, ManualPlay(chart));
        GivenView(new QualifierAdminView(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();

        Assert.Empty(page.FindAll(".qual-submission"));
    }

    [Fact]
    public void ADuplicateGroupMarksTheAccountEntryToKeep()
    {
        var chart = BuildChart("Alpha", 22);
        var signedIn = Entry("chezmix", true, ManualPlay(chart, 970000, 1000));
        var anonymous = Entry("chez_mix", false, ManualPlay(chart, 900000, 500));
        GivenView(new QualifierAdminView(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { signedIn, anonymous }, Array.Empty<Name>(),
            new[] { new QualifierDuplicateGroup(new[] { signedIn, anonymous }) }));

        var page = Render();

        var rows = page.FindAll(".qual-dupe-row");
        Assert.Equal(2, rows.Count);
        // Only the account entry carries the keep ring; the loose one carries the delete.
        Assert.Single(page.FindAll(".qual-dupe-keep"));
        Assert.Contains("chezmix", rows[0].TextContent);
    }

    [Fact]
    public void DeletingAnEntryConfirmsWithItsContentsFirst()
    {
        var chartA = BuildChart("Alpha", 22);
        var chartB = BuildChart("Beta", 23);
        var entry = Entry("player", false, ManualPlay(chartA), ManualPlay(chartB));
        GivenView(new QualifierAdminView(Config(new[] { chartA, chartB }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();
        page.FindAll(".qual-entry-tail button")[0].Click();

        // Nothing has been sent yet — the confirm names what would go.
        _mediator.Verify(m => m.Send(It.IsAny<DeleteQualifierEntryCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.NotEmpty(page.FindAll(".qual-danger"));
        Assert.Equal(2, page.FindAll(".qual-confirm-item").Count);
    }

    [Fact]
    public void ConfirmingTheDeleteSendsTheCommand()
    {
        var chart = BuildChart("Alpha", 22);
        var entry = Entry("dupe", false, ManualPlay(chart));
        GivenView(new QualifierAdminView(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();
        page.FindAll(".qual-entry-tail button")[0].Click();
        page.FindAll(".mud-dialog-actions button").Last().Click();

        _mediator.Verify(m => m.Send(
            It.Is<DeleteQualifierEntryCommand>(c =>
                c.TournamentId == TournamentId && c.UserName == Name.From("dupe")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void EntrantsWithNoScoreAreListedButHoldNoRank()
    {
        var chart = BuildChart("Alpha", 22);
        GivenView(new QualifierAdminView(Config(new[] { chart }), Name.From("Test Cup"),
            Array.Empty<QualifierAdminEntry>(), new[] { Name.From("registered-only") },
            Array.Empty<QualifierDuplicateGroup>()));

        var page = Render();

        Assert.Contains("registered-only", page.Find(".qual-unscored").TextContent);
        Assert.Empty(page.FindAll(".qual-entry"));
    }
}

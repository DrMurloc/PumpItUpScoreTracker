using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;
using QualifiersPage = ScoreTracker.Web.Pages.Competition.Qualifiers;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one qualifiers page. The load-bearing behaviours: the pool renders for everyone
///     including signed out, no photo reaches the markup, and the legend only names states that
///     are actually on screen.
/// </summary>
public sealed class QualifiersPageTests : ComponentTestBase
{
    private static readonly Guid TournamentId = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();

    private static Chart BuildChart(string name, int level, ChartType type = ChartType.Double)
    {
        var song = new Song(name, SongType.Arcade, new Uri($"https://piu.test/{name}.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }

    private static QualifiersConfiguration Config(IEnumerable<Chart> charts, int playCount = 2) =>
        new(charts, new Dictionary<Guid, int>(), Name.From("Score"), 0, playCount, null, false);

    private readonly Mock<IDateTimeOffsetAccessor> _clock = new();
    private readonly Mock<IAdminNotificationClient> _notifications = new();
    private readonly Mock<IFileUploadClient> _fileUpload = new();

    public QualifiersPageTests()
    {
        _clock.SetupGet(c => c.Now).Returns(new DateTimeOffset(2026, 2, 8, 12, 0, 0, TimeSpan.Zero));
        Services.AddSingleton(_clock.Object);
        // Pulled in by the chart-details and submit dialogs the page hosts.
        Services.AddSingleton(_notifications.Object);
        Services.AddSingleton(_fileUpload.Object);
        // DifficultyBubble resolves a scoring level for every chart it renders.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddScoped<ChartScoringLevels>();
    }

    private void GivenBoard(QualifierBoard board)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetQualifiersBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        _mediator.Setup(m => m.Send(It.IsAny<GetTournamentRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserTournamentRole>());
        Services.AddSingleton(_mediator.Object);
    }

    private IRenderedComponent<QualifiersPage> Render()
    {
        // The page declares its own circuit and nests DifficultyBubble, which gates its tooltip
        // on RendererInfo. Set after GivenBoard: SetRendererInfo builds the service provider.
        this.RenderInteractive();
        return RenderComponent<QualifiersPage>(p => p.Add(x => x.TournamentId, TournamentId));
    }

    [Fact]
    public void TheChartPoolRendersWhenSignedOut()
    {
        var charts = new[] { BuildChart("Alpha", 22), BuildChart("Beta", 23) };
        GivenBoard(new QualifierBoard(Config(charts), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        // The pool is what the page is for; gating it behind a name was the original sin.
        Assert.Equal(2, page.FindAll(".qual-card").Count);
        Assert.Contains("Alpha", page.Markup);
        Assert.Contains("Beta", page.Markup);
    }

    [Fact]
    public void SignedOutSeesAPromptToStartRatherThanAStanding()
    {
        var charts = new[] { BuildChart("Alpha", 22) };
        GivenBoard(new QualifierBoard(Config(charts), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        Assert.NotEmpty(page.FindAll(".qual-you-empty"));
        Assert.Empty(page.FindAll(".qual-you-place"));
    }

    [Fact]
    public void TheBoardCarriesAChipPerCountingPlay()
    {
        var chartA = BuildChart("Alpha", 22);
        var chartB = BuildChart("Beta", 23);
        var entry = new QualifierEntry(Name.From("player"), true, 2400.5, new[]
        {
            new QualifierPlay(chartA, 990000, 1300.25, SubmissionSource.Manual),
            new QualifierPlay(chartB, 985000, 1100.25, SubmissionSource.OfficialImport)
        });
        GivenBoard(new QualifierBoard(Config(new[] { chartA, chartB }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        Assert.Equal(2, page.FindAll(".qual-chip").Count);
        // The total prints at two decimals so the column lines up.
        Assert.Contains("2,400.50", page.Markup);
    }

    [Fact]
    public void NoPhotoUrlReachesThePlayerMarkup()
    {
        var chart = BuildChart("Alpha", 22);
        var entry = new QualifierEntry(Name.From("player"), true, 1300.25, new[]
        {
            new QualifierPlay(chart, 990000, 1300.25, SubmissionSource.Manual)
        });
        GivenBoard(new QualifierBoard(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        // The board projection has no photo field at all, so this is a guard against someone
        // widening it later: photos are organiser reference only.
        Assert.DoesNotContain("qualifiers/", page.Markup);
    }

    [Fact]
    public void EntrantsWithoutScoresSitBelowTheLadderRatherThanHoldingARank()
    {
        var chart = BuildChart("Alpha", 22);
        GivenBoard(new QualifierBoard(Config(new[] { chart }), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), new[] { Name.From("registered-only") }, null, false, false, Array.Empty<Guid>()));

        var page = Render();

        var unscored = page.Find(".qual-unscored");
        Assert.Contains("registered-only", unscored.TextContent);
        Assert.Empty(page.FindAll(".olb-rank-card"));
    }

    [Fact]
    public void TheLegendOnlyNamesStatesThatAreOnScreen()
    {
        var charts = new[] { BuildChart("Alpha", 22), BuildChart("Beta", 23) };
        GivenBoard(new QualifierBoard(Config(charts), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        // Nothing is played, so "not played" is the only state present — and the only caption.
        var legend = page.FindAll(".qual-legend span");
        Assert.Single(legend);
        Assert.Empty(page.FindAll(".qual-legend-counting"));
    }

    /// <summary>
    ///     The regression this exists to prevent: from 2025-06-16 the page computed suggestions
    ///     and painted nothing, because the border style was gated on AllCharts while the legend
    ///     was gated on its negation. A suggested chart must carry the ring AND the caption.
    /// </summary>
    [Fact]
    public void ASuggestedChartIsPaintedAndAppearsInTheLegend()
    {
        var suggestedChart = BuildChart("Alpha", 22);
        var plainChart = BuildChart("Beta", 23);
        GivenBoard(new QualifierBoard(Config(new[] { suggestedChart, plainChart }), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, false,
            new[] { suggestedChart.Id }));

        var page = Render();

        Assert.Single(page.FindAll(".qual-card-suggested"));
        Assert.Single(page.FindAll(".qual-legend-suggested"));
        // And the untouched chart still reads as untouched, so the two states are distinct.
        Assert.Single(page.FindAll(".qual-card-untouched"));
    }

    [Fact]
    public void ASuggestionNeverOutranksAScoreYouAlreadyPosted()
    {
        var chart = BuildChart("Alpha", 22);
        var standing = new QualifierStanding(Name.From("player"), 1, 1, 1300.25, null, null);
        var entry = new QualifierEntry(Name.From("player"), true, 1300.25, new[]
        {
            new QualifierPlay(chart, 990000, 1300.25, SubmissionSource.Manual)
        });
        // A board that wrongly suggests a chart the player has already counted.
        GivenBoard(new QualifierBoard(Config(new[] { chart }), Name.From("Test Cup"),
            new[] { entry }, Array.Empty<Name>(), standing, false, false, new[] { chart.Id }));

        var page = Render();

        Assert.Single(page.FindAll(".qual-card-counting"));
        Assert.Empty(page.FindAll(".qual-card-suggested"));
    }

    [Fact]
    public void AClosedEventSaysSoInsteadOfHidingTheAction()
    {
        var chart = BuildChart("Alpha", 22);
        GivenBoard(new QualifierBoard(Config(new[] { chart }), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, true, Array.Empty<Guid>()));

        var page = Render();

        Assert.NotEmpty(page.FindAll(".qual-closed"));
        // No clock on a closed event, and no invitation to submit.
        Assert.Empty(page.FindAll(".qual-clock"));
        Assert.Empty(page.FindAll(".qual-you"));
    }

    [Fact]
    public void ABigPoolDropsToTheDenseTileFloor()
    {
        var charts = Enumerable.Range(1, 13).Select(i => BuildChart($"Chart{i}", 20 + i % 4)).ToArray();
        GivenBoard(new QualifierBoard(Config(charts, 8), Name.From("Big Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        Assert.Equal(13, page.FindAll(".qual-card").Count);
        Assert.NotEmpty(page.FindAll(".qual-pool-many"));
    }

    [Fact]
    public void ASmallPoolKeepsTheRoomierTileFloor()
    {
        var charts = Enumerable.Range(1, 4).Select(i => BuildChart($"Chart{i}", 22)).ToArray();
        GivenBoard(new QualifierBoard(Config(charts), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), null, false, false, Array.Empty<Guid>()));

        var page = Render();

        Assert.Empty(page.FindAll(".qual-pool-many"));
    }

    [Fact]
    public void TheStandingPanelPrintsThePlaceAndTheGapToTheRungAbove()
    {
        var chart = BuildChart("Alpha", 22);
        var standing = new QualifierStanding(Name.From("chezmix"), 4, 5, 1206.89, 1141.30,
            Name.From("LIGHTW8"));
        GivenBoard(new QualifierBoard(Config(new[] { chart }), Name.From("Test Cup"),
            Array.Empty<QualifierEntry>(), Array.Empty<Name>(), standing, false, false, Array.Empty<Guid>()));

        var page = Render();

        Assert.Equal("4", page.Find(".qual-you-place").TextContent.Trim());
        Assert.Contains("1,206.89", page.Find(".qual-you-total-value").TextContent);
        // The harness localizer echoes keys without substituting arguments, so this asserts the
        // branch rather than the formatted sentence: a gap exists, so it is not "leading".
        Assert.Contains("Need To Pass", page.Find(".qual-you-gap").TextContent);
        Assert.DoesNotContain("Leading The Field", page.Markup);
    }
}

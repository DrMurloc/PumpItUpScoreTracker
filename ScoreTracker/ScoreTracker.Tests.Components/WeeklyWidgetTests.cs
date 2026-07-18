using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Weekly Charts widget: the per-chart placement column. A board you sit on reads
///     "place/total"; a board with entrants you're absent from still reads its count as
///     "-/total" so it feels alive; an empty board is a bare dash.
/// </summary>
public sealed class WeeklyWidgetTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Chart _placed = MakeChart("Bad Apple", 22);
    private readonly Chart _alive = MakeChart("Conflict", 21);
    private readonly Chart _empty = MakeChart("Gothique", 20);

    public WeeklyWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _placed, _alive, _empty });
        // No communities — the glow reader short-circuits to an empty set.
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());

        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentChart(_placed.Id, DateTimeOffset.MaxValue),
                new WeeklyTournamentChart(_alive.Id, DateTimeOffset.MaxValue),
                new WeeklyTournamentChart(_empty.Id, DateTimeOffset.MaxValue)
            });

        // Eight entrants on _placed (one is me) and eight on _alive (none me); _empty has none.
        var entries = Enumerable.Range(0, 8).Select(i => Entry(_placed.Id, i == 0 ? _me : Guid.NewGuid()))
            .Concat(Enumerable.Range(0, 8).Select(_ => Entry(_alive.Id, Guid.NewGuid())))
            .ToArray();
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        _mediator.Setup(m => m.Send(It.IsAny<GetUserWeeklyPlacementsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyPlacementRecord(_placed.Id, 1) });

        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(new Mock<IUserRepository>().Object); // the embedded LeaderboardDialog injects it
        Services.AddScoped<ChartCatalogCache>();
        Services.AddScoped<CommunityGlowReader>();
        this.RenderInteractive();
    }

    private static Chart MakeChart(string name, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(140, 140)),
            ChartType.Single, level, MixEnum.Phoenix, null, 1200, new HashSet<Skill>());

    private WeeklyTournamentEntry Entry(Guid chartId, Guid userId) =>
        new(userId, chartId, 950000, PhoenixPlate.SuperbGame, false, null, 20.0);

    private IRenderedComponent<WeeklyWidget> Render()
    {
        // All mode skips the competitive filter — the whole board renders.
        var config = WidgetConfigJson.Write(new WeeklyConfig { Mode = WeeklyBoardMode.All });
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "weekly-challenge", null, 0, "1x2", config, 1);
        return base.Render(builder =>
        {
            builder.OpenComponent<WeeklyWidget>(0);
            builder.AddAttribute(1, nameof(WeeklyWidget.Widget), widget);
            builder.AddAttribute(2, nameof(WeeklyWidget.EffectiveMix), MixEnum.Phoenix);
            builder.CloseComponent();
        }).FindComponent<WeeklyWidget>();
    }

    [Fact]
    public void PlacedChartReadsPlaceOverTotal()
    {
        var cut = Render();

        Assert.Contains("1/8", cut.Markup);
    }

    [Fact]
    public void AliveBoardYouAreAbsentFromStillShowsItsCount()
    {
        var cut = Render();

        Assert.Contains("-/8", cut.Markup);
    }

    [Fact]
    public void EmptyBoardIsABareDash()
    {
        var cut = Render();

        // The empty chart's cell is a lone em dash — no denominator to promise.
        var places = cut.FindAll(".dash-weekly-place").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Contains("—", places);
    }
}

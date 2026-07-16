using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Suggested Charts widget, Hot Streak goal: grouped sections caption each seed with
///     the Peers bar it cleared, flat mode carries the seed per row, the right column is
///     the personalized Pass tier (or the stale score + age for outdated targets), and
///     the goal has its own empty state.
/// </summary>
public sealed class SuggestedChartsWidgetTests : ComponentTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Chart _seed = MakeChart("District 1", ChartType.Single, 20);
    private readonly Chart _easyMatch = MakeChart("Achluoias", ChartType.Single, 20);
    private readonly Chart _hardMatch = MakeChart("Uh-Heung", ChartType.Single, 20);

    public SuggestedChartsWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _seed, _easyMatch, _hardMatch });
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartRecommendation>());
        _mediator.Setup(m => m.Send(It.IsAny<GetBlendedTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));
        Services.AddSingleton(_mediator.Object);
        Services.AddScoped<ChartCatalogCache>();

        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(Now);
        Services.AddSingleton(clock.Object);
    }

    private static Chart MakeChart(string name, ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(140, 140)),
            type, level, MixEnum.Phoenix, null, 1200, new HashSet<Skill>());

    private void SetUpRecommendations(params ChartRecommendation[] recommendations)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendations);
    }

    private void SetUpTiers(params (Guid ChartId, TierListCategory Tier)[] tiers)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetBlendedTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(
                tiers.Select(t => new SongTierListEntry("Pass", t.ChartId, t.Tier, 0)).ToArray(), false));
    }

    private IRenderedComponent<SuggestedChartsWidget> Render(SuggestedChartsConfig? config = null,
        WidgetHeaderSlot? headerSlot = null)
    {
        config ??= new SuggestedChartsConfig { Goal = SuggestedGoal.HotStreak };
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "suggested-charts", null, 0, "1x2",
            WidgetConfigJson.Write(config), 1);
        RenderFragment inner = builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<SuggestedChartsWidget>(1);
            builder.AddAttribute(2, nameof(SuggestedChartsWidget.Widget), widget);
            builder.AddAttribute(3, nameof(SuggestedChartsWidget.EffectiveMix), MixEnum.Phoenix);
            builder.CloseComponent();
        };
        return base.Render(builder =>
        {
            if (headerSlot != null)
            {
                builder.OpenComponent<CascadingValue<WidgetHeaderSlot>>(0);
                builder.AddAttribute(1, "Value", headerSlot);
                builder.AddAttribute(2, "ChildContent", inner);
                builder.CloseComponent();
            }
            else
            {
                inner(builder);
            }
        }).FindComponent<SuggestedChartsWidget>();
    }

    private ChartRecommendation HotStreakRec(Guid chartId, double? ranking = 0.94, bool fallback = false) =>
        new(RecommendationCategories.HotStreak, chartId, "More charts like your recent standout plays",
            SeedChartId: _seed.Id, SeedPeerRanking: ranking, SeedIsFallback: fallback);

    [Fact]
    public void GroupedModeCaptionsTheSeedWithThePeersBarEvenForASingleSection()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));

        var cut = Render();

        Assert.Contains("More like District 1 S20 · you beat 94% of Peers", cut.Markup);
    }

    [Fact]
    public void GroupedRowsOrderEasiestTierFirstAndColorTheTierColumn()
    {
        SetUpRecommendations(HotStreakRec(_hardMatch.Id), HotStreakRec(_easyMatch.Id));
        SetUpTiers((_hardMatch.Id, TierListCategory.VeryHard), (_easyMatch.Id, TierListCategory.Easy));

        var cut = Render();

        Assert.Contains("Easy", cut.Markup);
        Assert.Contains("Very Hard", cut.Markup);
        // The engine sent the hard match first; the widget reorders by the fetched tier.
        Assert.True(cut.Markup.IndexOf("Achluoias", StringComparison.Ordinal)
                    < cut.Markup.IndexOf("Uh-Heung", StringComparison.Ordinal));
    }

    [Fact]
    public void FlatModeCarriesTheSeedInEachRowsDetailInstead()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));

        var cut = Render(new SuggestedChartsConfig { Goal = SuggestedGoal.HotStreak, GroupBySeed = false });

        Assert.Contains("≈ District 1 S20", cut.Markup);
        Assert.DoesNotContain("More like", cut.Markup);
    }

    [Fact]
    public void OutdatedTargetsKeepTheirStaleScoreAndAge()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(_easyMatch.Id, 871204, PhoenixPlate.SuperbGame, false,
                    Now.AddDays(-400))
            });

        var cut = Render(new SuggestedChartsConfig
        {
            Goal = SuggestedGoal.HotStreak, HotStreakIncludeOldScores = true
        });

        Assert.Contains("871,204", cut.Markup);
        Assert.Contains("400 days old", cut.Markup);
    }

    [Fact]
    public void FallbackSeedsRaiseTheHeaderPillAndSayYourBest()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id, fallback: true));
        var slot = new WidgetHeaderSlot(() => { });

        var cut = Render(headerSlot: slot);

        Assert.NotNull(slot.Content);
        Assert.Contains("Your best: District 1 S20 · you beat 94% of Peers", cut.Markup);
        Assert.DoesNotContain("More like", cut.Markup);
    }

    [Fact]
    public void FlagSeededLoadLeavesTheHeaderPillEmpty()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));
        var slot = new WidgetHeaderSlot(() => { });

        var cut = Render(headerSlot: slot);

        Assert.Null(slot.Content);
        Assert.Contains("More like District 1 S20", cut.Markup);
    }

    [Fact]
    public void HotStreakEmptyStateSpeaksToTheGoalNotToMissingScores()
    {
        var cut = Render();

        Assert.Contains("No matching standouts yet, go push yourself to start getting suggestions!", cut.Markup);
        Assert.DoesNotContain("Upload Scores", cut.Markup);
    }

    [Fact]
    public void ConfigPanelShowsTheHotStreakKnobsAndHidesLevels()
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "suggested-charts", null, 0, "1x2",
            WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.HotStreak }), 1);
        var cut = base.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<SuggestedChartsConfigPanel>(1);
            builder.AddAttribute(2, nameof(SuggestedChartsConfigPanel.Widget), widget);
            builder.CloseComponent();
        });

        Assert.Contains("Standout bar", cut.Markup);
        Assert.Contains("Look back", cut.Markup);
        Assert.Contains("Treat very old scores as unplayed", cut.Markup);
        Assert.Contains("Group by seed chart", cut.Markup);
        Assert.DoesNotContain("Around my level", cut.Markup);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // Last — it touches the renderer, which freezes service registration.
        // DifficultyBubble/SongImage gate their tooltips on RendererInfo.IsInteractive;
        // the widget always lives inside a circuit, so these facts render as one.
        this.RenderInteractive();
    }

    private static Chart MakeChart(string name, ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(140, 140)),
            type, level, MixEnum.Phoenix, null, 1200);

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
        WidgetHeaderSlot? headerSlot = null, MixEnum mix = MixEnum.Phoenix)
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
            builder.AddAttribute(3, nameof(SuggestedChartsWidget.EffectiveMix), mix);
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
    public void TitleHuntFallsBackToPumbilityPushOnPhoenix2()
    {
        // P2 has no difficulty or skill titles, so the goal's own categories are structurally
        // empty there — the widget asks for the Pumbility Push bundle instead, silently
        // (owner, 2026-08-14: no explainer glyph; a P2 "next title" IS a pumbility threshold).
        Render(new SuggestedChartsConfig { Goal = SuggestedGoal.TitleHunt }, mix: MixEnum.Phoenix2);

        _mediator.Verify(m => m.Send(It.Is<GetRecommendedChartsQuery>(q =>
                q.Mix == MixEnum.Phoenix2 && q.Categories != null && q.Categories.Count == 1
                && q.Categories.Contains(RecommendationCategory.PushPumbility)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TitleHuntKeepsItsTitleCategoriesOnPhoenix()
    {
        Render(new SuggestedChartsConfig { Goal = SuggestedGoal.TitleHunt });

        _mediator.Verify(m => m.Send(It.Is<GetRecommendedChartsQuery>(q =>
                q.Categories != null && q.Categories.Contains(RecommendationCategory.PushLevel)
                && q.Categories.Contains(RecommendationCategory.SkillTitles)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GroupedModeCaptionsTheSeedEvenForASingleSectionWithThePeersBarInTheTooltip()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));

        var cut = Render();

        Assert.Contains("Like District 1 S20", cut.Markup);
        Assert.Contains("You beat 94% of Peers on this.", cut.Markup);
    }

    [Fact]
    public void TargetRowsAlwaysEmitTheSongNameSoWideCellsCanFillTheGap()
    {
        // The name span is always in the DOM now (a container query hides it only when
        // the cell is too narrow) — a full-width single-column widget stops leaving the
        // empty gap the right-aligned score used to open.
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));

        var cut = Render();

        Assert.Contains(cut.FindAll(".dash-target-name"), n => n.TextContent.Contains("Achluoias"));
    }

    [Fact]
    public void GroupedRowsOrderBestTierFirstAndSpeakThePumbilityVocabulary()
    {
        SetUpRecommendations(HotStreakRec(_hardMatch.Id), HotStreakRec(_easyMatch.Id));
        SetUpTiers((_hardMatch.Id, TierListCategory.VeryHard), (_easyMatch.Id, TierListCategory.Easy));

        var cut = Render();

        // Hot Streak reads the personalized PUMBILITY blend (Personalized Pass is gone), so
        // its tier column speaks that lens's vocabulary on the rarity ramp: an Easy-banded
        // chart reads Solid, a VeryHard one Slim — never the difficulty words.
        Assert.Contains("Solid", cut.Markup);
        Assert.Contains("Slim", cut.Markup);
        Assert.DoesNotContain("Very Hard", cut.Markup);
        _mediator.Verify(m => m.Send(It.Is<GetBlendedTierListQuery>(q =>
            (string)q.Lens == "PUMBILITY" && q.Personalized), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
        Assert.DoesNotContain("Like District 1", cut.Markup);
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
    public void FallbackSeedsRaiseTheHeaderGlyphAndTheTooltipSaysAllTimeBest()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id, fallback: true));
        var slot = new WidgetHeaderSlot(() => { });

        var cut = Render(headerSlot: slot);

        Assert.NotNull(slot.Content);
        // The caption stays short; the fallback story rides its tooltip.
        Assert.Contains("Like District 1 S20", cut.Markup);
        Assert.Contains("One of your all-time best — you beat 94% of Peers.", cut.Markup);
    }

    [Fact]
    public void FlagSeededLoadLeavesTheHeaderGlyphEmpty()
    {
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));
        var slot = new WidgetHeaderSlot(() => { });

        var cut = Render(headerSlot: slot);

        Assert.Null(slot.Content);
        Assert.Contains("Like District 1 S20", cut.Markup);
    }

    [Fact]
    public void HotStreakEmptyStateSpeaksToTheGoalNotToMissingScores()
    {
        var cut = Render();

        Assert.Contains("No matching standouts yet, go push yourself to start getting suggestions!", cut.Markup);
        Assert.DoesNotContain("Import Scores", cut.Markup);
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

    [Fact]
    public void FirstLoadShowsTheSpinnerBecauseThereIsNothingToKeep()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<IEnumerable<ChartRecommendation>>().Task);

        var cut = RenderComponent<SuggestedChartsWidget>(p => p
            .Add(w => w.Widget, WidgetRecord())
            .Add(w => w.EffectiveMix, MixEnum.Phoenix));

        Assert.NotEmpty(cut.FindAll(".mud-progress-circular"));
        Assert.Empty(cut.FindAll(".dash-refreshable"));
    }

    [Fact]
    public void RefreshKeepsTheCurrentListDimmedInsteadOfTheSpinner()
    {
        // The stale-while-revalidate contract (§2.3): a reload renders what is already on
        // screen, dimmed, and swaps in place — the spinner never returns after first load.
        // On the auto-height mobile column the old spinner swap collapsed the page under a
        // mid-scroll finger, which is the bug this pins shut.
        SetUpRecommendations(HotStreakRec(_easyMatch.Id));
        var cut = RenderComponent<SuggestedChartsWidget>(p => p
            .Add(w => w.Widget, WidgetRecord())
            .Add(w => w.EffectiveMix, MixEnum.Phoenix));
        cut.WaitForAssertion(() => Assert.Contains("Achluoias", cut.Markup));

        var pending = new TaskCompletionSource<IEnumerable<ChartRecommendation>>();
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(pending.Task);
        cut.SetParametersAndRender(p => p.Add(w => w.RefreshToken, 1));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".dash-stale"));
            Assert.Contains("Achluoias", cut.Markup);
            Assert.Empty(cut.FindAll(".mud-progress-circular"));
        });

        pending.SetResult(new[] { HotStreakRec(_hardMatch.Id) });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Uh-Heung", cut.Markup);
            Assert.DoesNotContain("Achluoias", cut.Markup);
            Assert.Empty(cut.FindAll(".dash-stale"));
        });
    }

    private static HomePageWidgetRecord WidgetRecord()
    {
        return new HomePageWidgetRecord(Guid.NewGuid(), "suggested-charts", null, 0, "1x2",
            WidgetConfigJson.Write(new SuggestedChartsConfig { Goal = SuggestedGoal.HotStreak }), 1);
    }
}

using Bunit;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class RandomizerSettingsPanelTests : ComponentTestBase
{
    // The selector popup renders through MudPopoverProvider (same as on the live page,
    // where MainLayout hosts it), so the fragment carries a provider sibling and facts
    // search the whole fragment.
    private IRenderedFragment Render(RandomSettings settings,
        MixEnum mix = MixEnum.Phoenix, bool loggedIn = true)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(loggedIn);
        return base.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<RandomizerSettingsPanel>(1);
            builder.AddAttribute(2, nameof(RandomizerSettingsPanel.Settings), settings);
            builder.AddAttribute(3, nameof(RandomizerSettingsPanel.Mix), mix);
            builder.CloseComponent();
        });
    }

    private static RandomSettings WithSinglesRange(int min, int max)
    {
        var settings = new RandomSettings();
        foreach (var key in settings.LevelWeights.Keys.ToArray())
            settings.LevelWeights[key] = key >= min && key <= max ? 1 : 0;
        foreach (var key in settings.SongTypeWeights.Keys.ToArray())
            settings.SongTypeWeights[key] = 1;
        return settings;
    }

    [Fact]
    public void CountPresetWritesTheSettingsCount()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);

        // Presets are 3/5/7/10 in order.
        cut.FindAll(".rand-count-preset")[2].Click();

        Assert.Equal(7, settings.Count);
    }

    [Fact]
    public void TogglingSinglesOnWritesAContiguousDefaultRange()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);

        cut.FindComponents<MudSwitch<bool>>()[0].Find("input").Change(true);

        var active = settings.LevelWeights.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { 15, 16, 17, 18 }, active);
    }

    [Fact]
    public void CoOpChipTogglesThePlayerCountWeight()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);

        cut.FindAll(".rand-coop-chip")[1].Click();

        Assert.Equal(1, settings.PlayerCountWeights[3]);
    }

    [Fact]
    public void AdvancedShowsSectionsWithoutTogglesAndNoWeightRowsInSliderMode()
    {
        var cut = Render(WithSinglesRange(15, 18));
        cut.Find(".rand-advanced-toggle").Click();

        // Weighted Levels, Minimum Counts, Personal Scores — all visible, no opt-in toggles.
        Assert.Equal(3, cut.FindAll(".rand-adv-section").Count);
        // Slider mode: no weight rows yet, just the entry button.
        Assert.Empty(cut.FindAll(".weight-row"));
        Assert.NotEmpty(cut.FindAll(".weight-add-btn"));
    }

    [Fact]
    public void OpeningTheSelectorHighlightsSliderLevelsWithoutEngagingWeightedMode()
    {
        var settings = WithSinglesRange(15, 18);
        var cut = Render(settings);
        cut.Find(".rand-advanced-toggle").Click();

        cut.Find(".weight-add-btn").Click();

        // The sliders' range arrives highlighted; looking around changes nothing.
        Assert.Equal(4, cut.FindAll(".folder-picker-current").Count);
        Assert.Empty(cut.FindAll(".weight-row"));
        Assert.Empty(cut.FindAll(".rand-owned-note"));
    }

    [Fact]
    public void TogglingALevelInTheSelectorEngagesWeightedMode()
    {
        var settings = WithSinglesRange(15, 18);
        var cut = Render(settings);
        cut.Find(".rand-advanced-toggle").Click();
        cut.Find(".weight-add-btn").Click();

        cut.FindAll(".folder-picker-level").First(b => b.TextContent == "20").Click();

        Assert.Equal(1, settings.LevelWeights[20]);
        // Weighted mode: rows for 15-18 + 20, and the basic controls hand over.
        Assert.Equal(5, cut.FindAll(".weight-row").Count);
        Assert.NotEmpty(cut.FindAll(".rand-owned-note"));
    }

    [Fact]
    public void BackToSlidersTakesTwoTapsAndSnapsToTheContiguousRange()
    {
        // Gaps + a weight above 1: weighted mode derives itself on.
        var settings = WithSinglesRange(15, 15);
        settings.LevelWeights[18] = 3;
        var cut = Render(settings);

        Assert.NotEmpty(cut.FindAll(".weight-row"));

        cut.Find(".rand-back-to-sliders").Click();
        Assert.Contains("Tap again to clear weights", cut.Markup);
        Assert.Equal(3, settings.LevelWeights[18]);

        cut.Find(".rand-back-to-sliders").Click();
        Assert.Equal(new[] { 15, 16, 17, 18 },
            settings.LevelWeights.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k).ToArray());
        Assert.All(settings.LevelWeights.Where(kv => kv.Value > 0), kv => Assert.Equal(1, kv.Value));
        Assert.Empty(cut.FindAll(".weight-row"));
    }

    [Fact]
    public void SongTypeChipsLiveInBasicAndTheLastActiveTypeCannotBeRemoved()
    {
        var settings = WithSinglesRange(15, 18);
        var cut = Render(settings);

        // Basic filters now — no Advanced expansion needed.
        cut.FindAll(".rand-song-chips .rand-grade-chip")[0].Click(); // Arcade off
        Assert.Equal(0, settings.SongTypeWeights[SongType.Arcade]);

        // Turning the rest off leaves the final type lit.
        cut.FindAll(".rand-song-chips .rand-grade-chip")[1].Click();
        cut.FindAll(".rand-song-chips .rand-grade-chip")[2].Click();
        cut.FindAll(".rand-song-chips .rand-grade-chip")[3].Click();
        Assert.Equal(1, settings.SongTypeWeights.Values.Count(v => v > 0));
    }

    [Fact]
    public void MinimumCountsRenderWithoutAnOptInToggle()
    {
        var cut = Render(WithSinglesRange(15, 18));
        cut.Find(".rand-advanced-toggle").Click();

        Assert.NotEmpty(cut.FindAll(".rand-min-mode"));
        Assert.Contains("Guarantee at least", cut.Markup);
    }

    [Fact]
    public void PersonalScoreFiltersAreGatedOffPhoenixScoring()
    {
        var settings = new RandomSettings { ClearStatus = false };
        settings.LetterGrades.Add(PhoenixLetterGrade.SSS);
        var cut = Render(settings, MixEnum.XX);

        // Advanced derives itself open from the data; XX shows the reason.
        Assert.NotEmpty(cut.FindAll(".rand-gated-reason"));
        Assert.Empty(cut.FindAll(".rand-personal-segs"));
    }

    [Fact]
    public void LoggedOutHidesPersonalScoreFiltersEntirely()
    {
        var cut = Render(new RandomSettings(), loggedIn: false);
        cut.Find(".rand-advanced-toggle").Click();

        Assert.DoesNotContain("Filter By Personal Scores", cut.Markup);
        Assert.Equal(2, cut.FindAll(".rand-adv-section").Count);
    }

    [Fact]
    public void AllowRepeatsLivesInAdvancedNow()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);
        cut.Find(".rand-advanced-toggle").Click();

        var repeats = cut.FindComponents<MudSwitch<bool>>().First(s => s.Instance.Label == "Allow Repeat Charts");
        repeats.Find("input").Change(true);

        Assert.True(settings.AllowRepeats);
    }
}

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class RandomizerSettingsPanelTests : ComponentTestBase
{
    // Every fact here drives the panel through ClickAsync/ChangeAsync rather than the
    // synchronous Click()/Change(). The synchronous overloads post the event to the
    // renderer's dispatcher and return without waiting for it, so on a busy thread pool
    // the assertion on the next line reads the pre-event render — and a tap that depends
    // on the one before it (open Advanced, then open the selector, then pick a level)
    // reads a screen the earlier tap has not drawn yet.

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
    public async Task CountPresetWritesTheSettingsCount()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);

        // Presets are 3/5/7/10 in order.
        await cut.FindAll(".rand-count-preset")[2].ClickAsync(new MouseEventArgs());

        Assert.Equal(7, settings.Count);
    }

    [Fact]
    public async Task TogglingSinglesOnWritesAContiguousDefaultRange()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);

        await cut.FindComponents<MudSwitch<bool>>()[0].Find("input")
            .ChangeAsync(new ChangeEventArgs { Value = true });

        var active = settings.LevelWeights.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { 15, 16, 17, 18 }, active);
    }

    [Fact]
    public async Task CoOpChipTogglesThePlayerCountWeight()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);

        await cut.FindAll(".rand-coop-chip")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal(1, settings.PlayerCountWeights[3]);
    }

    [Fact]
    public async Task AdvancedShowsSectionsWithoutTogglesAndNoWeightRowsInSliderMode()
    {
        var cut = Render(WithSinglesRange(15, 18));
        await cut.Find(".rand-advanced-toggle").ClickAsync(new MouseEventArgs());

        // Weighted Levels, Minimum Counts, Personal Scores — all visible, no opt-in toggles.
        Assert.Equal(3, cut.FindAll(".rand-adv-section").Count);
        // Slider mode: no weight rows yet, just the entry button.
        Assert.Empty(cut.FindAll(".weight-row"));
        Assert.NotEmpty(cut.FindAll(".weight-add-btn"));
    }

    [Fact]
    public async Task OpeningTheSelectorHighlightsSliderLevelsWithoutEngagingWeightedMode()
    {
        var settings = WithSinglesRange(15, 18);
        var cut = Render(settings);
        await cut.Find(".rand-advanced-toggle").ClickAsync(new MouseEventArgs());

        await cut.Find(".weight-add-btn").ClickAsync(new MouseEventArgs());

        // The sliders' range arrives highlighted; looking around changes nothing.
        Assert.Equal(4, cut.FindAll(".folder-picker-current").Count);
        Assert.Empty(cut.FindAll(".weight-row"));
        Assert.Empty(cut.FindAll(".rand-owned-note"));
    }

    [Fact]
    public async Task TogglingALevelInTheSelectorEngagesWeightedMode()
    {
        var settings = WithSinglesRange(15, 18);
        var cut = Render(settings);
        await cut.Find(".rand-advanced-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".weight-add-btn").ClickAsync(new MouseEventArgs());

        await cut.FindAll(".folder-picker-level").First(b => b.TextContent == "20")
            .ClickAsync(new MouseEventArgs());

        Assert.Equal(1, settings.LevelWeights[20]);
        // Weighted mode: rows for 15-18 + 20, and the basic controls hand over.
        Assert.Equal(5, cut.FindAll(".weight-row").Count);
        Assert.NotEmpty(cut.FindAll(".rand-owned-note"));
    }

    [Fact]
    public async Task BackToSlidersTakesTwoTapsAndSnapsToTheContiguousRange()
    {
        // Gaps + a weight above 1: weighted mode derives itself on.
        var settings = WithSinglesRange(15, 15);
        settings.LevelWeights[18] = 3;
        var cut = Render(settings);

        Assert.NotEmpty(cut.FindAll(".weight-row"));

        await cut.Find(".rand-back-to-sliders").ClickAsync(new MouseEventArgs());
        Assert.Contains("Tap again to clear weights", cut.Markup);
        Assert.Equal(3, settings.LevelWeights[18]);

        await cut.Find(".rand-back-to-sliders").ClickAsync(new MouseEventArgs());
        Assert.Equal(new[] { 15, 16, 17, 18 },
            settings.LevelWeights.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k).ToArray());
        Assert.All(settings.LevelWeights.Where(kv => kv.Value > 0), kv => Assert.Equal(1, kv.Value));
        Assert.Empty(cut.FindAll(".weight-row"));
    }

    [Fact]
    public async Task SongTypeChipsLiveInBasicAndTheLastActiveTypeCannotBeRemoved()
    {
        var settings = WithSinglesRange(15, 18);
        var cut = Render(settings);

        // Basic filters now — no Advanced expansion needed.
        await cut.FindAll(".rand-song-chips .rand-grade-chip")[0].ClickAsync(new MouseEventArgs()); // Arcade off
        Assert.Equal(0, settings.SongTypeWeights[SongType.Arcade]);

        // Turning the rest off leaves the final type lit.
        await cut.FindAll(".rand-song-chips .rand-grade-chip")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".rand-song-chips .rand-grade-chip")[2].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".rand-song-chips .rand-grade-chip")[3].ClickAsync(new MouseEventArgs());
        Assert.Equal(1, settings.SongTypeWeights.Values.Count(v => v > 0));
    }

    [Fact]
    public async Task MinimumCountsRenderWithoutAnOptInToggle()
    {
        var cut = Render(WithSinglesRange(15, 18));
        await cut.Find(".rand-advanced-toggle").ClickAsync(new MouseEventArgs());

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
    public async Task LoggedOutHidesPersonalScoreFiltersEntirely()
    {
        var cut = Render(new RandomSettings(), loggedIn: false);
        await cut.Find(".rand-advanced-toggle").ClickAsync(new MouseEventArgs());

        Assert.DoesNotContain("Filter By Personal Scores", cut.Markup);
        Assert.Equal(2, cut.FindAll(".rand-adv-section").Count);
    }

    [Fact]
    public async Task AllowRepeatsLivesInAdvancedNow()
    {
        var settings = new RandomSettings();
        var cut = Render(settings);
        await cut.Find(".rand-advanced-toggle").ClickAsync(new MouseEventArgs());

        var repeats = cut.FindComponents<MudSwitch<bool>>().First(s => s.Instance.Label == "Allow Repeat Charts");
        await repeats.Find("input").ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.True(settings.AllowRepeats);
    }
}

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
    private IRenderedComponent<RandomizerSettingsPanel> Render(RandomSettings settings,
        MixEnum mix = MixEnum.Phoenix, bool loggedIn = true)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(loggedIn);
        return RenderComponent<RandomizerSettingsPanel>(p => p
            .Add(x => x.Settings, settings)
            .Add(x => x.Mix, mix));
    }

    private static RandomSettings WithSinglesRange(int min, int max)
    {
        var settings = new RandomSettings();
        foreach (var key in settings.LevelWeights.Keys.ToArray())
            settings.LevelWeights[key] = key >= min && key <= max ? 1 : 0;
        return settings;
    }

    private static IRenderedComponent<MudSwitch<bool>> SectionSwitch(
        IRenderedComponent<RandomizerSettingsPanel> cut, string label)
    {
        return cut.FindComponents<MudSwitch<bool>>().First(s => s.Instance.Label == label);
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
    public void AdvancedDisclosureRevealsTheOptInSections()
    {
        var cut = Render(new RandomSettings());
        Assert.Empty(cut.FindAll(".rand-adv-section"));

        cut.Find(".rand-advanced-toggle").Click();

        // Six opt-in sections for a logged-in Phoenix user, all collapsed.
        Assert.Equal(6, cut.FindAll(".rand-adv-section").Count);
        Assert.Empty(cut.FindAll(".rand-adv-section-content"));
    }

    [Fact]
    public void OptingIntoDynamicSinglesDisablesTheBasicSliderAndShowsWeightRows()
    {
        var settings = WithSinglesRange(16, 18);
        var cut = Render(settings);
        cut.Find(".rand-advanced-toggle").Click();

        SectionSwitch(cut, "Dynamic Singles Weights").Find("input").Change(true);

        Assert.NotEmpty(cut.FindAll(".rand-owned-note"));
        // Rows seed from the slider's current range.
        Assert.Equal(3, cut.FindComponents<FolderWeightList>()[0].FindAll(".weight-row").Count);
    }

    [Fact]
    public void OptingOutTakesTwoTapsAndSnapsWeightsToTheContiguousRange()
    {
        // Gaps + a weight above 1: the section derives itself open.
        var settings = new RandomSettings();
        settings.LevelWeights[15] = 1;
        settings.LevelWeights[18] = 3;
        var cut = Render(settings);

        var section = SectionSwitch(cut, "Dynamic Singles Weights");
        Assert.True(section.Instance.Value);

        section.Find("input").Change(false);
        // First tap: confirm caption, still opted in, data untouched.
        Assert.Contains("Tap again to turn off and clear", cut.Markup);
        Assert.Equal(3, settings.LevelWeights[18]);

        SectionSwitch(cut, "Dynamic Singles Weights").Find("input").Change(false);
        // Second tap: section off, weights snap to the slider-expressible span.
        Assert.Equal(new[] { 15, 16, 17, 18 },
            settings.LevelWeights.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(k => k).ToArray());
        Assert.All(settings.LevelWeights.Where(kv => kv.Value > 0), kv => Assert.Equal(1, kv.Value));
    }

    [Fact]
    public void SongTypeFilteringDerivesOpenAndOptOutRestoresAllTypes()
    {
        var settings = WithSinglesRange(15, 18);
        settings.SongTypeWeights[SongType.Arcade] = 1; // others stay 0 = filtering
        var cut = Render(settings);

        var section = SectionSwitch(cut, "Song Types");
        Assert.True(section.Instance.Value);

        section.Find("input").Change(false);
        SectionSwitch(cut, "Song Types").Find("input").Change(false);

        Assert.All(settings.SongTypeWeights.Values, v => Assert.Equal(1, v));
    }

    [Fact]
    public void PersonalScoreFiltersAreGatedOffPhoenixScoring()
    {
        var settings = new RandomSettings { ClearStatus = false };
        settings.LetterGrades.Add(PhoenixLetterGrade.SSS);
        var cut = Render(settings, MixEnum.XX);

        // The section derives itself open from the data; XX shows the reason instead.
        Assert.NotEmpty(cut.FindAll(".rand-gated-reason"));
        Assert.Empty(cut.FindAll(".rand-my-results"));
    }

    [Fact]
    public void PersonalScoreFiltersAreAnOptInSection()
    {
        var cut = Render(new RandomSettings());
        cut.Find(".rand-advanced-toggle").Click();

        Assert.Empty(cut.FindAll(".rand-my-results"));

        SectionSwitch(cut, "Filter By Personal Scores").Find("input").Change(true);

        Assert.Equal(3, cut.FindAll(".rand-seg").Count);
        Assert.NotEmpty(cut.FindAll(".rand-grade-chip"));
    }

    [Fact]
    public void LoggedOutHidesPersonalScoreFiltersEntirely()
    {
        var cut = Render(new RandomSettings(), loggedIn: false);
        cut.Find(".rand-advanced-toggle").Click();

        Assert.DoesNotContain("Filter By Personal Scores", cut.Markup);
        Assert.Equal(5, cut.FindAll(".rand-adv-section").Count);
    }
}

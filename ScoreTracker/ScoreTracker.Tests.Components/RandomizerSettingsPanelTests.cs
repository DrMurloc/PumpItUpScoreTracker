using Bunit;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Components;
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

        cut.FindComponents<MudBlazor.MudSwitch<bool>>()[0].Find("input").Change(true);

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
    public void PlayerFiltersAreGatedOffPhoenixScoring()
    {
        var settings = new RandomSettings { ClearStatus = false };
        settings.LetterGrades.Add(PhoenixLetterGrade.SSS);
        var cut = Render(settings, MixEnum.XX);

        Assert.NotEmpty(cut.FindAll(".rand-gated-reason"));
        Assert.Empty(cut.FindAll(".rand-my-results"));
    }

    [Fact]
    public void PhoenixMixShowsThePassedSegmentsAndGradeChips()
    {
        var cut = Render(new RandomSettings());

        Assert.Equal(3, cut.FindAll(".rand-seg").Count);
        Assert.NotEmpty(cut.FindAll(".rand-grade-chip"));
    }

    [Fact]
    public void LoggedOutHidesPlayerFiltersEntirely()
    {
        var cut = Render(new RandomSettings(), loggedIn: false);

        Assert.Empty(cut.FindAll(".rand-my-results"));
        Assert.Empty(cut.FindAll(".rand-gated-reason"));
    }

    [Fact]
    public void AdvancedDisclosureRevealsThePerLevelGrids()
    {
        var cut = Render(new RandomSettings());
        Assert.Empty(cut.FindAll(".rand-advanced-body"));

        cut.Find(".rand-advanced-toggle").Click();

        Assert.NotEmpty(cut.FindAll(".rand-advanced-body"));
    }
}

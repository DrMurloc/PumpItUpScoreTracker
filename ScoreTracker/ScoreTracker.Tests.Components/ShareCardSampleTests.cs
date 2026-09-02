using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The example's scripted states: one tile per thing the dialog can show, in a fixed order,
///     so the preview never depends on what the player happens to have played.
/// </summary>
public sealed class ShareCardSampleTests
{
    private static Chart BuildChart(string name)
    {
        var song = new Song(name, SongType.Arcade, new Uri($"https://example.invalid/{name}.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Single, DifficultyLevel.From(20),
            MixEnum.Phoenix, null, null);
    }

    private static Chart[] Charts(int count) =>
        Enumerable.Range(1, count).Select(i => BuildChart($"Chart {i}")).ToArray();

    [Fact]
    public void SixChartsWearTheSixScriptedStatesInOrder()
    {
        var facts = ShareCardSample.Facts(Charts(6), MixEnum.Phoenix, _ => null, _ => null);

        Assert.Equal(6, facts.Count);

        var perfect = facts[0];
        Assert.True(perfect.Passed);
        Assert.Equal(PhoenixScore.From(1_000_000), perfect.Score);
        Assert.Equal(PhoenixPlate.PerfectGame, perfect.Plate);
        Assert.True(perfect.InTop50Combined);
        Assert.Null(perfect.Gain);
        Assert.True(perfect.CurrentPumbility > 0);

        var pass = facts[1];
        Assert.True(pass.Passed);
        Assert.True(pass.InTop50Type);
        Assert.False(pass.InTop50Combined);
        Assert.Equal(2.1, pass.Gain);
        Assert.NotNull(pass.ExpectedScore);

        var broken = facts[2];
        Assert.True(broken.Broken);
        Assert.False(broken.Passed);
        Assert.NotNull(broken.Score);
        Assert.Null(broken.CurrentPumbility);

        Assert.True(facts[3].IsToDo);
        Assert.Null(facts[3].Score);
        Assert.True(facts[4].PassedInOtherMix);

        var bare = facts[5];
        Assert.False(bare.Passed || bare.Broken || bare.IsToDo || bare.PassedInOtherMix);
        Assert.Null(bare.Score);
        Assert.Null(bare.Gain);
    }

    [Fact]
    public void FewerChartsTakeTheFirstStatesAndNeverMoreThanSix()
    {
        var two = ShareCardSample.Facts(Charts(2), MixEnum.Phoenix, _ => null, _ => null);
        Assert.Equal(2, two.Count);
        Assert.True(two[0].InTop50Combined);
        Assert.True(two[1].InTop50Type);

        var nine = ShareCardSample.Facts(Charts(9), MixEnum.Phoenix, _ => null, _ => null);
        Assert.Equal(ShareCardSample.Size, nine.Count);
    }

    [Fact]
    public void ALegacyMixKeepsTheStatesAndDropsTheNumbers()
    {
        var facts = ShareCardSample.Facts(Charts(6), MixEnum.XX, _ => null, _ => null);

        Assert.True(facts[0].Passed);
        Assert.Null(facts[0].Score);
        Assert.Null(facts[0].Plate);
        Assert.Null(facts[0].CurrentPumbility);
        Assert.True(facts[2].Broken);
        Assert.Null(facts[1].ExpectedScore);
    }

    [Fact]
    public void SkillsAndBubblesComeFromTheHost()
    {
        var chips = new[] { new TierListChartCard.CardSkillChip("Twists", "badgecat-twists", null, IsIdentity: true) };
        var charts = Charts(1);

        var facts = ShareCardSample.Facts(charts, MixEnum.Phoenix, _ => chips, c => $"bubble:{c.Song.Name}");

        Assert.Same(chips, facts[0].Skills);
        Assert.Equal("bubble:Chart 1", facts[0].BubbleUrl);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The two ways a folder standing renders. The chip pins hue-is-grade / glow-is-completion,
///     the spectrum pins that length is completion and the grey tail is what is left
///     (docs/design/folder-level-progression.md §3).
/// </summary>
public sealed class FolderLevelComponentTests : ComponentTestBase
{
    // The grade reads off the score at the tier's position, so these standings name that
    // directly; the average rides along as the display number it is.
    private static FolderLevelRecord Folder(int size, int played, int tierScore,
        ChartType type = ChartType.Single, int level = 22, MixEnum mix = MixEnum.Phoenix) =>
        new(mix, type, DifficultyLevel.From(level), size, played, tierScore, tierScore);

    private static IReadOnlyList<PhoenixLetterGrade> Grades(params (PhoenixLetterGrade Grade, int Count)[] runs) =>
        runs.SelectMany(r => Enumerable.Repeat(r.Grade, r.Count)).ToArray();

    [Fact]
    public void TheChipShowsTheGradeAndTheCompletionPercent()
    {
        var cut = RenderComponent<FolderLevelChip>(p => p.Add(c => c.Level, Folder(97, 90, 934245)));

        Assert.Contains("AA+", cut.Markup);
        Assert.Contains("92%", cut.Markup);
    }

    [Fact]
    public void TheChipWearsTheGradeMetalAsATokenNeverALiteral()
    {
        var cut = RenderComponent<FolderLevelChip>(p => p.Add(c => c.Level, Folder(97, 90, 934245)));

        Assert.Contains("var(--grade-aaplus)", cut.Markup);
        Assert.DoesNotContain("#", cut.Find(".fl-chip").GetAttribute("style") ?? string.Empty);
    }

    [Theory]
    [InlineData(40, "")]
    [InlineData(60, "rarity-glow-1")]
    [InlineData(80, "rarity-glow-2")]
    [InlineData(100, "rarity-glow-3")]
    public void GlowStepsWithTheCompletionTier(int played, string expectedGlow)
    {
        var cut = RenderComponent<FolderLevelChip>(p => p.Add(c => c.Level, Folder(100, played, 934245)));

        var grade = cut.Find(".fl-chip-grade").GetAttribute("class") ?? string.Empty;
        if (expectedGlow.Length == 0)
            Assert.DoesNotContain("rarity-glow", grade);
        else
            Assert.Contains(expectedGlow, grade);
    }

    [Fact]
    public void ALampedFolderIsRinged()
    {
        var cut = RenderComponent<FolderLevelChip>(p => p.Add(c => c.Level, Folder(23, 23, 888850)));

        Assert.Contains("fl-lamped", cut.Find(".fl-chip").GetAttribute("class"));
        Assert.Contains("LAMP", cut.Markup);
    }

    [Fact]
    public void AnUntouchedFolderShowsACountRatherThanAnF()
    {
        var cut = RenderComponent<FolderLevelChip>(p => p.Add(c => c.Level, Folder(11, 0, 0)));

        Assert.Contains("0 / 11", cut.Markup);
        Assert.DoesNotContain("fl-chip-grade", cut.Markup);
    }

    [Fact]
    public void TheSameAverageWearsADifferentMetalInPhoenix2()
    {
        var phoenix = RenderComponent<FolderLevelChip>(p =>
            p.Add(c => c.Level, Folder(10, 10, 930000)));
        var phoenix2 = RenderComponent<FolderLevelChip>(p =>
            p.Add(c => c.Level, Folder(10, 10, 930000, mix: MixEnum.Phoenix2)));

        Assert.Contains("var(--grade-aaplus)", phoenix.Markup);
        Assert.Contains("var(--grade-aa)", phoenix2.Markup);
    }

    [Fact]
    public void TheSpectrumEndsInGreyForWhateverIsUnplayed()
    {
        // Half a ten-chart folder played: the fill runs to 50%, grey covers the rest.
        var cut = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Grades((PhoenixLetterGrade.SSS, 5)))
            .Add(s => s.Size, 10));

        var fill = cut.Find(".fl-fill").GetAttribute("style") ?? string.Empty;
        Assert.Contains("var(--grade-sss) 0% 50%", fill);
        Assert.Contains("var(--unplayed-grade) 50% 100%", fill);
    }

    [Fact]
    public void AdjacentGradesSharingAMetalCollapseIntoOneBand()
    {
        // AA+ and AA are one rung of the eight-metal ladder, so they must not draw two slivers.
        var cut = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Grades((PhoenixLetterGrade.AAPlus, 2), (PhoenixLetterGrade.AA, 2)))
            .Add(s => s.Size, 4));

        var fill = cut.Find(".fl-fill").GetAttribute("style") ?? string.Empty;
        Assert.Contains("var(--grade-aaplus) 0% 100%", fill);
        Assert.Equal(1, fill.Split("var(--grade-").Length - 1);
    }

    [Fact]
    public void ALampedSpectrumHasNoGreyTailAndGlows()
    {
        var lamped = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Grades((PhoenixLetterGrade.A, 4)))
            .Add(s => s.Size, 4));
        var partial = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Grades((PhoenixLetterGrade.A, 3)))
            .Add(s => s.Size, 4));

        Assert.DoesNotContain("var(--unplayed-grade)",
            lamped.Find(".fl-fill").GetAttribute("style") ?? string.Empty);
        Assert.Contains("fl-track-lamped", lamped.Find(".fl-track").GetAttribute("class"));
        Assert.DoesNotContain("fl-track-lamped", partial.Find(".fl-track").GetAttribute("class"));
    }

    [Fact]
    public void TierTicksSitAtTheLadderAndCanBeTurnedOff()
    {
        var withTicks = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Grades((PhoenixLetterGrade.SSS, 5)))
            .Add(s => s.Size, 10));
        var without = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Grades((PhoenixLetterGrade.SSS, 5)))
            .Add(s => s.Size, 10)
            .Add(s => s.ShowTicks, false));

        // Four ticks: 20/40/60/80. 100 is the track's own end, so it never gets one.
        Assert.Equal(4, withTicks.FindAll(".fl-tick").Count);
        Assert.Empty(without.FindAll(".fl-tick"));
    }

    [Fact]
    public void AnEmptyFolderRendersGreyRatherThanThrowing()
    {
        var cut = RenderComponent<FolderSpectrum>(p => p
            .Add(s => s.GradesDescending, Array.Empty<PhoenixLetterGrade>())
            .Add(s => s.Size, 0));

        Assert.Contains("var(--unplayed-grade)", cut.Find(".fl-fill").GetAttribute("style") ?? string.Empty);
    }
}

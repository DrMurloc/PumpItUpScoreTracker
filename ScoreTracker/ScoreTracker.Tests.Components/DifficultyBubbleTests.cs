using Bunit;
using Microsoft.AspNetCore.Components;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Which picture a difficulty gets is the mix's profile's answer (docs/design/rise.md D12):
///     Rise borrows the Phoenix 2 stepballs, half-doubles included, while Infinity's half-doubles
///     keep the chip because no bubble set of theirs has one.
/// </summary>
public sealed class DifficultyBubbleTests : ComponentTestBase
{
    public DifficultyBubbleTests()
    {
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private static Chart MakeChart(MixEnum mix, ChartType type, int level)
    {
        return new Chart(Guid.NewGuid(), mix,
            new Song("Kraken", SongType.Arcade, new Uri("https://piu.test/art.png"), TimeSpan.FromMinutes(2),
                "NeLiME", Bpm.From(150, 150)),
            type, level, mix, null, null);
    }

    [Fact]
    public void ARiseHalfDoubleDrawsTheHDoubleStepballFromThePhoenix2Set()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Rise, ChartType.HalfDouble, 24)));

        Assert.EndsWith("/difficulty/Phoenix2/hdb24.png", cut.Find("img").GetAttribute("src"));
        Assert.Empty(cut.FindAll(".legacy-chip"));
    }

    [Fact]
    public void ARiseSingleBorrowsThePhoenix2Stepball()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Rise, ChartType.Single, 16)));

        Assert.EndsWith("/difficulty/Phoenix2/s16.png", cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void AnInfinityHalfDoubleStaysTheNeutralChip()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Infinity, ChartType.HalfDouble, 12)));

        Assert.Single(cut.FindAll(".legacy-chip"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("HD 12", cut.Markup);
    }

    [Fact]
    public void AChartLessRiseHalfDoubleDrawsTheStepballToo()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Type, ChartType.HalfDouble)
            .Add(x => x.Level, DifficultyLevel.From(18))
            .Add(x => x.Mix, MixEnum.Rise));

        Assert.EndsWith("/difficulty/Phoenix2/hdb18.png", cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void ABubbleInACircuitIsAQuietPictureWithNoTooltipAndNoTabStop()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Phoenix2, ChartType.Single, 16)));

        Assert.Empty(cut.FindAll(".mud-tooltip-root"));
        var img = cut.Find("img");
        Assert.Contains("difficulty-bubble", img.ClassList);
        Assert.Equal("S16", img.GetAttribute("alt"));
        Assert.False(img.HasAttribute("tabindex"));
        Assert.False(img.HasAttribute("title"));
    }

    [Fact]
    public void ALegacyChipIsNotATabStopEither()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Infinity, ChartType.HalfDouble, 12)));

        Assert.Empty(cut.FindAll(".mud-tooltip-root"));
        var chip = cut.Find(".legacy-chip.difficulty-bubble");
        Assert.False(chip.HasAttribute("tabindex"));
        Assert.False(chip.HasAttribute("title"));
    }

    [Fact]
    public void AHardmodeGlowNamesItsEndInTheTitleSoTheRedIsNotTheOnlySignal()
    {
        var chart = MakeChart(MixEnum.Phoenix2, ChartType.Double, 22);
        HardmodeReader.Setup(h => h.GetQualifyingCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(chart) });

        var cut = RenderComponent<DifficultyBubble>(p => p.Add(x => x.Chart, chart));

        var img = cut.Find("img");
        Assert.Contains("bubble-hardmode", img.ClassList);
        Assert.Contains("difficulty-bubble", img.ClassList);
        Assert.Equal("Almost nobody holds this in a PUMBILITY top 50 — one of this week's Hardmode charts.",
            img.GetAttribute("title"));
        Assert.Empty(cut.FindAll(".mud-tooltip-root"));
    }

    [Fact]
    public void AMostHeldGlowNamesItsEndInTheTitleSoTheMintIsNotTheOnlySignal()
    {
        var chart = MakeChart(MixEnum.Phoenix2, ChartType.Single, 18);
        HardmodeReader.Setup(h => h.GetMostHeldCharts(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(chart) });

        var cut = RenderComponent<DifficultyBubble>(p => p.Add(x => x.Chart, chart));

        var img = cut.Find("img");
        Assert.Contains("bubble-easy", img.ClassList);
        Assert.Equal("One of the most-held charts in this folder's PUMBILITY top 50s.", img.GetAttribute("title"));
    }

    private static HardmodeChartEntry Entry(Chart chart)
    {
        return new HardmodeChartEntry(chart.Id, chart.Type, (int)chart.Level, 1000, 1, 40, 10);
    }
}

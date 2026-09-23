using Bunit;
using Microsoft.AspNetCore.Components;
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
            .Add(x => x.Chart, MakeChart(MixEnum.Rise, ChartType.HalfDouble, 24))
            .Add(x => x.Tooltip, false));

        Assert.EndsWith("/difficulty/Phoenix2/hdb24.png", cut.Find("img").GetAttribute("src"));
        Assert.Empty(cut.FindAll(".legacy-chip"));
    }

    [Fact]
    public void ARiseSingleBorrowsThePhoenix2Stepball()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Rise, ChartType.Single, 16))
            .Add(x => x.Tooltip, false));

        Assert.EndsWith("/difficulty/Phoenix2/s16.png", cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void AnInfinityHalfDoubleStaysTheNeutralChip()
    {
        var cut = RenderComponent<DifficultyBubble>(p => p
            .Add(x => x.Chart, MakeChart(MixEnum.Infinity, ChartType.HalfDouble, 12))
            .Add(x => x.Tooltip, false));

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
            .Add(x => x.Mix, MixEnum.Rise)
            .Add(x => x.Tooltip, false));

        Assert.EndsWith("/difficulty/Phoenix2/hdb18.png", cut.Find("img").GetAttribute("src"));
    }
}

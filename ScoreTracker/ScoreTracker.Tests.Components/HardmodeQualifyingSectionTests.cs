using Bunit;
using MediatR;
using Moq;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The qualifying-charts list (docs/design/hardmode-leaderboard.md §7). Three things it must
///     get right: which border a chart wears, that the sub-17 volatility warning appears exactly
///     where the census is thin, and that a chart no pool holds says so.
/// </summary>
public sealed class HardmodeQualifyingSectionTests : ComponentTestBase
{
    private static readonly Guid InPool = Guid.NewGuid();
    private static readonly Guid Outside = Guid.NewGuid();
    private static readonly Guid NotPlayed = Guid.NewGuid();

    [Fact]
    public void GoldMarksTheChartsInYourPoolAndGreenTheOnesThatDoNotCount()
    {
        var cut = Render(21);

        // Gold is the share card's own Top 50 boundary, so the same fact wears the same colour
        // in the page and in the download; green is a pass that does not count here.
        Assert.Single(cut.FindAll(".tier-chart-card.tier-chart-card-top50"));
        Assert.Single(cut.FindAll(".tier-chart-card.tier-chart-card-pass"));
        // The third state takes no border at all — it is the opportunity, not a claim.
        Assert.Equal(3, cut.FindAll(".tier-chart-card").Count);
    }

    [Fact]
    public void EveryStatePrintsItsWordSoTheHueNeverTravelsAlone()
    {
        var cut = Render(21);

        Assert.Single(cut.FindAll(".hmd-in"));
        Assert.Single(cut.FindAll(".hmd-out"));
        Assert.Single(cut.FindAll(".hmd-none"));
    }

    [Fact]
    public void AChartNoPoolHoldsSaysSoAtTheTopOfTheRarityRamp()
    {
        var cut = Render(21);

        // held by nobody is the rarest thing in a folder, so it reads as prism rather than as
        // missing data.
        Assert.Single(cut.FindAll(".hmd-held.hmd-held-zero"));
        Assert.Contains("held by nobody", cut.Markup);
    }

    [Fact]
    public void TheVolatilityWarningAppearsAtSeventeenAndBelow()
    {
        Assert.Contains("Level 17 and below moves a lot", Render(17).Markup);
        Assert.Contains("Level 17 and below moves a lot", Render(15).Markup);
    }

    [Fact]
    public void TheVolatilityWarningIsAbsentWhereTheCensusHasARealElectorate()
    {
        Assert.DoesNotContain("Level 17 and below moves a lot", Render(21).Markup);
        Assert.DoesNotContain("Level 17 and below moves a lot", Render(24).Markup);
    }

    [Fact]
    public void TheFolderSaysHowManyOfItQualifies()
    {
        Assert.Contains("3 of 170 charts in this folder qualify", Render(21).Markup);
    }

    private IRenderedComponent<HardmodeQualifyingSection> Render(int level)
    {
        var charts = new[]
        {
            Chart(InPool, "In My Pool", level),
            Chart(Outside, "Scored Outside", level),
            Chart(NotPlayed, "Never Played", level)
        };
        var qualifying = new[]
        {
            Record(InPool, level, holders: 12),
            Record(Outside, level, holders: 4),
            // Nobody holds this one — the rarest thing in the folder.
            Record(NotPlayed, level, holders: 0)
        };

        Mediator.Setup(m => m.Send(It.Is<GetHardmodeChartsQuery>(q => q.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>())).ReturnsAsync(qualifying);
        Mediator.Setup(m => m.Send(It.IsAny<GetPeerStandingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PeerStanding>());
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(false);
        // The cards nest DifficultyBubble, which gates its tooltip on RendererInfo.IsInteractive.
        this.RenderInteractive();

        return RenderComponent<HardmodeQualifyingSection>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Charts, charts.ToDictionary(c => c.Id))
            .Add(x => x.Pool, new[] { Entry(InPool, 460) })
            .Add(x => x.ScoredOutsidePool, new[] { Entry(Outside, 380) })
            .Add(x => x.QualifyingCount, 1211));
    }

    private static Chart Chart(Guid id, string name, int level)
    {
        return new Chart(id, MixEnum.Phoenix2,
            new Song(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/j.png"),
                TimeSpan.FromMinutes(2), Name.From("Artist"), null),
            ChartType.Single, DifficultyLevel.From(level), MixEnum.Phoenix2, null, null, null, null);
    }

    private static HardmodeChartRecord Record(Guid id, int level, int holders)
    {
        return new HardmodeChartRecord(id, ChartType.Single, level, holders * 30, holders, 170, 3);
    }

    private static PoolEntry Entry(Guid id, double value)
    {
        return new PoolEntry(1, id, PhoenixScore.From(975_000), PhoenixPlate.MarvelousGame, false,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), value);
    }
}

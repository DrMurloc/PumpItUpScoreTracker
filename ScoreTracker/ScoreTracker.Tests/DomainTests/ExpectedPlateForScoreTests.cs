using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Pins the empirically calibrated score→plate expectation (modal plate of
///     922,765 real records; boundaries measured at 2k granularity — see
///     docs/design/HomePageWidgets/README.md §5). Moving a boundary is a deliberate
///     recalibration, not a refactor.
/// </summary>
public sealed class ExpectedPlateForScoreTests
{
    [Theory]
    [InlineData(850_000, PhoenixPlate.FairGame)]
    [InlineData(963_999, PhoenixPlate.FairGame)]
    [InlineData(964_000, PhoenixPlate.TalentedGame)]
    [InlineData(971_999, PhoenixPlate.TalentedGame)]
    [InlineData(972_000, PhoenixPlate.MarvelousGame)]
    [InlineData(995_999, PhoenixPlate.MarvelousGame)]
    [InlineData(996_000, PhoenixPlate.UltimateGame)]
    [InlineData(999_999, PhoenixPlate.UltimateGame)]
    [InlineData(1_000_000, PhoenixPlate.PerfectGame)]
    public void ExpectedPlateFollowsTheCalibratedBands(int score, PhoenixPlate expected)
    {
        Assert.Equal(expected, ScoringConfiguration.ExpectedPlateForScore(score));
    }

    [Fact]
    public void ExpectationNeverEmitsMinorityPlates()
    {
        // SG/EG/RG are never the population mode in any band; the modal ladder is
        // FG → TG → MG → UG (→ PG at exactly 1M).
        for (var score = 850_000; score <= 1_000_000; score += 1_000)
        {
            var plate = ScoringConfiguration.ExpectedPlateForScore(score);
            Assert.NotEqual(PhoenixPlate.SuperbGame, plate);
            Assert.NotEqual(PhoenixPlate.ExtremeGame, plate);
            Assert.NotEqual(PhoenixPlate.RoughGame, plate);
        }
    }
}

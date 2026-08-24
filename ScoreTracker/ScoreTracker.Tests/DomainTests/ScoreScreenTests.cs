using ScoreTracker.Domain.Records;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Pins the forward score formula to the machine's own answers. Every golden row below is a
///     real judgement-carrying record imported from the official site, chosen so the raw value
///     falls between two integers — the rows that distinguish flooring from ceiling. The game
///     floors: each of these scores is exactly the floored value and one below the ceiled one.
/// </summary>
public sealed class ScoreScreenTests
{
    [Theory]
    [InlineData(907, 24, 4, 1, 2, 909, 983_191)]
    [InlineData(1110, 50, 21, 2, 17, 415, 950_627)]
    [InlineData(622, 2, 0, 0, 1, 428, 995_558)]
    [InlineData(1026, 14, 1, 2, 5, 406, 984_404)]
    [InlineData(524, 22, 1, 0, 0, 546, 982_528)]
    [InlineData(915, 9, 3, 0, 5, 466, 985_756)]
    [InlineData(996, 23, 5, 2, 4, 475, 978_951)]
    [InlineData(1004, 9, 0, 0, 10, 415, 983_800)]
    public void ScoresFloorTheWayTheMachineDoes(int perfects, int greats, int goods, int bads, int misses,
        int maxCombo, int machineScore)
    {
        var screen = new ScoreScreen(perfects, greats, goods, bads, misses, maxCombo);

        Assert.Equal(machineScore, (int)screen.CalculatePhoenixScore);
    }

    [Fact]
    public void APerfectPlayIsExactlyOneMillion()
    {
        var screen = new ScoreScreen(1200, 0, 0, 0, 0, 1200);

        Assert.Equal(1_000_000, (int)screen.CalculatePhoenixScore);
    }

    [Fact]
    public void AnAllGoodPlayScoresJustUnderTwoHundredThousand()
    {
        // The good-spam floor: goods keep the run alive without advancing the combo, so the
        // combo term contributes nothing and the score is 0.995 x 0.2 of a million, floored.
        var screen = new ScoreScreen(0, 0, 500, 0, 0, 0);

        Assert.Equal(199_000, (int)screen.CalculatePhoenixScore);
    }

    [Fact]
    public void AnEmptyScreenIsInvalidAndScoresZero()
    {
        var screen = new ScoreScreen(0, 0, 0, 0, 0, 0);

        Assert.False(screen.IsValid);
        Assert.Equal(0, (int)screen.CalculatePhoenixScore);
    }
}

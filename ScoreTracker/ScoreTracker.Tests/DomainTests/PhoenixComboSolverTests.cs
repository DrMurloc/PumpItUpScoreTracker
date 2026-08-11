using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The solver is the inverse of <see cref="ScoreScreen.CalculatePhoenixScore" />, so the
///     tests that matter score a known combo forward and read it back. Anything else would be
///     checking the solver against a second copy of the same arithmetic.
/// </summary>
public sealed class PhoenixComboSolverTests
{
    private static int? SolveRoundTrip(int perfects, int greats, int goods, int bads, int misses, int maxCombo)
    {
        var screen = new ScoreScreen(perfects, greats, goods, bads, misses, maxCombo);
        var counts = new JudgementCounts(perfects, greats, goods, bads, misses);
        return PhoenixComboSolver.MaxComboFor(counts, screen.CalculatePhoenixScore, counts.NoteCount);
    }

    [Theory]
    [InlineData(900, 40, 5, 2, 3, 947)]
    [InlineData(1200, 0, 0, 0, 0, 1200)]
    [InlineData(742, 118, 30, 12, 44, 800)]
    [InlineData(2400, 60, 20, 10, 10, 2100)]
    [InlineData(300, 20, 10, 5, 40, 120)]
    public void TheSolvedComboIsTheComboThatProducedTheScore(int perfects, int greats, int goods, int bads,
        int misses, int maxCombo)
    {
        Assert.Equal(maxCombo, SolveRoundTrip(perfects, greats, goods, bads, misses, maxCombo));
    }

    /// <summary>A full combo is the one case whose answer is known without inverting anything.</summary>
    [Fact]
    public void AFullComboSolvesToTheNoteCount()
    {
        Assert.Equal(1000, SolveRoundTrip(950, 50, 0, 0, 0, 1000));
    }

    [Fact]
    public void JudgementsThatDoNotCoverTheChartAnswerNothing()
    {
        var counts = new JudgementCounts(400, 10, 2, 1, 5);
        var screen = new ScoreScreen(400, 10, 2, 1, 5, 410);

        // 418 judged notes against a 1,000-note chart: the play stopped early, or the catalog
        // count is stale. Either way the denominator is unknown.
        Assert.Null(PhoenixComboSolver.MaxComboFor(counts, screen.CalculatePhoenixScore, 1000));
    }

    [Fact]
    public void MissingInputsAnswerNothing()
    {
        var counts = new JudgementCounts(900, 40, 5, 2, 3);

        Assert.Null(PhoenixComboSolver.MaxComboFor(null, 985000, 950));
        Assert.Null(PhoenixComboSolver.MaxComboFor(counts, null, 950));
        Assert.Null(PhoenixComboSolver.MaxComboFor(counts, 985000, null));
    }

    [Fact]
    public void AComboTheNoteCountCannotSupportAnswersNothing()
    {
        var counts = new JudgementCounts(900, 40, 5, 2, 3);

        // A perfect score on a chart these judgements could never produce one on: the implied
        // combo lands above the note count, which is a contradiction rather than a reading.
        Assert.Null(PhoenixComboSolver.MaxComboFor(counts, 1000000, counts.NoteCount));
    }
}

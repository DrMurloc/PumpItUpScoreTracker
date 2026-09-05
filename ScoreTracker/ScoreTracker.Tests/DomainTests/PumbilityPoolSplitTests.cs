using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The merged fifty split by type (docs/design/pumbility-overhaul.md D58): what one player's
///     fifty is made of across both types, and the average over the peers who hold a full one.
/// </summary>
public sealed class PumbilityPoolSplitTests
{
    [Fact]
    public void AFiftyIsTheHighestPricedRecordsAcrossBothTypes()
    {
        // Thirty-five singles from 400 down and twenty-five doubles from 390 down: the fifty
        // highest across both are the singles 400 to 371 and the doubles 390 to 371 — thirty and
        // twenty — and the single at 370 is the first left out, however many of its type are above.
        var records = Enumerable.Range(0, 35).Select(i => new PricedRecord(ChartType.Single, 400 - i))
            .Concat(Enumerable.Range(0, 25).Select(i => new PricedRecord(ChartType.Double, 390 - i)))
            .ToArray();

        var split = PumbilityPoolSplit.Of(records);

        Assert.Equal(50, split.Count);
        Assert.Equal(30, split.SinglesCount);
        Assert.Equal(20, split.DoublesCount);
        Assert.Equal(Enumerable.Range(371, 30).Sum(), split.SinglesValue);
        Assert.Equal(Enumerable.Range(371, 20).Sum(), split.DoublesValue);
        Assert.Equal(split.SinglesValue + split.DoublesValue, split.Total);
        Assert.True(PumbilityPoolSplit.IsFull(split));
    }

    [Fact]
    public void AZeroRatedRecordNeverTakesASlot()
    {
        // A broken run and a sub-ten chart price at zero and hold no slot (§3.8).
        var records = Enumerable.Range(0, 10).Select(i => new PricedRecord(ChartType.Single, 300 + i))
            .Append(new PricedRecord(ChartType.Double, 0))
            .ToArray();

        var split = PumbilityPoolSplit.Of(records);

        Assert.Equal(10, split.Count);
        Assert.Equal(0, split.DoublesCount);
        Assert.False(PumbilityPoolSplit.IsFull(split));
    }

    [Fact]
    public void TheAverageIsTheMeanOverThePeersHoldingAFullFifty()
    {
        var thirtySingles = Fifty(singles: 30);
        var fortySingles = Fifty(singles: 40);
        var short_ = Enumerable.Range(0, 20).Select(i => new PricedRecord(ChartType.Single, 350 - i)).ToArray();

        var average = PumbilityPoolSplit.Average(new[] { thirtySingles, fortySingles, short_ });

        Assert.NotNull(average);
        Assert.Equal(2, average!.Peers);
        Assert.Equal(35, average.SinglesCount);
        Assert.Equal(15, average.DoublesCount);
        Assert.Equal((PumbilityPoolSplit.Of(thirtySingles).SinglesValue + PumbilityPoolSplit.Of(fortySingles).SinglesValue) / 2,
            average.SinglesValue, 6);
    }

    [Fact]
    public void NobodyHoldingAFullFiftyMeansNoAverage()
    {
        var short_ = Enumerable.Range(0, 49).Select(i => new PricedRecord(ChartType.Double, 350 - i)).ToArray();

        Assert.Null(PumbilityPoolSplit.Average(new IEnumerable<PricedRecord>[] { short_ }));
        Assert.Null(PumbilityPoolSplit.Average(System.Array.Empty<IEnumerable<PricedRecord>>()));
    }

    /// <summary>A full fifty with the given number of singles at 400 and the rest doubles at 380.</summary>
    private static IEnumerable<PricedRecord> Fifty(int singles)
    {
        return Enumerable.Range(0, singles).Select(_ => new PricedRecord(ChartType.Single, 400))
            .Concat(Enumerable.Range(0, 50 - singles).Select(_ => new PricedRecord(ChartType.Double, 380)))
            .ToArray();
    }
}

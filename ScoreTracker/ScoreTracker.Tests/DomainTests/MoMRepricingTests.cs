using System;
using System.Linq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The re-pricing split (docs/design/march-of-murlocs.md §11.3, D20) on 김재현's real
///     August 2024 session against Winter 2025's frozen configuration. Ten of his 32 charts
///     were re-rated upward, and every level-23-and-up rating rose while the grade table
///     tightened — two different things moved, and the split names each.
/// </summary>
public sealed class MoMRepricingTests
{
    [Fact]
    public void ReRunningTheOldSessionUnderItsOwnSeasonReproducesItsStoredTotal()
    {
        var split = MoMRepricing.Split(MoMRealSessions.August2024(), MoMRealSessions.August2024Total,
            MoMRealSessions.MoM2Season(), MoMRealSessions.Winter2025Season());

        Assert.Equal(44139, split.RecomputedOldTotal);
        Assert.Equal(44139, split.OldTotal);
    }

    [Fact]
    public void TheSplitNamesWhatTheBalanceMovedAndWhatTheTablesMoved()
    {
        var split = MoMRepricing.Split(MoMRealSessions.August2024(), MoMRealSessions.August2024Total,
            MoMRealSessions.MoM2Season(), MoMRealSessions.Winter2025Season());

        Assert.Equal(801, split.ChartsReRated);
        Assert.Equal(2447, split.TablesReCut);
        Assert.Equal(47865, split.RepricedTotal);
        Assert.Equal(3726, split.TotalShift);
        // The tables' re-cut was worth about three times the chart re-ratings.
        Assert.True(split.TablesReCut > 3 * split.ChartsReRated);
    }

    [Fact]
    public void ThePartsMultiplySoTheySumToLessThanTheTotalShift()
    {
        var split = MoMRepricing.Split(MoMRealSessions.August2024(), MoMRealSessions.August2024Total,
            MoMRealSessions.MoM2Season(), MoMRealSessions.Winter2025Season());

        Assert.True(split.ChartsReRated + split.TablesReCut < split.TotalShift);
    }

    [Fact]
    public void TheSameSeasonOnBothSidesMovesNothing()
    {
        var season = MoMRealSessions.Winter2025Season();
        var split = MoMRepricing.Split(MoMRealSessions.Winter2025(), MoMRealSessions.Winter2025Total, season, season);

        Assert.Equal(0, split.ChartsReRated);
        Assert.Equal(0, split.TablesReCut);
        Assert.Equal(0, split.TotalShift);
    }

    [Fact]
    public void ACatalogThatMovedUnderTheOldSessionShiftsTheLedgerNotTheStoredTotal()
    {
        // A chart's length changed since 2024: every recomputation moves alike, so the ledger
        // still leads with the stored total and the repriced line carries only the seasons' doing.
        var rows = MoMRealSessions.August2024().ToList();
        var longer = rows[0] with
        {
            Chart = MoMRealSessions.Chart(rows[0].Chart.Song.Name + " (re-timed)", (int)rows[0].Chart.Level,
                (int)rows[0].Chart.Song.Duration.TotalSeconds + 30)
        };
        rows[0] = longer;
        var split = MoMRepricing.Split(rows, MoMRealSessions.August2024Total,
            MoMRealSessions.MoM2Season(), MoMRealSessions.Winter2025Season());

        Assert.Equal(44139, split.OldTotal);
        Assert.NotEqual(split.OldTotal, split.RecomputedOldTotal);
        Assert.Equal(split.OldTotal + (split.TotalShift), split.RepricedTotal);
    }

    [Fact]
    public void SwappingTheSnapshotCopiesTheTablesRatherThanMutatingThem()
    {
        var source = MoMRealSessions.MoM2Season().Scoring;
        var snapshot = new System.Collections.Generic.Dictionary<Guid, double> { [Guid.NewGuid()] = 24.9 };

        var copy = MoMRepricing.WithSnapshot(source, snapshot);

        Assert.Same(snapshot, copy.ChartLevelSnapshot);
        Assert.NotSame(source.ChartLevelSnapshot, copy.ChartLevelSnapshot);
        Assert.NotSame(source.LevelRatings, copy.LevelRatings);
        Assert.Equal(source.LevelRatings, copy.LevelRatings);
        Assert.Equal(source.LetterGradeModifiers[PhoenixLetterGrade.AAA], copy.LetterGradeModifiers[PhoenixLetterGrade.AAA]);
        Assert.Equal(source.AdjustToTime, copy.AdjustToTime);
        Assert.Equal(source.ContinuousLetterGradeScale, copy.ContinuousLetterGradeScale);
        Assert.Equal(source.PgLetterGradeModifier, copy.PgLetterGradeModifier);
        Assert.Equal(source.Mix, copy.Mix);
    }
}

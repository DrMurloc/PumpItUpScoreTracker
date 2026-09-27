using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The one rule every announcement counts a chart by — the typed-entry batch and an import's
///     announcement alike (docs/design/import-restart-recovery.md §0).
/// </summary>
public sealed class ScoreChangeFoldTests
{
    private static readonly Guid Chart = Guid.NewGuid();

    [Fact]
    public void AChartThatBecameAPassIsANewPassEvenIfItWentUpFirst()
    {
        // A break raised to a higher break, then passed: the pass is what happened to the chart.
        var fold = new ScoreChangeFold();

        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.Upscore, 900000));
        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.NewPass));

        var batch = fold.ToBatch(MixEnum.Phoenix2, null);
        Assert.Equal(new[] { Chart }, batch.NewChartIds);
        Assert.Empty(batch.UpscoredChartIds);
    }

    [Fact]
    public void AnUpscoreAfterANewPassDoesNotTurnItBackIntoAnUpscore()
    {
        var fold = new ScoreChangeFold();

        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.NewPass));
        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.Upscore, 950000));

        var batch = fold.ToBatch(MixEnum.Phoenix2, null);
        Assert.Equal(new[] { Chart }, batch.NewChartIds);
        Assert.Empty(batch.UpscoredChartIds);
    }

    [Fact]
    public void AChartRaisedTwiceReadsAsOneStepFromWhereThePlayerStarted()
    {
        // The walk raised it to 960k and the re-read raised it again to 980k: the announcement says
        // "from 950k", the score the player had before the run.
        var fold = new ScoreChangeFold();

        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.Upscore, 950000));
        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.Upscore, 960000));

        Assert.Equal(950000, fold.ToBatch(MixEnum.Phoenix2, null).UpscoredChartIds[Chart]);
    }

    [Fact]
    public void SavesThatChangedNothingLeaveTheFoldEmpty()
    {
        var fold = new ScoreChangeFold();

        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.None));
        fold.Add(default(ScoreSaveResult));

        Assert.True(fold.IsEmpty);
    }

    [Fact]
    public void AnOpenBatchFoldsInBeforeTheRunsOwnSaves()
    {
        // A score typed in during an import joins its announcement; the typed score came first, so
        // its before-state is the one the announcement keeps.
        var fold = new ScoreChangeFold();
        var other = Guid.NewGuid();

        fold.Add(new PendingScoreBatch(MixEnum.Phoenix2, new[] { other },
            new Dictionary<Guid, int> { [Chart] = 940000 }));
        fold.Add(new ScoreSaveResult(Chart, ScoreSaveChange.Upscore, 955000));

        var batch = fold.ToBatch(MixEnum.Phoenix2, Guid.Empty);
        Assert.Equal(new[] { other }, batch.NewChartIds);
        Assert.Equal(940000, batch.UpscoredChartIds[Chart]);
    }
}

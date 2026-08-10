using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The replay has to reproduce rules that live in UpdatePhoenixRecordHandler's write path, and
///     the journal it reads from records MORE than that path ever announced. Every test here is a
///     way of getting that wrong (docs/design/import-restart-recovery.md §5).
/// </summary>
public sealed class SessionReplayBuilderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private static ScoreJournalEntry Row(Guid chartId, int? score, bool isBroken, DateTimeOffset at,
        MixEnum mix = MixEnum.Phoenix, bool isBest = true, PhoenixPlate? plate = null)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.OfficialImportSource, User, chartId,
            score == null ? null : PhoenixScore.From(score.Value), plate, isBroken, mix, Guid.NewGuid(),
            null, isBest);
    }

    [Fact]
    public void AChartWithNoPriorHistoryIsANewPass()
    {
        var chart = Guid.NewGuid();
        var session = new[] { Row(chart, 900_000, false, T0) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, session);

        var change = Assert.Single(changes);
        Assert.Equal(chart, change.ChartId);
        Assert.True(change.IsNewPass);
        Assert.Null(change.OldScore);
    }

    [Fact]
    public void ABrokenChartThatPassesIsANewPass()
    {
        var chart = Guid.NewGuid();
        var before = Row(chart, 400_000, true, T0.AddDays(-1));
        var session = new[] { Row(chart, 910_000, false, T0) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, new[] { before }.Concat(session).ToArray());

        var change = Assert.Single(changes);
        Assert.True(change.IsNewPass);
        Assert.Null(change.OldScore);
    }

    [Fact]
    public void ARaisedScoreIsAnUpscoreCarryingTheOldValue()
    {
        var chart = Guid.NewGuid();
        var before = Row(chart, 900_000, false, T0.AddDays(-1));
        var session = new[] { Row(chart, 950_000, false, T0) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, new[] { before }.Concat(session).ToArray());

        var change = Assert.Single(changes);
        Assert.False(change.IsNewPass);
        Assert.Equal(900_000, change.OldScore);
    }

    /// <summary>
    ///     The journal records every change to the record; the batch only ever held new passes and
    ///     upscores. A plate improvement at an unchanged score is journaled and was deliberately
    ///     never announced, so replaying it would announce something that never happened.
    /// </summary>
    [Fact]
    public void APlateOnlyImprovementIsNotReplayed()
    {
        var chart = Guid.NewGuid();
        var before = Row(chart, 950_000, false, T0.AddDays(-1), plate: PhoenixPlate.MarvelousGame);
        var session = new[] { Row(chart, 950_000, false, T0, plate: PhoenixPlate.SuperbGame) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, new[] { before }.Concat(session).ToArray());

        Assert.Empty(changes);
    }

    /// <summary>
    ///     The accumulator keeps NewCharts and UpscoreCharts disjoint and lets the new pass win, so
    ///     a chart cleared and then improved inside one session is ONE announcement.
    /// </summary>
    [Fact]
    public void AChartPassedThenImprovedInOneSessionIsASingleNewPass()
    {
        var chart = Guid.NewGuid();
        var session = new[]
        {
            Row(chart, 880_000, false, T0),
            Row(chart, 940_000, false, T0.AddMinutes(20))
        };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, session);

        var change = Assert.Single(changes);
        Assert.True(change.IsNewPass);
        Assert.Null(change.OldScore);
    }

    [Fact]
    public void ObservedPlaysThatNeverBecameTheRecordAreExcluded()
    {
        var chart = Guid.NewGuid();
        var session = new[] { Row(chart, 700_000, false, T0, isBest: false) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, session);

        Assert.Empty(changes);
    }

    /// <summary>
    ///     ⚠ The one that has already gone wrong once. GetChartHistories is cross-mix — a returning
    ///     song carries one ChartId across Phoenix and Phoenix 2 — so an unfiltered "row before
    ///     this one" hands a Phoenix 1 play to a Phoenix 2 session as its before-state. Here the
    ///     P1 play is a HIGHER score, so trusting it would turn a genuine new pass into no
    ///     announcement at all.
    /// </summary>
    [Fact]
    public void APhoenix1PlayIsNotTheBeforeStateForAPhoenix2Session()
    {
        var chart = Guid.NewGuid();
        var phoenix1Play = Row(chart, 990_000, false, T0.AddDays(-30));
        var session = new[] { Row(chart, 800_000, false, T0, MixEnum.Phoenix2) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix2, session,
            new[] { phoenix1Play }.Concat(session).ToArray());

        var change = Assert.Single(changes);
        Assert.True(change.IsNewPass);
        Assert.Null(change.OldScore);
    }

    [Fact]
    public void RowsFromAnotherMixInTheSessionItselfAreIgnored()
    {
        var phoenixChart = Guid.NewGuid();
        var otherMixChart = Guid.NewGuid();
        var session = new[]
        {
            Row(phoenixChart, 900_000, false, T0),
            Row(otherMixChart, 900_000, false, T0, MixEnum.Phoenix2)
        };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, session);

        Assert.Equal(phoenixChart, Assert.Single(changes).ChartId);
    }

    [Fact]
    public void AScoreThatWentDownIsNotReplayed()
    {
        // Manual overwrite territory: the record changed, so it is journaled, but a decrease was
        // never a batch entry.
        var chart = Guid.NewGuid();
        var before = Row(chart, 960_000, false, T0.AddDays(-1));
        var session = new[] { Row(chart, 900_000, false, T0) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session, new[] { before }.Concat(session).ToArray());

        Assert.Empty(changes);
    }

    [Fact]
    public void AnEmptySessionProducesNothing()
    {
        Assert.Empty(SessionReplayBuilder.Build(MixEnum.Phoenix, Array.Empty<ScoreJournalEntry>(),
            Array.Empty<ScoreJournalEntry>()));
    }

    [Fact]
    public void HistoryNewerThanTheSessionDoesNotBecomeItsBeforeState()
    {
        // A later manual correction must not be read as what the chart stood at beforehand.
        var chart = Guid.NewGuid();
        var before = Row(chart, 900_000, false, T0.AddDays(-1));
        var after = Row(chart, 999_000, false, T0.AddDays(1));
        var session = new[] { Row(chart, 950_000, false, T0) };

        var changes = SessionReplayBuilder.Build(MixEnum.Phoenix, session,
            new[] { before, after }.Concat(session).ToArray());

        Assert.Equal(900_000, Assert.Single(changes).OldScore);
    }
}

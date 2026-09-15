using System;
using System.Collections.Generic;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SeasonCountingPolicyTests
{
    private static readonly SeasonId Summer = SeasonId.From(2026, 3);
    private static readonly SeasonId Fall = SeasonId.From(2026, 4);

    private static readonly DateTimeOffset InSummer = new(2026, 8, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InFall = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BeforeSeasons = new(2026, 3, 1, 20, 0, 0, TimeSpan.Zero);

    // Three days into Fall. Summer ended 2026-09-30 23:59:59 UTC-5 and the roll has not stamped
    // it yet, which changes nothing: a closed season is closed whether or not its seal has landed.
    private static readonly DateTimeOffset JustIntoFall = new(2026, 10, 3, 20, 0, 0, TimeSpan.Zero);

    private static SeasonRecord Season(SeasonId id, DateTimeOffset? sealedAt = null)
    {
        var offset = TimeSpan.FromHours(-5);
        var month = id.Quarter * 3;
        return new SeasonRecord(id, $"Q{id.Quarter} {id.Year}",
            new DateTimeOffset(new DateTime(id.Year, month - 2, 1, 0, 0, 0), offset),
            new DateTimeOffset(new DateTime(id.Year, month, DateTime.DaysInMonth(id.Year, month), 23, 59, 59), offset),
            sealedAt, false);
    }

    private static readonly IReadOnlyList<SeasonRecord> Both = new[] { Season(Fall), Season(Summer) };

    [Fact]
    public void AnInWindowPlayCountsForTheSeasonRunningNow()
    {
        Assert.Equal(Fall, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InFall,
            raisedExistingRecord: false, Both, InFall));
    }

    [Fact]
    public void AnUpscoreWearingAPreSeasonDateCountsForTheSeasonRunningWhenWeSawIt()
    {
        // The Phoenix 2 best-list card carries the chart's FIRST play, so this is what catches an
        // in-season improvement on a chart the player first touched years ago.
        Assert.Equal(Fall, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, BeforeSeasons,
            raisedExistingRecord: true, Both, InFall));
    }

    [Fact]
    public void AFirstEverImportersOldCardsCountForNothing()
    {
        // Nothing to raise, and dated before any season: an old score being seen for the first time.
        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, BeforeSeasons,
            raisedExistingRecord: false, Both, InFall));
    }

    [Theory]
    [InlineData(ScoreJournalEntry.ManualSource)]
    [InlineData(ScoreJournalEntry.CsvSource)]
    [InlineData(ScoreJournalEntry.BackfillSource)]
    public void OnlyAnOfficialImportSeedsASeasonsPool(string source)
    {
        Assert.Null(SeasonCountingPolicy.SeasonFor(source, InFall, raisedExistingRecord: false, Both, InFall));
        Assert.Null(SeasonCountingPolicy.SeasonFor(source, InFall, raisedExistingRecord: true, Both, InFall));
    }

    [Fact]
    public void APlayDatedInAnEndedSeasonIsLostRatherThanLanding()
    {
        // No grace (D13, owner 2026-09-14). Summer closed three days ago and the roll has not sealed
        // it yet, and it still takes nothing: a September play imported now counts for no season at
        // all, while a play of the same import dated in October is Fall's.
        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: false, Both, JustIntoFall));
        Assert.Equal(Fall, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, JustIntoFall,
            raisedExistingRecord: false, Both, JustIntoFall));
    }

    [Fact]
    public void AnUpscoreImportedAfterTheBoundaryBelongsToTheSeasonRunningNow()
    {
        // The other half of the same rule: the raise is something this import watched happen, so it
        // is the new season's — never the ended one's, which is exactly what no grace means.
        Assert.Equal(Fall, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: true, Both, JustIntoFall));
    }

    [Fact]
    public void ASealedSeasonIsNeverWritten()
    {
        // The seal is the one thing that can stop the running season too: an admin sealing early,
        // or a roll that stamped the quarter we stand in. Nothing is written while it holds.
        var sealedFall = new[] { Season(Fall, sealedAt: JustIntoFall), Season(Summer) };

        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InFall,
            raisedExistingRecord: false, sealedFall, InFall));
        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: true, sealedFall, InFall));
    }

    [Fact]
    public void BeforeTheFirstRollNothingCounts()
    {
        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InFall,
            raisedExistingRecord: true, Array.Empty<SeasonRecord>(), InFall));
    }

    [Fact]
    public void AnUpscoreWithNoRunningSeasonCountsForNothing()
    {
        // Only the previous season exists — the roll has not opened the quarter we stand in yet.
        var onlySummer = new[] { Season(Summer, sealedAt: JustIntoFall) };

        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: true, onlySummer, InFall));
    }
}

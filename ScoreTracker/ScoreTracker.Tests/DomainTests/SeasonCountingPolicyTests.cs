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

    // The seven-day grace: Summer ended 2026-09-30 23:59:59 UTC-5 and is not yet sealed.
    private static readonly DateTimeOffset InTheGrace = new(2026, 10, 3, 20, 0, 0, TimeSpan.Zero);

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
    public void AnInWindowPlayCountsForTheSeasonThatHoldsIt()
    {
        Assert.Equal(Summer, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: false, Both, InFall));
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
    public void DuringTheGraceWeekAnInWindowPlayStillLandsOnTheEndedSeason()
    {
        // Two seasons are live at once for seven days (D13): a play dated in September lands on
        // Summer, one dated in October on Fall, both while the clock stands in the grace.
        Assert.Equal(Summer, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: false, Both, InTheGrace));
        Assert.Equal(Fall, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InTheGrace,
            raisedExistingRecord: false, Both, InTheGrace));
    }

    [Fact]
    public void ASealedSeasonIsNeverWritten()
    {
        var sealedSummer = new[] { Season(Fall), Season(Summer, sealedAt: InTheGrace) };

        // A late play for the sealed season is ignored outright...
        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: false, sealedSummer, InFall));
        // ...but a card dated inside it that raised a record we held is a raise the import watched
        // happen, so it belongs to the season running now rather than being dropped.
        Assert.Equal(Fall, SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: true, sealedSummer, InFall));
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
        // Only the sealed season exists — the roll has not opened the quarter we stand in yet.
        var onlySummer = new[] { Season(Summer, sealedAt: InTheGrace) };

        Assert.Null(SeasonCountingPolicy.SeasonFor(ScoreJournalEntry.OfficialImportSource, InSummer,
            raisedExistingRecord: true, onlySummer, InFall));
    }
}

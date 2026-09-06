using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Finding a session inside a night (march-of-murlocs.md §11.4 and D32). Both nights below are
///     production pulls, and they are the two the rule was written against: a public player's
///     8 August Doubles run is a session, and DrMurloc's 14 August night is an evening of play that
///     never becomes one.
/// </summary>
public sealed class MoMSessionDetectorTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(105);
    private static readonly TimeSpan MaxRest = TimeSpan.FromMinutes(50);
    private static readonly DateTimeOffset Midnight = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);

    // ---- D32: does this night hold a session-shaped window? ----------------------------------

    [Fact]
    public void ThePublicPlayersEighthOfAugustNightIsASessionAndDrMurlocsFourteenthIsNot()
    {
        var found = MoMSessionDetector.FindSessionWindow(AugustEighth(), Window, MaxRest);

        Assert.NotNull(found);
        Assert.Equal(ChartType.Double, found!.Type);
        Assert.Equal(31, found.Charts);
        Assert.Equal(TimeSpan.FromMinutes(61.5), found.SongTime);
        Assert.Equal(TimeSpan.FromMinutes(43.5), found.Rest);

        // The 14th tops out at 28.2 minutes of song in any 1:45, which is 76.8 of rest.
        Assert.Null(MoMSessionDetector.FindSessionWindow(AugustFourteenth(), Window, MaxRest));
    }

    [Fact]
    public void TheWindowSlidesSoANightsOwnStartAndEndDoNotMatter()
    {
        // The same run, with an hour of nothing in front of it and a stray play behind.
        var padded = AugustEighth()
            .Select(p => p with { PlayedAt = p.PlayedAt.AddHours(1) })
            .Prepend(Play(0, 120, ChartType.Double, chart: 99))
            .Append(Play(60 * 60 * 9, 120, ChartType.Double, chart: 98))
            .ToArray();

        var found = MoMSessionDetector.FindSessionWindow(padded, Window, MaxRest);

        Assert.NotNull(found);
        Assert.Equal(31, found!.Charts);
    }

    [Fact]
    public void PlaysOfTheOtherTypeInsideTheWindowBecomeRestRatherThanBeingIgnored()
    {
        // Four Doubles that would fit comfortably, with a Singles set wedged between them: the
        // Singles time is not counted, so the window's rest grows and the night fails honestly.
        var mixed = new[]
        {
            Play(0, 120, ChartType.Double, chart: 1),
            Play(150, 120, ChartType.Double, chart: 2),
            Play(300, 3000, ChartType.Single, chart: 3),
            Play(3400, 120, ChartType.Double, chart: 4)
        };

        var found = MoMSessionDetector.FindSessionWindow(mixed, Window, MaxRest);

        Assert.Null(found);
        Assert.Equal(TimeSpan.FromMinutes(6), Sum(mixed.Where(p => p.Type == ChartType.Double)));
    }

    // ---- §11.4: the import dialog's blocks and checks -----------------------------------------

    [Fact]
    public void ANightSplitsWhereTheMachineSatIdleLongerThanFifteenMinutes()
    {
        var blocks = MoMSessionDetector.Split(AugustFourteenth());

        Assert.Equal(3, blocks.Count);
        Assert.Equal((0, 0, 1), (blocks[0].StartIndex, blocks[0].EndIndex, blocks[0].Plays));
        Assert.Equal((1, 13, 13), (blocks[1].StartIndex, blocks[1].EndIndex, blocks[1].Plays));
        Assert.Equal((14, 23, 10), (blocks[2].StartIndex, blocks[2].EndIndex, blocks[2].Plays));
    }

    [Fact]
    public void AnUninterruptedNightIsOneBlock()
    {
        Assert.Single(MoMSessionDetector.Split(AugustEighth()));
        Assert.Empty(MoMSessionDetector.Split(Array.Empty<MoMPlay>()));
    }

    [Fact]
    public void TheDialogOpensOnTheBlockThatWouldPutTheMostChartsOnTheBoard()
    {
        var suggested = MoMSessionDetector.Suggest(AugustFourteenth(), ChartType.Double, Window);

        // The late block carries eight charts against the middle block's seven.
        Assert.NotNull(suggested);
        Assert.Equal(14, suggested!.StartIndex);
        Assert.Equal(23, suggested.EndIndex);
    }

    [Fact]
    public void TheLateBlockCountsItsChartsOnceAndReportsWhatItLeftOut()
    {
        var checks = MoMSessionDetector.Check(AugustFourteenth(), 14, 23, ChartType.Double, Window);

        // Nine Doubles plays of eight charts -- he played Ugly Dee D17 twice -- and the stage
        // break that ended the night.
        Assert.Equal(8, checks.Charts);
        Assert.Equal(1, checks.RepeatPlays);
        Assert.Equal(1, checks.StageBreaksSkipped);
        Assert.Equal(0, checks.WrongTypeSkipped);
        Assert.Equal(new TimeSpan(0, 16, 35), checks.SongTime);
        Assert.False(checks.OverWindowBeforeLast);
        Assert.False(checks.SpanOverWindow);
        Assert.Null(checks.LongestBreak);
    }

    [Fact]
    public void SelectingTwoBlocksWarnsOnTheWallClockAndNamesTheBreakInTheMiddle()
    {
        var night = AugustFourteenth();

        var checks = MoMSessionDetector.Check(night, 1, 23, ChartType.Double, Window);

        Assert.Equal(11, checks.Charts);
        Assert.Equal(6, checks.RepeatPlays);
        Assert.Equal(2, checks.StageBreaksSkipped);
        Assert.Equal(4, checks.WrongTypeSkipped);
        Assert.Equal(new TimeSpan(2, 16, 37), checks.Span);
        // Song time still fits, so this is a judgement call rather than a block.
        Assert.False(checks.OverWindowBeforeLast);
        Assert.True(checks.SpanOverWindow);
        // Trimming an end would not help: the 44-minute break is in the middle, before the play
        // that opens the late block.
        Assert.NotNull(checks.LongestBreak);
        Assert.Equal(new TimeSpan(0, 43, 36), checks.LongestBreak!.Length);
        Assert.Equal(night[14].PlayedAt, checks.LongestBreak.BeforePlayedAt);
        Assert.Equal(night[14].ChartId, checks.LongestBreak.BeforeChartId);
    }

    [Fact]
    public void SongTimeOverTheWindowBeforeTheLastChartIsAHardBlock()
    {
        // Fifty-three two-minute charts: 1:44 before the last one, so it still fits.
        var fits = Run(53);
        Assert.False(MoMSessionDetector.Check(fits, 0, fits.Count - 1, ChartType.Double, Window)
            .OverWindowBeforeLast);

        var over = Run(54);
        var checks = MoMSessionDetector.Check(over, 0, over.Count - 1, ChartType.Double, Window);
        Assert.True(checks.OverWindowBeforeLast);
        // A hard block suppresses the soft warning: there is nothing to weigh up.
        Assert.False(checks.SpanOverWindow);
    }

    [Fact]
    public void TheClosingChartMayOverhangTheWindow()
    {
        // 1:44 of song, then a four-minute closer that starts inside and runs past the end.
        var run = Run(52).Append(Play(52 * 150, 240, ChartType.Double, chart: 500)).ToArray();

        var checks = MoMSessionDetector.Check(run, 0, run.Length - 1, ChartType.Double, Window);

        Assert.False(checks.OverWindowBeforeLast);
        Assert.Equal(53, checks.Charts);
        Assert.True(checks.SongTime > Window);
    }

    [Fact]
    public void AGapIsMeasuredFromWhenTheEarlierSongEndedAndNeverGoesNegative()
    {
        var plays = new[]
        {
            Play(0, 600, ChartType.Double, chart: 1),
            Play(300, 120, ChartType.Double, chart: 2)
        };

        Assert.Equal(TimeSpan.Zero, MoMSessionDetector.GapBefore(plays, 1));
        Assert.Single(MoMSessionDetector.Split(plays));
    }

    [Fact]
    public void ARangeOutsideTheListIsClampedRatherThanThrowing()
    {
        var night = AugustFourteenth();

        var checks = MoMSessionDetector.Check(night, -5, 500, ChartType.Double, Window);

        Assert.Equal(MoMSessionDetector.Check(night, 0, night.Count - 1, ChartType.Double, Window), checks);
        Assert.Equal(0, MoMSessionDetector.Check(Array.Empty<MoMPlay>(), 0, 3, ChartType.Double, Window).Charts);
    }

    // ---- fixtures ----------------------------------------------------------------------------

    private static TimeSpan Sum(IEnumerable<MoMPlay> plays) =>
        TimeSpan.FromTicks(plays.Sum(p => p.Duration.Ticks));

    /// <summary>A metronome of two-minute charts thirty seconds apart, for the window arithmetic.</summary>
    private static IReadOnlyList<MoMPlay> Run(int charts) =>
        Enumerable.Range(0, charts).Select(i => Play(i * 150, 120, ChartType.Double, chart: i)).ToArray();

    private static MoMPlay Play(int offsetSeconds, int durationSeconds, ChartType type, bool stageBroken = false,
        int chart = 0) =>
        new(Chart(chart), Midnight.AddSeconds(offsetSeconds), TimeSpan.FromSeconds(durationSeconds), type,
            stageBroken);

    private static Guid Chart(int n) => new(n, 0, 0, new byte[8]);

    private static IReadOnlyList<MoMPlay> Night(IEnumerable<(int Offset, int Seconds, ChartType Type, bool Broken, int Chart)> rows) =>
        rows.Select(r => Play(r.Offset, r.Seconds, r.Type, r.Broken, r.Chart)).ToArray();

    /// <summary>A public player's real Doubles night, 05:20 to 07:18 on 8 August 2026: 35 plays, one type.</summary>
    /// <remarks>Offsets are seconds from 2026-08-08 05:20:51 UTC.</remarks>
    private static IReadOnlyList<MoMPlay> AugustEighth() => Night(new (int, int, ChartType, bool, int)[]
    {
        (0, 117, ChartType.Double, false, 0),
        (179, 120, ChartType.Double, false, 1),
        (367, 119, ChartType.Double, false, 2),
        (532, 116, ChartType.Double, false, 3),
        (713, 118, ChartType.Double, false, 4),
        (952, 124, ChartType.Double, false, 5),
        (1122, 122, ChartType.Double, false, 6),
        (1331, 126, ChartType.Double, false, 7),
        (1556, 135, ChartType.Double, false, 8),
        (1757, 124, ChartType.Double, false, 9),
        (1995, 121, ChartType.Double, false, 10),
        (2194, 113, ChartType.Double, false, 11),
        (2413, 125, ChartType.Double, false, 12),
        (2617, 118, ChartType.Double, false, 13),
        (2831, 107, ChartType.Double, false, 14),
        (3097, 121, ChartType.Double, false, 15),
        (3337, 116, ChartType.Double, false, 16),
        (3518, 125, ChartType.Double, false, 17),
        (3696, 113, ChartType.Double, false, 18),
        (3912, 104, ChartType.Double, false, 19),
        (4185, 104, ChartType.Double, false, 19),
        (4424, 121, ChartType.Double, false, 20),
        (4634, 122, ChartType.Double, false, 21),
        (4855, 126, ChartType.Double, false, 22),
        (5094, 128, ChartType.Double, false, 23),
        (5324, 128, ChartType.Double, false, 24),
        (5485, 128, ChartType.Double, false, 24),
        (5651, 102, ChartType.Double, false, 25),
        (5833, 124, ChartType.Double, false, 26),
        (6048, 112, ChartType.Double, false, 27),
        (6286, 111, ChartType.Double, false, 28),
        (6457, 111, ChartType.Double, false, 28),
        (6644, 111, ChartType.Double, false, 29),
        (6860, 122, ChartType.Double, false, 30),
        (7086, 117, ChartType.Double, false, 31),
    });

    /// <summary>DrMurloc's real night of 14 August 2026: 24 plays, Singles and Doubles mixed, two stage breaks.</summary>
    /// <remarks>Offsets are seconds from 2026-08-14 23:38:29 UTC.</remarks>
    private static IReadOnlyList<MoMPlay> AugustFourteenth() => Night(new (int, int, ChartType, bool, int)[]
    {
        (0, 123, ChartType.Double, false, 0),
        (1483, 148, ChartType.Double, false, 1),
        (1740, 125, ChartType.Double, false, 2),
        (1882, 120, ChartType.Double, true, 3),
        (2354, 146, ChartType.Single, false, 4),
        (2678, 146, ChartType.Single, false, 4),
        (2878, 146, ChartType.Single, false, 4),
        (3147, 121, ChartType.Single, false, 5),
        (3792, 123, ChartType.Double, false, 6),
        (3983, 123, ChartType.Double, false, 7),
        (4140, 123, ChartType.Double, false, 7),
        (4323, 108, ChartType.Double, false, 8),
        (4605, 115, ChartType.Double, false, 9),
        (5066, 107, ChartType.Double, false, 10),
        (7789, 115, ChartType.Double, false, 9),
        (7949, 123, ChartType.Double, false, 6),
        (8135, 123, ChartType.Double, false, 7),
        (8277, 107, ChartType.Double, false, 10),
        (8423, 111, ChartType.Double, false, 11),
        (8820, 96, ChartType.Double, false, 12),
        (8939, 96, ChartType.Double, false, 12),
        (9096, 105, ChartType.Double, false, 13),
        (9345, 119, ChartType.Double, false, 14),
        (9562, 118, ChartType.Double, true, 15),
    });
}

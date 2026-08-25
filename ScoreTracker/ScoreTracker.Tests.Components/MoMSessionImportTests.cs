using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The import dialog's gap arithmetic (march-of-murlocs.md §11.4): plays split wherever
///     a gap exceeds fifteen minutes, the longest block pre-selects with ties to the most
///     recent, one click moves the nearer selection end, a single-play block imports, and
///     D10's hard block counts the songs BEFORE the last play — the closing chart may
///     overhang the window (§2.9).
/// </summary>
public sealed class MoMSessionImportTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 20, 16, 12, 0, TimeSpan.FromHours(9));
    private static readonly TimeSpan MaxTime = TimeSpan.FromMinutes(105);

    [Fact]
    public void SplitsAtGapsOverFifteenMinutesAndPreselectsTheLongestBlock()
    {
        // Warm-up (3) · afternoon away · the session (4) · evening (2): the session block wins.
        var plays = Sequence(
            (120, 42), (120, 38), (120, TimeSpan.FromHours(3).TotalSeconds),
            (120, 30), (266, 45), (120, 31), (378, TimeSpan.FromHours(2).TotalSeconds),
            (120, 44), (120, 0));

        var blocks = MoMSessionImport.Blocks(plays);

        Assert.Equal(3, blocks.Count);
        Assert.Equal((3, 6), blocks[1]);
        Assert.Equal((3, 6), MoMSessionImport.BestBlock(plays));
    }

    [Fact]
    public void TiesGoToTheMostRecentBlockAndOnePlayIsABlock()
    {
        var plays = Sequence((120, TimeSpan.FromHours(1).TotalSeconds), (120, 0));

        Assert.Equal(2, MoMSessionImport.Blocks(plays).Count);
        // Two one-play blocks: the most recent wins the tie — and one play IS importable.
        Assert.Equal((1, 1), MoMSessionImport.BestBlock(plays));
    }

    [Fact]
    public void ClickingMovesTheNearerEndAndNeverInvertsTheSelection()
    {
        Assert.Equal((2, 8), MoMSessionImport.MoveNearestEnd((4, 8), 2));
        Assert.Equal((4, 10), MoMSessionImport.MoveNearestEnd((4, 8), 10));
        // Equidistant goes to the start end.
        Assert.Equal((6, 8), MoMSessionImport.MoveNearestEnd((4, 8), 6));
        // A click just past the far end moves that end, never inverts the range.
        Assert.Equal((4, 9), MoMSessionImport.MoveNearestEnd((4, 8), 9));
        Assert.Equal((5, 5), MoMSessionImport.MoveNearestEnd((5, 5), 5));
    }

    [Fact]
    public void TheHardBlockCountsSongsBeforeTheLastPlayOnly()
    {
        // Three 50-minute songs: 100 minutes before the last < 105 — legal, the closer
        // overhangs (§2.9). Four of them: 150 minutes before the last — blocked.
        var legal = Sequence((3000, 30), (3000, 30), (3000, 0));
        Assert.False(MoMSessionImport.ExceedsWindow(legal, MaxTime));

        var blocked = Sequence((3000, 30), (3000, 30), (3000, 30), (3000, 0));
        Assert.True(MoMSessionImport.ExceedsWindow(blocked, MaxTime));

        // Unselectable plays (stage breaks, the other chart type) never count toward it.
        var withDead = Sequence((3000, 30), (3000, 30), (3000, 30), (3000, 0));
        var mixed = withDead.Select((p, i) => p with { Selectable = i != 1 }).ToArray();
        Assert.False(MoMSessionImport.ExceedsWindow(mixed, MaxTime));
    }

    [Fact]
    public void TheSoftWarningNamesTheLongestBreakInsideTheSelection()
    {
        var plays = Sequence((120, 30), (120, 320), (120, 45), (120, 0));

        var warning = MoMSessionImport.LongestBreakInside(plays, (0, 3));

        Assert.NotNull(warning);
        Assert.Equal(TimeSpan.FromSeconds(320), warning!.Value.Gap);
        Assert.Same(plays[2].Chart, warning.Value.Before.Chart);
    }

    [Fact]
    public void AJournalStampInsideThePreviousSongFloorsTheGapAtZero()
    {
        var plays = new List<MoMImportPlay>
        {
            Play(Base, 300),
            // Starts 100 seconds into the previous 300-second song — durations disagree.
            Play(Base.AddSeconds(100), 120)
        };

        Assert.Equal(TimeSpan.Zero, MoMSessionImport.GapBefore(plays, 1));
        Assert.Single(MoMSessionImport.Blocks(plays));
    }

    private static IReadOnlyList<MoMImportPlay> Sequence(
        params (double Seconds, double RestAfter)[] plays)
    {
        var list = new List<MoMImportPlay>();
        var at = Base;
        foreach (var (seconds, restAfter) in plays)
        {
            list.Add(Play(at, seconds));
            at = at.AddSeconds(seconds + restAfter);
        }

        return list;
    }

    private static MoMImportPlay Play(DateTimeOffset at, double seconds)
    {
        var song = new Song($"song-{at.Ticks}-{seconds}", SongType.Arcade,
            new Uri("https://example.invalid/a.png"), TimeSpan.FromSeconds(seconds), "Artist",
            null);
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Double,
            DifficultyLevel.From(22), MixEnum.Phoenix, null, null, new HashSet<Skill>());
        var entry = new ScoreJournalEntry(at, "test", Guid.NewGuid(), chart.Id, 950000,
            PhoenixPlate.RoughGame, false);
        return new MoMImportPlay(entry, chart, true);
    }
}

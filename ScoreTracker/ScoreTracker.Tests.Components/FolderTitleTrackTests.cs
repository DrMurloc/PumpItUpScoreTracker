using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The pure per-folder Phoenix 2 title read (docs/design/pumbility-title-track.md). Scenarios
///     build a spread of scored charts so the pool has a real floor (its 50th chart) rather than a
///     flat value.
/// </summary>
public sealed class FolderTitleTrackTests
{
    private static Chart Chart(ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song("song", SongType.Arcade, new Uri("https://piu.test/a.png"),
                TimeSpan.FromMinutes(2), "artist", Bpm.From(140, 140)),
            type, level, MixEnum.Phoenix2, null, 1000, new HashSet<Skill>());

    private static RecordedPhoenixScore Score(Guid chartId, int value, PhoenixPlate plate) =>
        new(chartId, value, plate, false, DateTimeOffset.UtcNow);

    // A folder of `count` singles at `level`, each scored `value` — plus the folder's full size.
    private static (Dictionary<Guid, Chart> Charts, Dictionary<Guid, RecordedPhoenixScore> Scores) Folder(
        ChartType type, int level, int scoredCount, int folderSize, int value, PhoenixPlate plate,
        Dictionary<Guid, Chart>? into = null, Dictionary<Guid, RecordedPhoenixScore>? scoresInto = null)
    {
        var charts = into ?? new Dictionary<Guid, Chart>();
        var scores = scoresInto ?? new Dictionary<Guid, RecordedPhoenixScore>();
        for (var i = 0; i < folderSize; i++)
        {
            var chart = Chart(type, level);
            charts[chart.Id] = chart;
            if (i < scoredCount)
                // A shrinking bonus per chart gives the pool a spread (a real floor below the top).
                scores[chart.Id] = Score(chart.Id, Math.Min(1_000_000, value + (scoredCount - i) * 300), plate);
        }

        return (charts, scores);
    }

    [Fact]
    public void ReturnsNullOffPhoenix2()
    {
        var (charts, scores) = Folder(ChartType.Single, 20, 50, 55, 925_000, PhoenixPlate.FairGame);
        Assert.Null(FolderTitleTrack.Compute(MixEnum.Phoenix, ChartType.Single, 20, charts, scores));
    }

    [Fact]
    public void ReturnsNullForCoOpFolders()
    {
        var (charts, scores) = Folder(ChartType.Single, 20, 50, 55, 925_000, PhoenixPlate.FairGame);
        Assert.Null(FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.CoOp, 3, charts, scores));
    }

    [Fact]
    public void ReadsAValidTitleAndBoundedProgressForALivePool()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 50, 55, 925_000, PhoenixPlate.FairGame);

        var result = FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.Single, 22, charts, scores);

        Assert.NotNull(result);
        Assert.True(result!.Show);
        Assert.False(string.IsNullOrEmpty(result.TargetTitle));
        Assert.InRange(result.Progress, 0, 1);
    }

    [Fact]
    public void HidesTheBarWhenEvenAPerfectFolderCantReachTheTitle()
    {
        // A strong S24 pool, then look at the 12s — even a perfect 12 can't crack the top 50.
        var (charts, scores) = Folder(ChartType.Single, 24, 50, 55, 950_000, PhoenixPlate.UltimateGame);
        Folder(ChartType.Single, 12, 20, 40, 1_000_000, PhoenixPlate.PerfectGame, charts, scores);

        var result = FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.Single, 12, charts, scores);

        Assert.NotNull(result);
        Assert.False(result!.Show);
        Assert.False(string.IsNullOrEmpty(result.ServesTitle));
    }

    [Fact]
    public void FlagsServesAboveWhenTheFolderOutranksYourTarget()
    {
        // A pool parked in the low 20s; the 26s folder serves far above the current target.
        var (charts, scores) = Folder(ChartType.Single, 20, 50, 55, 920_000, PhoenixPlate.FairGame);
        Folder(ChartType.Single, 26, 3, 30, 950_000, PhoenixPlate.SuperbGame, charts, scores);

        var result = FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.Single, 26, charts, scores);

        Assert.NotNull(result);
        Assert.True(result!.ServesAbove);
        Assert.False(string.IsNullOrEmpty(result.ServesTitle));
    }

    [Fact]
    public void GradeUpNeverAsksForBelowAPass()
    {
        // A solid pool, then a folder the player's barely touched (< 5 charts → no median → grade
        // up). The grade named must be a pass (A) or better — never "get this folder to F".
        var (charts, scores) = Folder(ChartType.Single, 22, 50, 55, 925_000, PhoenixPlate.FairGame);
        Folder(ChartType.Single, 20, 3, 40, 920_000, PhoenixPlate.FairGame, charts, scores);

        var result = FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.Single, 20, charts, scores);

        Assert.NotNull(result);
        if (result!.Show && result.Mode == FolderTrackMode.GradeUp)
        {
            Assert.True(result.NeededGrade >= PhoenixLetterGrade.A, $"named {result.NeededGrade}, below an A pass");
            Assert.InRange(result.ChartsLeft, 1, 40);
        }
    }

    [Fact]
    public void AThinFolderFarAboveYourLevelStaysVisibleAndServesAbove()
    {
        // A mid doubles pool, then a sky-high D28 folder with only a couple of charts in it. The old
        // rule hid it — too few charts to finish the title single-handed — so it read as "behind your
        // level" (the D28/D29 bug). A folder above you must stay visible and flag that it serves above,
        // never collapse to the beneath-you whisper.
        var (charts, scores) = Folder(ChartType.Double, 18, 50, 55, 850_000, PhoenixPlate.FairGame);
        Folder(ChartType.Double, 28, 0, 2, 0, PhoenixPlate.FairGame, charts, scores);

        var result = FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.Double, 28, charts, scores);

        Assert.NotNull(result);
        Assert.True(result!.Show, "a folder above your level must not hide as 'behind your top 50'");
        Assert.True(result.ServesAbove);
        Assert.False(string.IsNullOrEmpty(result.ServesTitle));
    }

    [Fact]
    public void IdenticalContributionsDoNotDivideByZero()
    {
        // Every chart the same value → median equals the floor; the on-pace count must not run.
        var charts = new Dictionary<Guid, Chart>();
        var scores = new Dictionary<Guid, RecordedPhoenixScore>();
        for (var i = 0; i < 55; i++)
        {
            var chart = Chart(ChartType.Single, 22);
            charts[chart.Id] = chart;
            if (i < 50) scores[chart.Id] = Score(chart.Id, 925_000, PhoenixPlate.FairGame);
        }

        var result = FolderTitleTrack.Compute(MixEnum.Phoenix2, ChartType.Single, 22, charts, scores);

        Assert.NotNull(result);
        // Whatever the mode, ChartsLeft is a finite, sane number (no Infinity → int overflow).
        Assert.InRange(result!.ChartsLeft, 0, 500);
    }
}

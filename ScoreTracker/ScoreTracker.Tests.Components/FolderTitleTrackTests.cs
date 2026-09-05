using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The pure per-folder gate behind the tier list's title-progress pointer — it says yes exactly
///     where the retired Phoenix 2 folder-title track drew its bar (docs/design/pumbility-title-track.md).
///     Scenarios build a spread of scored charts so the pool has a real floor (its 50th chart) rather
///     than a flat value.
/// </summary>
public sealed class FolderTitleTrackTests
{
    private static Chart Chart(ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song("song", SongType.Arcade, new Uri("https://piu.test/a.png"),
                TimeSpan.FromMinutes(2), "artist", Bpm.From(140, 140)),
            type, level, MixEnum.Phoenix2, null, 1000);

    private static RecordedPhoenixScore Score(Guid chartId, int value, PhoenixPlate plate) =>
        new(chartId, value, plate, false, DateTimeOffset.UtcNow);

    // A folder of `count` charts at `level`, each scored `value` — plus the folder's full size.
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
    public void OffPhoenix2ThereIsNothingToPointAt()
    {
        var (charts, scores) = Folder(ChartType.Single, 20, 50, 55, 925_000, PhoenixPlate.FairGame);
        Assert.False(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix, ChartType.Single, 20, charts, scores));
    }

    [Fact]
    public void CoOpFoldersHaveNoPool()
    {
        var (charts, scores) = Folder(ChartType.Single, 20, 50, 55, 925_000, PhoenixPlate.FairGame);
        Assert.False(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix2, ChartType.CoOp, 3, charts, scores));
    }

    [Fact]
    public void BelowLevel10ChartsScoreZeroSoThereIsNothingToPointAt()
    {
        // Phoenix 2 prices charts below level 10 at zero — a sub-10 folder can't touch the pool, so
        // there's no title progress to point at (even a perfect S9 gives 0 PUMBILITY).
        var (charts, scores) = Folder(ChartType.Single, 9, 20, 25, 1_000_000, PhoenixPlate.PerfectGame);
        Assert.False(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix2, ChartType.Single, 9, charts, scores));
    }

    [Fact]
    public void ALivePoolWithARungStillAboveItShowsThePointer()
    {
        var (charts, scores) = Folder(ChartType.Single, 22, 50, 55, 925_000, PhoenixPlate.FairGame);
        Assert.True(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix2, ChartType.Single, 22, charts, scores));
    }

    [Fact]
    public void AFolderBeneathYourTop50HidesThePointer()
    {
        // A strong S24 pool, then look at the 12s — even a perfect 12 can't crack the top 50. The
        // track hid its bar here (only its whisper stayed), so the pointer stays away too.
        var (charts, scores) = Folder(ChartType.Single, 24, 50, 55, 950_000, PhoenixPlate.UltimateGame);
        Folder(ChartType.Single, 12, 20, 40, 1_000_000, PhoenixPlate.PerfectGame, charts, scores);

        Assert.False(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix2, ChartType.Single, 12, charts, scores));
    }

    [Fact]
    public void AThinFolderFarAboveYourLevelStaysVisible()
    {
        // A low doubles pool (D10s, ~11.5k → chasing INTERMEDIATE LV.8), then a sky-high D28 folder
        // that holds a single chart. The track once hid this — too few charts to finish the title
        // single-handed — so it read as "behind your level" (the D28/D29 bug). A folder above you
        // is not beneath your top 50, and the pointer must stay.
        var (charts, scores) = Folder(ChartType.Double, 10, 50, 55, 850_000, PhoenixPlate.FairGame);
        Folder(ChartType.Double, 28, 0, 1, 0, PhoenixPlate.FairGame, charts, scores);

        Assert.True(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix2, ChartType.Double, 28, charts, scores),
            "a folder above your level must not hide as 'behind your top 50'");
    }

    [Fact]
    public void PastTheTopRungThereIsNothingLeftToChase()
    {
        // Fifty perfect S28s price far past the singles ladder's top rung (19,000): no title is
        // left above the pool, so the track had no bar to draw and the pointer has nothing to say.
        var (charts, scores) = Folder(ChartType.Single, 28, 50, 55, 1_000_000, PhoenixPlate.PerfectGame);
        Assert.False(FolderTitleTrack.HasTitleProgress(MixEnum.Phoenix2, ChartType.Single, 28, charts, scores));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetMixDiffHandlerTests
{
    private readonly Mock<IChartRepository> _charts = new();

    private GetMixDiffHandler Handler => new(_charts.Object);

    private void Catalog(MixEnum mix, params Chart[] charts)
    {
        _charts.Setup(c => c.GetCharts(mix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private static Chart Chart(Guid id, string song, ChartType type, int level, MixEnum mix)
    {
        return new ChartBuilder().WithId(id).WithSongName(song).WithType(type).WithLevel(level).WithMix(mix);
    }

    private static Chart Chart(Guid id, string song, ChartType type, int level, MixEnum mix, int notes)
    {
        return new ChartBuilder().WithId(id).WithSongName(song).WithType(type).WithLevel(level).WithMix(mix)
            .WithNoteCount(notes);
    }

    private Task<MixDiffRecord> Diff(MixEnum from = MixEnum.Phoenix, MixEnum to = MixEnum.Phoenix2)
    {
        return Handler.Handle(new GetMixDiffQuery(from, to), CancellationToken.None);
    }

    [Fact]
    public async Task AChartThatKeptItsIdAndChangedLevelIsReRated()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2, Chart(id, "Iolite Sky", ChartType.Double, 21, MixEnum.Phoenix2));

        var diff = await Diff();

        var move = Assert.Single(diff.Rerated);
        Assert.Equal(20, (int)move.Before.Level);
        Assert.Equal(21, (int)move.After.Level);
        Assert.Equal(1, move.Delta);
        Assert.Equal(1, diff.RatedHarder);
        Assert.Equal(0, diff.RatedEasier);
    }

    [Fact]
    public async Task AChartAtTheSameLevelInBothMixesIsNotReported()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "Conflict", ChartType.Single, 18, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2, Chart(id, "Conflict", ChartType.Single, 18, MixEnum.Phoenix2));

        var diff = await Diff();

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public async Task ADroppedLevelCountsAsEasier()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "About The Universe", ChartType.Single, 21, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2, Chart(id, "About The Universe", ChartType.Single, 20, MixEnum.Phoenix2));

        var diff = await Diff();

        Assert.Equal(-1, Assert.Single(diff.Rerated).Delta);
        Assert.Equal(1, diff.RatedEasier);
        Assert.Equal(0, diff.RatedHarder);
    }

    [Fact]
    public async Task ASongPresentOnlyInTheLaterMixArrivesWithAllOfItsCharts()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "Conflict", ChartType.Single, 18, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2,
            Chart(id, "Conflict", ChartType.Single, 18, MixEnum.Phoenix2),
            Chart(Guid.NewGuid(), "Freedom Dive", ChartType.Single, 22, MixEnum.Phoenix2),
            Chart(Guid.NewGuid(), "Freedom Dive", ChartType.Double, 24, MixEnum.Phoenix2));

        var diff = await Diff();

        var arrived = Assert.Single(diff.ArrivedSongs);
        Assert.Equal("Freedom Dive", arrived.Song.Name.ToString());
        Assert.Equal(2, arrived.Charts.Count);
        Assert.Equal(2, diff.ChartsArrived);
        // The song carries its charts; they are not also listed as loose arrivals.
        Assert.Empty(diff.AddedCharts);
    }

    [Fact]
    public async Task ASongPresentOnlyInTheEarlierMixDepartsWithAllOfItsCharts()
    {
        Catalog(MixEnum.Phoenix,
            Chart(Guid.NewGuid(), "Nxde", ChartType.Single, 17, MixEnum.Phoenix),
            Chart(Guid.NewGuid(), "Nxde", ChartType.Double, 20, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2);

        var diff = await Diff();

        var departed = Assert.Single(diff.DepartedSongs);
        Assert.Equal("Nxde", departed.Song.Name.ToString());
        Assert.Equal(2, departed.Charts.Count);
        Assert.Equal(2, diff.ChartsDeparted);
        Assert.Empty(diff.RemovedCharts);
    }

    [Fact]
    public async Task AChartAddedToASongThatAlreadyExistedIsItsOwnArrival()
    {
        var kept = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(kept, "Bemera", ChartType.Single, 17, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2,
            Chart(kept, "Bemera", ChartType.Single, 17, MixEnum.Phoenix2),
            Chart(Guid.NewGuid(), "Bemera", ChartType.Double, 19, MixEnum.Phoenix2));

        var diff = await Diff();

        Assert.Empty(diff.ArrivedSongs);
        var added = Assert.Single(diff.AddedCharts);
        Assert.Equal(ChartType.Double, added.Type);
        Assert.Equal(1, diff.ChartsArrived);
    }

    [Fact]
    public async Task AChartRemovedFromASongThatSurvivedIsItsOwnDeparture()
    {
        var kept = Guid.NewGuid();
        Catalog(MixEnum.Phoenix,
            Chart(kept, "Iolite Sky", ChartType.Single, 16, MixEnum.Phoenix),
            Chart(Guid.NewGuid(), "Iolite Sky", ChartType.DoublePerformance, 3, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2, Chart(kept, "Iolite Sky", ChartType.Single, 16, MixEnum.Phoenix2));

        var diff = await Diff();

        Assert.Empty(diff.DepartedSongs);
        Assert.Equal(ChartType.DoublePerformance, Assert.Single(diff.RemovedCharts).Type);
        Assert.Equal(1, diff.ChartsDeparted);
    }

    [Fact]
    public async Task ComparingAMixToItselfReadsAsNoChangeAndTouchesNoCatalog()
    {
        var diff = await Diff(MixEnum.Phoenix, MixEnum.Phoenix);

        Assert.True(diff.IsEmpty);
        _charts.Verify(
            c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SongNamesMatchWithoutRegardToCase()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "BOOOM!!", ChartType.Single, 10, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2,
            Chart(id, "BOOOM!!", ChartType.Single, 12, MixEnum.Phoenix2),
            Chart(Guid.NewGuid(), "booom!!", ChartType.Double, 13, MixEnum.Phoenix2));

        var diff = await Diff();

        // The second chart is a new chart on a song that was already here, not a new song.
        Assert.Empty(diff.ArrivedSongs);
        Assert.Single(diff.AddedCharts);
    }

    [Fact]
    public async Task ReRatesAreOrderedBySongThenTypeThenLevel()
    {
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        Catalog(MixEnum.Phoenix,
            Chart(ids[0], "Zeus", ChartType.Single, 20, MixEnum.Phoenix),
            Chart(ids[1], "Allegro", ChartType.Double, 23, MixEnum.Phoenix),
            Chart(ids[2], "Allegro", ChartType.Single, 19, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2,
            Chart(ids[0], "Zeus", ChartType.Single, 21, MixEnum.Phoenix2),
            Chart(ids[1], "Allegro", ChartType.Double, 24, MixEnum.Phoenix2),
            Chart(ids[2], "Allegro", ChartType.Single, 20, MixEnum.Phoenix2));

        var diff = await Diff();

        Assert.Equal(new[] { "Allegro", "Allegro", "Zeus" },
            diff.Rerated.Select(m => m.After.Song.Name.ToString()).ToArray());
        Assert.Equal(new[] { ChartType.Single, ChartType.Double, ChartType.Single },
            diff.Rerated.Select(m => m.After.Type).ToArray());
    }

    [Fact]
    public async Task AChangedNoteCountIsReportedAsARestep()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix, 1000));
        Catalog(MixEnum.Phoenix2, Chart(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix2, 1012));

        var diff = await Diff();

        var restep = Assert.Single(diff.Restepped);
        Assert.Equal(12, restep.NoteDelta);
        Assert.True(diff.NoteCountsTracked);
        Assert.Equal(0, diff.NoteCountsUnknown);
        // Its level held, so it is not a rerate — the two lists answer different questions.
        Assert.Empty(diff.Rerated);
    }

    [Fact]
    public async Task AChartCanBeBothReRatedAndRestepped()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix, 1000));
        Catalog(MixEnum.Phoenix2, Chart(id, "Iolite Sky", ChartType.Double, 21, MixEnum.Phoenix2, 980));

        var diff = await Diff();

        Assert.Single(diff.Rerated);
        Assert.Equal(-20, Assert.Single(diff.Restepped).NoteDelta);
    }

    [Fact]
    public async Task AChartMissingANoteCountOnEitherSideIsCountedAsUnknownNotUnchanged()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        Catalog(MixEnum.Phoenix,
            Chart(known, "Conflict", ChartType.Single, 12, MixEnum.Phoenix, 500),
            Chart(unknown, "Butterfly", ChartType.Single, 4, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2,
            Chart(known, "Conflict", ChartType.Single, 12, MixEnum.Phoenix2, 500),
            Chart(unknown, "Butterfly", ChartType.Single, 4, MixEnum.Phoenix2));

        var diff = await Diff();

        Assert.Empty(diff.Restepped);
        Assert.True(diff.NoteCountsTracked);
        Assert.Equal(1, diff.NoteCountsUnknown);
    }

    [Fact]
    public async Task APairThatRecordsNoNoteCountsAtAllIsNotTracked()
    {
        // Pre-Phoenix catalogs have no note counts, so the question is unanswerable rather
        // than answered "nothing changed".
        var id = Guid.NewGuid();
        Catalog(MixEnum.XX, Chart(id, "Conflict", ChartType.Single, 12, MixEnum.XX));
        Catalog(MixEnum.Phoenix, Chart(id, "Conflict", ChartType.Single, 12, MixEnum.Phoenix));

        var diff = await Diff(MixEnum.XX, MixEnum.Phoenix);

        Assert.False(diff.NoteCountsTracked);
        Assert.Empty(diff.Restepped);
    }

    [Fact]
    public async Task RestepsAreOrderedByHowMuchTheChartChanged()
    {
        var small = Guid.NewGuid();
        var big = Guid.NewGuid();
        Catalog(MixEnum.Phoenix,
            Chart(small, "Small", ChartType.Single, 12, MixEnum.Phoenix, 500),
            Chart(big, "Big", ChartType.Single, 12, MixEnum.Phoenix, 500));
        Catalog(MixEnum.Phoenix2,
            Chart(small, "Small", ChartType.Single, 12, MixEnum.Phoenix2, 505),
            Chart(big, "Big", ChartType.Single, 12, MixEnum.Phoenix2, 300));

        var diff = await Diff();

        Assert.Equal(new[] { "Big", "Small" },
            diff.Restepped.Select(m => m.After.Song.Name.ToString()).ToArray());
    }

    [Fact]
    public async Task TheDiffRunsInEitherDirection()
    {
        var id = Guid.NewGuid();
        Catalog(MixEnum.Phoenix, Chart(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix));
        Catalog(MixEnum.Phoenix2, Chart(id, "Iolite Sky", ChartType.Double, 21, MixEnum.Phoenix2));

        var backwards = await Diff(MixEnum.Phoenix2, MixEnum.Phoenix);

        Assert.Equal(-1, Assert.Single(backwards.Rerated).Delta);
        Assert.Equal(1, backwards.RatedEasier);
    }
}

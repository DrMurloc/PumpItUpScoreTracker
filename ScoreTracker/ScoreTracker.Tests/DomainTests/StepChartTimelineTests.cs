using System.Linq;
using ScoreTracker.Catalog.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class StepChartTimelineTests
{
    private static StepChartData Build(string ssc, string stepsType = "pump-single", int meter = 10)
    {
        var document = StepFileParser.Parse(ssc);
        var chart = StepFileParser.SelectChart(document, stepsType, meter);
        Assert.NotNull(chart);
        var data = StepChartTimeline.Build(document, chart!);
        Assert.NotNull(data);
        return data!;
    }

    private static string Wrap(string notes, string songTags = "#OFFSET:0;\n#BPMS:0=120;",
        string chartTags = "")
    {
        return $"""
            {songTags}
            #NOTEDATA:;
            #STEPSTYPE:pump-single;
            #METER:10;
            {chartTags}
            #NOTES:
            {notes}
            ;
            """;
    }

    [Fact]
    public void QuartersAtOneTwentyLandHalfASecondApart()
    {
        var data = Build(Wrap("10000\n01000\n00100\n00010"));

        Assert.Equal(4, data.Rows.Count);
        Assert.Equal(new[] { 0m, 1m, 2m, 3m }, data.Rows.Select(r => r.Beat));
        Assert.Equal(new[] { 0m, 0.5m, 1m, 1.5m }, data.Rows.Select(r => r.Time));
    }

    [Fact]
    public void TheOffsetShiftsBeatZero()
    {
        var data = Build(Wrap("10000", "#OFFSET:0.500;\n#BPMS:0=120;"));

        Assert.Equal(-0.5m, data.Rows.Single().Time);
    }

    [Fact]
    public void MeasureResolutionSetsTheSubdivision()
    {
        var data = Build(Wrap("10000\n00100\n01000\n00100\n00010\n00100\n01000\n00100"));

        Assert.Equal(0.5m, data.Rows[1].Beat);
        Assert.Equal(0.25m, data.Rows[1].Time);
    }

    [Fact]
    public void AJumpIsOneRowWearingBothPanels()
    {
        var data = Build(Wrap("10001"));

        var row = data.Rows.Single();
        Assert.Equal((1 << 0) | (1 << 4), row.PanelMask);
    }

    [Fact]
    public void AHoldSpansHeadToTailAndTheTailAloneIsNoRow()
    {
        var data = Build(Wrap("20000\n00000\n30000\n00000"));

        var hold = data.Holds.Single();
        Assert.Equal(0, hold.Panel);
        Assert.Equal(0m, hold.StartBeat);
        Assert.Equal(2m, hold.EndBeat);
        Assert.Equal(0m, hold.StartTime);
        Assert.Equal(1m, hold.EndTime);
        Assert.Single(data.Rows); // the head; the tail judges nothing on its own
    }

    [Fact]
    public void TickCountsPutCheckpointsOnTheGridEdgesIncluded()
    {
        var data = Build(Wrap("20000\n00000\n30000\n00000", "#OFFSET:0;\n#BPMS:0=120;\n#TICKCOUNTS:0=2;"));

        // Hold spans beats 0..2 on a half-beat grid: 0, .5, 1, 1.5, 2.
        Assert.Equal(new[] { 0m, 0.5m, 1m, 1.5m, 2m }, data.Ticks.Select(t => t.Beat));
        Assert.Equal(new[] { 0m, 0.25m, 0.5m, 0.75m, 1m }, data.Ticks.Select(t => t.Time));
    }

    [Fact]
    public void AStopPausesAfterItsBeat()
    {
        var data = Build(Wrap("10000\n10000\n10000\n10000",
            "#OFFSET:0;\n#BPMS:0=120;\n#STOPS:1=1;"));

        // The row ON the stop beat plays before the pause; everything later slides by it.
        Assert.Equal(new[] { 0m, 0.5m, 2m, 2.5m }, data.Rows.Select(r => r.Time));
    }

    [Fact]
    public void ADelayPausesBeforeItsBeat()
    {
        var data = Build(Wrap("10000\n10000\n10000\n10000",
            "#OFFSET:0;\n#BPMS:0=120;\n#DELAYS:1=1;"));

        Assert.Equal(new[] { 0m, 1.5m, 2m, 2.5m }, data.Rows.Select(r => r.Time));
    }

    [Fact]
    public void AWarpTakesNoTimeAndSwallowsItsRows()
    {
        var data = Build(Wrap("10000\n10000\n10000\n10000",
            "#OFFSET:0;\n#BPMS:0=120;\n#WARPS:1=1;"));

        // The beat-1 row sits inside the warp: faked by the game, absent here. Beat 2 lands
        // where beat 1 would have.
        Assert.Equal(new[] { 0m, 2m, 3m }, data.Rows.Select(r => r.Beat));
        Assert.Equal(new[] { 0m, 0.5m, 1m }, data.Rows.Select(r => r.Time));
    }

    [Fact]
    public void ABpmChangeRepricesOnlyTheBeatsAfterIt()
    {
        var data = Build(Wrap("10000\n10000\n10000\n10000",
            "#OFFSET:0;\n#BPMS:0=120,2=240;"));

        Assert.Equal(new[] { 0m, 0.5m, 1m, 1.25m }, data.Rows.Select(r => r.Time));
    }

    [Fact]
    public void ChartLevelTimingOverridesTheSongs()
    {
        var data = Build(Wrap("10000\n10000\n10000\n10000",
            "#OFFSET:0;\n#BPMS:0=120;", "#BPMS:0=240;"));

        Assert.Equal(new[] { 0m, 0.25m, 0.5m, 0.75m }, data.Rows.Select(r => r.Time));
    }

    [Fact]
    public void AnUnsupportedEncodingKeepsBeatsAndDropsTheTimeline()
    {
        var data = Build(Wrap("10000\n10000", "#OFFSET:0;\n#BPMS:0=120,1=-120;"));

        Assert.False(data.HasTimeline);
        Assert.Equal(2, data.Rows.Count);
        Assert.Empty(data.Ticks);
    }

    [Fact]
    public void HalfDoubleColumnsLandOnTheMiddleOfTheDoublesPad()
    {
        var ssc = """
            #OFFSET:0;
            #BPMS:0=120;
            #NOTEDATA:;
            #STEPSTYPE:pump-halfdouble;
            #METER:10;
            #NOTES:
            100001
            ;
            """;
        var document = StepFileParser.Parse(ssc);
        var chart = StepFileParser.SelectChart(document, "pump-halfdouble", 10)!;

        var data = StepChartTimeline.Build(document, chart)!;

        Assert.Equal(10, data.Panels);
        Assert.Equal((1 << 2) | (1 << 7), data.Rows.Single().PanelMask);
    }

    [Fact]
    public void ARollHeadOpensAHoldBody()
    {
        var data = Build(Wrap("40000\n00000\n30000\n00000"));

        var hold = data.Holds.Single();
        Assert.Equal(2m, hold.EndBeat);
    }

    [Fact]
    public void AHeadlessTailAndATaillessHeadBothStayHarmless()
    {
        var data = Build(Wrap("30000\n20000\n00000\n00000"));

        var hold = data.Holds.Single();
        Assert.Equal(hold.StartBeat, hold.EndBeat); // never closed: zero-length, not negative
    }
}

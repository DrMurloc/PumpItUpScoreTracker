using System.Linq;
using ScoreTracker.Catalog.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class StepFileParserTests
{
    private const string TwoChartFile = """
        #TITLE:Altale;
        #OFFSET:0.09;
        #BPMS:0.000=90.000;
        // a comment that mentions #BPMS:999; and must not parse
        #NOTEDATA:;
        #STEPSTYPE:pump-single;
        #DESCRIPTION:S12;
        #METER:12;
        #NOTES:
        10000
        01000
        ,
        00100
        ;
        #NOTEDATA:;
        #STEPSTYPE:pump-single;
        #METER:21;
        #BPMS:0.000=180.000;
        #NOTES:
        00010
        ;
        """;

    [Fact]
    public void SplitsSongTagsFromChartBlocks()
    {
        var document = StepFileParser.Parse(TwoChartFile);

        Assert.Equal("Altale", document.SongTags.Get("TITLE"));
        Assert.Equal(2, document.Charts.Count);
        Assert.Equal(12, document.Charts[0].Meter);
        Assert.Equal(21, document.Charts[1].Meter);
    }

    [Fact]
    public void SelectsAChartByStepsTypeAndMeter()
    {
        var document = StepFileParser.Parse(TwoChartFile);

        var chart = StepFileParser.SelectChart(document, "pump-single", 21);

        Assert.NotNull(chart);
        Assert.Equal("180.000", chart!.Tags.Get("BPMS")!.Split('=').Last());
    }

    [Fact]
    public void ReturnsNothingWhenNoChartMatches()
    {
        var document = StepFileParser.Parse(TwoChartFile);

        Assert.Null(StepFileParser.SelectChart(document, "pump-double", 21));
        Assert.Null(StepFileParser.SelectChart(document, "pump-single", 22));
    }

    [Fact]
    public void CommentsNeverLeakIntoValues()
    {
        var document = StepFileParser.Parse(TwoChartFile);

        Assert.Equal("0.000=90.000", document.SongTags.Get("BPMS"));
    }

    [Fact]
    public void ChartTagsShadowSongTagsLastWins()
    {
        var document = StepFileParser.Parse(TwoChartFile);
        var chart = StepFileParser.SelectChart(document, "pump-single", 21)!;

        Assert.Equal("0.000=180.000", chart.Tags.Get("BPMS"));
        Assert.Null(chart.Tags.Get("OFFSET"));
    }
}

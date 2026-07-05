using System;
using System.Linq;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class BulkChartJsonParserTests
{
    private readonly BulkChartJsonParser _parser = new();

    private const string ValidBlob = """
        {
          "songs": [
            {
              "name": "District 1",
              "koreanName": "디스트릭트 1",
              "artist": "Doin",
              "type": "Arcade",
              "minBpm": 195,
              "maxBpm": 195,
              "durationSeconds": 105,
              "imageUrl": "https://example.com/district1.png",
              "charts": [
                {
                  "type": "Single",
                  "level": 22,
                  "stepArtist": "EXC",
                  "youtubeHash": "dQw4w9WgXcQ"
                },
                {
                  "type": "CoOp",
                  "level": 2,
                  "stepArtist": "EXC",
                  "youtubeHash": "abc_DEF-123",
                  "channelName": "SPHAM"
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void ValidBlobProducesFullyPopulatedSpecs()
    {
        var result = _parser.Parse(ValidBlob);

        Assert.Empty(result.GlobalErrors);
        var entry = Assert.Single(result.Songs);
        Assert.Empty(entry.Errors);
        Assert.NotNull(entry.Song);

        var song = entry.Song!;
        Assert.Equal("District 1", song.Name.ToString());
        Assert.Equal("디스트릭트 1", song.KoreanName.ToString());
        Assert.Equal("Doin", song.Artist.ToString());
        Assert.Equal(SongType.Arcade, song.Type);
        Assert.Equal(195, song.Bpm.Min);
        Assert.Equal(195, song.Bpm.Max);
        Assert.Equal(TimeSpan.FromSeconds(105), song.Duration);
        Assert.Equal(new Uri("https://example.com/district1.png"), song.ImageUrl);
        Assert.Equal(2, song.Charts.Count);

        var single = song.Charts[0];
        Assert.Equal(ChartType.Single, single.Type);
        Assert.Equal(22, (int)single.Level);
        Assert.Equal("EXC", single.StepArtist.ToString());
        Assert.Equal(new Uri("https://www.youtube.com/embed/dQw4w9WgXcQ"), single.VideoUrl);

        var coOp = song.Charts[1];
        Assert.Equal(ChartType.CoOp, coOp.Type);
        Assert.Equal(2, (int)coOp.Level);
        Assert.Equal("SPHAM", coOp.ChannelName.ToString());
    }

    [Fact]
    public void OmittedChannelNameDefaultsToPumpItUpOfficial()
    {
        var result = _parser.Parse(ValidBlob);

        var song = result.Songs.Single().Song!;
        Assert.Equal(BulkChartJsonParser.DefaultChannelName, song.Charts[0].ChannelName.ToString());
    }

    [Fact]
    public void UnknownSongTypeIsAnErrorListingValidValues()
    {
        var blob = ValidBlob.Replace("\"type\": \"Arcade\"", "\"type\": \"Boss\"");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        var error = Assert.Single(entry.Errors);
        Assert.Contains("'Boss'", error);
        Assert.Contains("Arcade", error);
        Assert.Contains("ShortCut", error);
        Assert.Contains("FullSong", error);
        Assert.Contains("Remix", error);
    }

    [Fact]
    public void SongTypeParsesCaseInsensitively()
    {
        var blob = ValidBlob.Replace("\"type\": \"Arcade\"", "\"type\": \"fullsong\"");

        var result = _parser.Parse(blob);

        Assert.Equal(SongType.FullSong, result.Songs.Single().Song!.Type);
    }

    [Fact]
    public void OutOfRangeChartLevelIsAnError()
    {
        var blob = ValidBlob.Replace("\"level\": 22", "\"level\": 30");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        var error = Assert.Single(entry.Errors);
        Assert.Contains("Chart 1", error);
        Assert.Contains("30", error);
    }

    [Fact]
    public void UnknownChartTypeIsAnError()
    {
        var blob = ValidBlob.Replace("\"type\": \"Single\"", "\"type\": \"Triple\"");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        var error = Assert.Single(entry.Errors);
        Assert.Contains("'Triple'", error);
        Assert.Contains("Single, Double, CoOp", error);
    }

    [Fact]
    public void MissingKoreanNameIsAnError()
    {
        var blob = ValidBlob.Replace("\"koreanName\": \"디스트릭트 1\",", string.Empty);

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        var error = Assert.Single(entry.Errors);
        Assert.Contains("koreanName", error);
    }

    [Fact]
    public void DuplicateSongNameWithinBlobIsAnErrorOnTheSecondOccurrence()
    {
        var songObject = ValidBlob.Split("\"songs\": [")[1].TrimEnd().TrimEnd('}').TrimEnd().TrimEnd(']');
        var blob = $$"""{ "songs": [ {{songObject}}, {{songObject}} ] }""";

        var result = _parser.Parse(blob);

        Assert.Equal(2, result.Songs.Count);
        Assert.Empty(result.Songs[0].Errors);
        Assert.NotNull(result.Songs[0].Song);
        Assert.Null(result.Songs[1].Song);
        var error = Assert.Single(result.Songs[1].Errors);
        Assert.Contains("Duplicate song name", error);
        Assert.Contains("song 1", error);
    }

    [Fact]
    public void DuplicateDetectionIsCaseInsensitive()
    {
        var second = ValidBlob.Split("\"songs\": [")[1].TrimEnd().TrimEnd('}').TrimEnd().TrimEnd(']')
            .Replace("\"name\": \"District 1\"", "\"name\": \"DISTRICT 1\"");
        var first = ValidBlob.Split("\"songs\": [")[1].TrimEnd().TrimEnd('}').TrimEnd().TrimEnd(']');
        var blob = $$"""{ "songs": [ {{first}}, {{second}} ] }""";

        var result = _parser.Parse(blob);

        Assert.Null(result.Songs[1].Song);
        Assert.Contains("Duplicate song name", Assert.Single(result.Songs[1].Errors));
    }

    [Fact]
    public void MalformedJsonIsAGlobalError()
    {
        var result = _parser.Parse("{ not json");

        Assert.Empty(result.Songs);
        Assert.Contains("Invalid JSON", Assert.Single(result.GlobalErrors));
    }

    [Fact]
    public void EmptyInputIsAGlobalError()
    {
        var result = _parser.Parse("   ");

        Assert.Empty(result.Songs);
        Assert.Single(result.GlobalErrors);
    }

    [Fact]
    public void MissingSongsArrayIsAGlobalError()
    {
        var result = _parser.Parse("{ \"tracks\": [] }");

        Assert.Empty(result.Songs);
        Assert.Contains("songs", Assert.Single(result.GlobalErrors));
    }

    [Fact]
    public void EmptySongsArrayIsAGlobalError()
    {
        var result = _parser.Parse("{ \"songs\": [] }");

        Assert.Empty(result.Songs);
        Assert.Contains("empty", Assert.Single(result.GlobalErrors));
    }

    [Fact]
    public void InvertedBpmRangeIsAnError()
    {
        var blob = ValidBlob.Replace("\"minBpm\": 195", "\"minBpm\": 200");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        Assert.Contains("BPM", Assert.Single(entry.Errors));
    }

    [Fact]
    public void MissingBpmIsASingleCombinedError()
    {
        var blob = ValidBlob.Replace("\"minBpm\": 195,", string.Empty);

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Contains("minBpm", Assert.Single(entry.Errors));
    }

    [Fact]
    public void NonPositiveDurationIsAnError()
    {
        var blob = ValidBlob.Replace("\"durationSeconds\": 105", "\"durationSeconds\": 0");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Contains("durationSeconds", Assert.Single(entry.Errors));
    }

    [Fact]
    public void RelativeImageUrlIsAnError()
    {
        var blob = ValidBlob.Replace("https://example.com/district1.png", "/songs/district1.png");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Contains("imageUrl", Assert.Single(entry.Errors));
    }

    [Fact]
    public void YoutubeHashRejectsFullUrls()
    {
        var blob = ValidBlob.Replace("dQw4w9WgXcQ", "https://youtu.be/dQw4w9WgXcQ");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        Assert.Contains(entry.Errors, e => e.Contains("youtubeHash"));
    }

    [Fact]
    public void SongWithoutChartsIsAnError()
    {
        var start = ValidBlob.IndexOf("\"charts\"", StringComparison.Ordinal);
        var blob = string.Concat(ValidBlob.AsSpan(0, start), "\"charts\": []\n} ] }");

        var result = _parser.Parse(blob);

        var entry = Assert.Single(result.Songs);
        Assert.Null(entry.Song);
        Assert.Contains("at least one chart", Assert.Single(entry.Errors));
    }

    [Fact]
    public void OneInvalidSongDoesNotBlockParsingTheOthers()
    {
        var invalid = ValidBlob.Split("\"songs\": [")[1].TrimEnd().TrimEnd('}').TrimEnd().TrimEnd(']')
            .Replace("\"name\": \"District 1\"", "\"name\": \"Another Song\"")
            .Replace("\"type\": \"Arcade\"", "\"type\": \"Boss\"");
        var valid = ValidBlob.Split("\"songs\": [")[1].TrimEnd().TrimEnd('}').TrimEnd().TrimEnd(']');
        var blob = $$"""{ "songs": [ {{invalid}}, {{valid}} ] }""";

        var result = _parser.Parse(blob);

        Assert.Equal(2, result.Songs.Count);
        Assert.Null(result.Songs[0].Song);
        Assert.NotEmpty(result.Songs[0].Errors);
        Assert.NotNull(result.Songs[1].Song);
        Assert.Empty(result.Songs[1].Errors);
    }
}

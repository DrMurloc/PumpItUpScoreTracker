using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class UserQualifiersTests
{
    private static QualifiersConfiguration Config(IEnumerable<Chart> charts, string scoringType, int playCount = 2,
        IDictionary<Guid, int>? adjustments = null) =>
        new(charts, adjustments ?? new Dictionary<Guid, int>(), Name.From(scoringType), 0, playCount, null, false);

    private static UserQualifiers New(QualifiersConfiguration config) =>
        new(config, false, Name.From("player"), Guid.NewGuid(), new Dictionary<Guid, UserQualifiers.Submission>());

    [Fact]
    public void ApproveFlipsIsApprovedToTrue()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));

        qualifiers.Approve();

        Assert.True(qualifiers.IsApproved);
    }

    [Fact]
    public void AddPhoenixScoreRecordsSubmissionForChart()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));
        var chartId = Guid.NewGuid();
        var photo = new Uri("https://example.invalid/proof.png");

        var added = qualifiers.AddPhoenixScore(chartId, 950000, photo);

        Assert.True(added);
        Assert.True(qualifiers.Submissions.ContainsKey(chartId));
        Assert.Equal((PhoenixScore)950000, qualifiers.Submissions[chartId].Score);
        Assert.Equal(photo, qualifiers.Submissions[chartId].PhotoUrl);
    }

    [Fact]
    public void RatingForScoreScoringTypeReturnsRawScore()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));

        var rating = qualifiers.Rating(DifficultyLevel.From(20), 980000);

        Assert.Equal(980000.0, rating);
    }

    [Theory]
    [InlineData("Fungpapi")]
    [InlineData("Competitive Level")]
    public void RatingForFungpapiTypesReturnsLevelPlusScoreOffset(string scoringType)
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), scoringType));

        // 965,000 is the formula's zero point: level + (965000 - 965000) / 17500 = level
        var atZeroPoint = qualifiers.Rating(DifficultyLevel.From(20), 965000);
        var oneLevelHigher = qualifiers.Rating(DifficultyLevel.From(20), 965000 + 17500);

        Assert.Equal(20.0, atZeroPoint);
        Assert.Equal(21.0, oneLevelHigher);
    }

    [Fact]
    public void RatingForChartIdReturnsZeroWhenNoSubmissionExists()
    {
        var chart = new ChartBuilder().WithLevel(15).Build();
        var qualifiers = New(Config(new[] { chart }, "Score"));

        Assert.Equal(0.0, qualifiers.Rating(chart.Id));
    }

    [Fact]
    public void RatingForChartIdUsesChartDifficultyAndSubmissionScore()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var qualifiers = New(Config(new[] { chart }, "Fungpapi"));
        qualifiers.AddPhoenixScore(chart.Id, 965000, null);

        // Fungpapi at 965,000 → level (20)
        Assert.Equal(20.0, qualifiers.Rating(chart.Id));
    }

    [Fact]
    public void BestChartsReturnsTopPlayCountOrderedByRatingDescending()
    {
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var chartC = new ChartBuilder().WithLevel(20).Build();
        var config = Config(new[] { chartA, chartB, chartC }, "Score", playCount: 2);
        var qualifiers = New(config);

        qualifiers.AddPhoenixScore(chartA.Id, 900000, null);
        qualifiers.AddPhoenixScore(chartB.Id, 950000, null);
        qualifiers.AddPhoenixScore(chartC.Id, 980000, null);

        var best = qualifiers.BestCharts().ToArray();

        Assert.Equal(2, best.Length);
        Assert.Equal(chartC.Id, best[0].Chart.Id);
        Assert.Equal(chartB.Id, best[1].Chart.Id);
    }

    [Fact]
    public void CalculateScoreSumsBestChartsForNonFungpapiScoring()
    {
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var qualifiers = New(Config(new[] { chartA, chartB }, "Score", playCount: 2));
        qualifiers.AddPhoenixScore(chartA.Id, 900000, null);
        qualifiers.AddPhoenixScore(chartB.Id, 950000, null);

        Assert.Equal(900000.0 + 950000.0, qualifiers.CalculateScore());
    }

    [Fact]
    public void CalculateScoreAveragesByPlayCountForFungpapiScoring()
    {
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var qualifiers = New(Config(new[] { chartA, chartB }, "Fungpapi", playCount: 4));
        qualifiers.AddPhoenixScore(chartA.Id, 965000, null); // → 20
        qualifiers.AddPhoenixScore(chartB.Id, 965000 + 17500, null); // → 21

        // Fungpapi divides by configured PlayCount (4) even when fewer charts are submitted
        Assert.Equal((20.0 + 21.0) / 4.0, qualifiers.CalculateScore());
    }

    [Fact]
    public void CalculateScoreReturnsZeroForFungpapiWithNoSubmissions()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var qualifiers = New(Config(new[] { chart }, "Fungpapi", playCount: 4));

        Assert.Equal(0.0, qualifiers.CalculateScore());
    }

    [Fact]
    public void AddXXScoreAppliesNoteCountAdjustmentBeforeCalculatingPhoenixScore()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var adjustments = new Dictionary<Guid, int> { [chart.Id] = 10 };
        var qualifiers = New(Config(new[] { chart }, "Score", adjustments: adjustments));
        var baseline = New(Config(new[] { chart }, "Score"));
        var photo = new Uri("https://example.invalid/x.png");

        // Use a step screen with greats so the score is below the 1M ceiling and the
        // adjustment (which lifts perfects + maxCombo) produces a measurably higher score.
        baseline.AddXXScore(chart.Id, 90, 10, 0, 0, 0, 90, photo);
        qualifiers.AddXXScore(chart.Id, 90, 10, 0, 0, 0, 90, photo);

        Assert.True(qualifiers.Submissions[chart.Id].Score > baseline.Submissions[chart.Id].Score);
    }
}

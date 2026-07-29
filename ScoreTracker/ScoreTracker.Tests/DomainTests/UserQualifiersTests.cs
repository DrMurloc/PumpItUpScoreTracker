using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class UserQualifiersTests
{
    private static readonly DateTimeOffset SubmittedAt = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Uri Photo = new("https://example.invalid/proof.png");

    private static QualifiersConfiguration Config(IEnumerable<Chart> charts, string scoringType, int playCount = 2,
        IDictionary<Guid, int>? adjustments = null) =>
        new(charts, adjustments ?? new Dictionary<Guid, int>(), Name.From(scoringType), 0, playCount, null, false);

    private static UserQualifiers New(QualifiersConfiguration config) =>
        new(config, Name.From("player"), Guid.NewGuid(), new Dictionary<Guid, UserQualifiers.Submission>());

    [Fact]
    public void ManualScoreRecordsPhotoSourceAndTimestamp()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));
        var chartId = Guid.NewGuid();

        var added = qualifiers.AddManualScore(chartId, 950000, Photo, SubmittedAt);

        Assert.True(added);
        var submission = qualifiers.Submissions[chartId];
        Assert.Equal((PhoenixScore)950000, submission.Score);
        Assert.Equal(Photo, submission.PhotoUrl);
        Assert.Equal(SubmissionSource.Manual, submission.Source);
        Assert.Equal(SubmittedAt, submission.SubmittedAt);
    }

    [Fact]
    public void ManualScoreWithoutAPhotoIsRejected()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));

        Assert.Throws<QualifierPhotoRequiredException>(() =>
            qualifiers.AddManualScore(Guid.NewGuid(), 950000, null!, SubmittedAt));
        Assert.Empty(qualifiers.Submissions);
    }

    [Fact]
    public void ImportedScoreCarriesNoPhotoAndIsMarkedAsSuch()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));
        var chartId = Guid.NewGuid();

        qualifiers.AddImportedScore(chartId, 970000, SubmittedAt);

        var submission = qualifiers.Submissions[chartId];
        Assert.Null(submission.PhotoUrl);
        Assert.Equal(SubmissionSource.OfficialImport, submission.Source);
        Assert.Equal(SubmittedAt, submission.SubmittedAt);
    }

    [Fact]
    public void ResubmittingAChartReplacesThePreviousSubmission()
    {
        var qualifiers = New(Config(Array.Empty<Chart>(), "Score"));
        var chartId = Guid.NewGuid();

        qualifiers.AddImportedScore(chartId, 900000, SubmittedAt);
        qualifiers.AddManualScore(chartId, 980000, Photo, SubmittedAt.AddHours(1));

        Assert.Single(qualifiers.Submissions);
        Assert.Equal((PhoenixScore)980000, qualifiers.Submissions[chartId].Score);
        Assert.Equal(SubmissionSource.Manual, qualifiers.Submissions[chartId].Source);
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
        qualifiers.AddImportedScore(chart.Id, 965000, SubmittedAt);

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

        qualifiers.AddImportedScore(chartA.Id, 900000, SubmittedAt);
        qualifiers.AddImportedScore(chartB.Id, 950000, SubmittedAt);
        qualifiers.AddImportedScore(chartC.Id, 980000, SubmittedAt);

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
        qualifiers.AddImportedScore(chartA.Id, 900000, SubmittedAt);
        qualifiers.AddImportedScore(chartB.Id, 950000, SubmittedAt);

        Assert.Equal(900000.0 + 950000.0, qualifiers.CalculateScore());
    }

    [Fact]
    public void CalculateScoreAveragesByPlayCountForFungpapiScoring()
    {
        var chartA = new ChartBuilder().WithLevel(20).Build();
        var chartB = new ChartBuilder().WithLevel(20).Build();
        var qualifiers = New(Config(new[] { chartA, chartB }, "Fungpapi", playCount: 4));
        qualifiers.AddImportedScore(chartA.Id, 965000, SubmittedAt); // → 20
        qualifiers.AddImportedScore(chartB.Id, 965000 + 17500, SubmittedAt); // → 21

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

        // Use a step screen with greats so the score is below the 1M ceiling and the
        // adjustment (which lifts perfects + maxCombo) produces a measurably higher score.
        baseline.AddXXScore(chart.Id, 90, 10, 0, 0, 0, 90, Photo, SubmittedAt);
        qualifiers.AddXXScore(chart.Id, 90, 10, 0, 0, 0, 90, Photo, SubmittedAt);

        Assert.True(qualifiers.Submissions[chart.Id].Score > baseline.Submissions[chart.Id].Score);
    }

    [Fact]
    public void AddXXScoreIsAManualSubmissionAndStillNeedsAPhoto()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var qualifiers = New(Config(new[] { chart }, "Score"));

        qualifiers.AddXXScore(chart.Id, 90, 10, 0, 0, 0, 90, Photo, SubmittedAt);
        Assert.Equal(SubmissionSource.Manual, qualifiers.Submissions[chart.Id].Source);

        var noPhoto = New(Config(new[] { chart }, "Score"));
        Assert.Throws<QualifierPhotoRequiredException>(() =>
            noPhoto.AddXXScore(chart.Id, 90, 10, 0, 0, 0, 90, null!, SubmittedAt));
    }
}

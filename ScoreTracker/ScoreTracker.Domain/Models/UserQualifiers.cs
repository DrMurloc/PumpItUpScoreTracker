using ScoreTracker.SharedKernel.Models;
﻿using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed class UserQualifiers
{
    private readonly ScoringConfiguration _scoreConfig = new()
    {
        ContinuousLetterGradeScale = true
    };

    private static ScoringConfiguration BuildStorm()
    {
        var result = new ScoringConfiguration
        {
            ContinuousLetterGradeScale = true
        };
        result.LetterGradeModifiers[PhoenixLetterGrade.B] = .6;
        result.LetterGradeModifiers[PhoenixLetterGrade.C] = .4;
        result.LetterGradeModifiers[PhoenixLetterGrade.D] = .2;
        result.LetterGradeModifiers[PhoenixLetterGrade.F] = 0;
        return result;
    }

    private static readonly ScoringConfiguration _stormScoreConfig = BuildStorm();

    public UserQualifiers(QualifiersConfiguration config, Name userName, Guid? userId,
        IDictionary<Guid, Submission> submissions)
    {
        Configuration = config;
        UserName = userName;
        Submissions = submissions;
        UserId = userId;
    }

    public QualifiersConfiguration Configuration { get; }

    public Name UserName { get; set; }
    public Guid? UserId { get; set; }

    public IDictionary<Guid, Submission> Submissions { get; }

    public IEnumerable<(Chart Chart, PhoenixScore Score, double Rating)> BestCharts()
    {
        return Configuration.Charts.Where(c => Submissions.ContainsKey(c.Id)).OrderByDescending(c => Rating(c.Id))
            .Select(c => (c, Submissions[c.Id].Score, Rating(c.Id)))
            .Take(Configuration.PlayCount).ToArray();
    }

    public double Rating(DifficultyLevel level, PhoenixScore score)
    {
        if (Configuration.ScoringType == "Fungpapi" || Configuration.ScoringType=="Competitive Level") return level + (score - 965000.0) / 17500.0;
        if (Configuration.ScoringType == "Score") return score;
        if (Configuration.ScoringType == "Storm") return _stormScoreConfig.GetScore(level, score);
        return _scoreConfig.GetScore(level, score);
    }

    public double Rating(Guid chartId)
    {
        if (!Submissions.ContainsKey(chartId)) return 0;

        var difficulty = Configuration.Charts.First(c => c.Id == chartId).Level;
        return Rating(difficulty, Submissions[chartId].Score);
    }

    public double CalculateScore()
    {
        var bestCharts = BestCharts().ToArray();
        return
            Configuration.ScoringType == "Fungpapi"
                ? bestCharts.Any() ? bestCharts.Sum(c => c.Rating)/(double)Configuration.PlayCount : 0.0
                : BestCharts().Sum(c => c.Rating);
    }

    /// <summary>
    ///     A score the player entered. The photo is not optional: it is the only evidence an
    ///     organiser has for a hand-typed number.
    /// </summary>
    public bool AddManualScore(Guid chartId, PhoenixScore score, Uri photo, DateTimeOffset submittedAt)
    {
        if (photo == null) throw new QualifierPhotoRequiredException();

        return Record(chartId, score, photo, SubmissionSource.Manual, submittedAt);
    }

    /// <summary>
    ///     A score read off the official site. The site is the evidence, so there is no photo.
    /// </summary>
    public bool AddImportedScore(Guid chartId, PhoenixScore score, DateTimeOffset submittedAt)
    {
        return Record(chartId, score, null, SubmissionSource.OfficialImport, submittedAt);
    }

    public bool AddXXScore(Guid chartId, StepCount perfects, StepCount greats, StepCount goods, StepCount bads,
        StepCount misses, StepCount maxCombo,
        Uri photo, DateTimeOffset submittedAt)
    {
        var offset = Configuration.NoteCountAdjustments.TryGetValue(chartId, out var adjustment)
            ? adjustment
            : 0;
        perfects += offset;
        maxCombo += offset;
        var scoreScreen = new ScoreScreen(perfects, greats, goods, bads, misses, maxCombo);
        return AddManualScore(chartId, scoreScreen.CalculatePhoenixScore, photo, submittedAt);
    }

    private bool Record(Guid chartId, PhoenixScore score, Uri? photo, SubmissionSource source,
        DateTimeOffset submittedAt)
    {
        Submissions[chartId] = new Submission
        {
            ChartId = chartId,
            PhotoUrl = photo,
            Score = score,
            Source = source,
            SubmittedAt = submittedAt
        };
        return true;
    }

    public sealed class Submission
    {
        public Guid ChartId { get; set; }
        public PhoenixScore Score { get; set; }
        public Uri? PhotoUrl { get; set; }
        public SubmissionSource Source { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
    }
}

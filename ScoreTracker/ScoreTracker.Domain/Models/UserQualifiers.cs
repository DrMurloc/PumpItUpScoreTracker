using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

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

    public UserQualifiers(QualifiersConfiguration config, bool isApproved, Name userName, Guid? userId,
        IDictionary<Guid, Submission> submissions)
    {
        Configuration = config;
        IsApproved = isApproved;
        UserName = userName;
        Submissions = submissions;
    }

    public QualifiersConfiguration Configuration { get; }

    public bool IsApproved { get; private set; }
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

    public void Approve()
    {
        IsApproved = true;
    }

    public bool AddXXScore(Guid chartId, StepCount perfects, StepCount greats, StepCount goods, StepCount bads,
        StepCount misses, StepCount maxCombo,
        Uri uri)
    {
        var offset = Configuration.NoteCountAdjustments.TryGetValue(chartId, out var adjustment)
            ? adjustment
            : 0;
        perfects += offset;
        maxCombo += offset;
        var scoreScreen = new ScoreScreen(perfects, greats, goods, bads, misses, maxCombo);
        return AddPhoenixScore(chartId, scoreScreen.CalculatePhoenixScore, uri);
    }

    public bool AddPhoenixScore(Guid chartId, PhoenixScore score, Uri? uri)
    {
        Submissions[chartId] = new Submission
        {
            ChartId = chartId,
            PhotoUrl = uri,
            Score = score
        };
        IsApproved = false;
        return true;
    }

    public sealed class Submission
    {
        public Guid ChartId { get; set; }
        public PhoenixScore Score { get; set; }
        public Uri? PhotoUrl { get; set; }
    }
}

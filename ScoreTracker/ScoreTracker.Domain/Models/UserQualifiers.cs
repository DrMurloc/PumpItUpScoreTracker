using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class UserQualifiers
    {
        public QualifiersConfiguration Configuration { get; }

        public bool IsApproved { get; private set; }
        public Name UserName { get; set; }

        public IDictionary<Guid, Submission> Submissions { get; }

        public (Chart? First, Chart? Second) BestCharts()
        {
            Chart? first = null;
            Chart? second = null;
            var best = 0;
            var secondBest = 0;
            foreach (var chart in Configuration.Charts)
            {
                var rating = Rating(chart.Id);
                if (rating > best)
                {
                    secondBest = best;
                    second = first;
                    best = rating;
                    first = chart;
                }
                else if (rating > secondBest)
                {
                    secondBest = rating;
                    second = chart;
                }
            }

            return (first, second);
        }

        public int Rating(Guid chartId)
        {
            if (!Submissions.ContainsKey(chartId))
            {
                return 0;
            }

            var grade = Submissions[chartId].Score.LetterGrade;
            var difficulty = Configuration.Charts.First(c => c.Id == chartId).Level;

            return (int)(difficulty.BaseRating * grade.GetModifier());
        }

        public int CalculateScore()
        {
            var best = 0;
            var secondBest = 0;
            foreach (var chart in Configuration.Charts)
            {
                var rating = Rating(chart.Id);
                if (rating > best)
                {
                    secondBest = best;
                    best = rating;
                }
                else if (rating > secondBest)
                {
                    secondBest = rating;
                }
            }

            return best + secondBest;
        }

        public UserQualifiers(QualifiersConfiguration config) : this(config, false, "Not Set",
            new Dictionary<Guid, Submission>())
        {
        }

        public void Approve()
        {
            IsApproved = true;
        }

        public UserQualifiers(QualifiersConfiguration config, bool isApproved, Name userName,
            IDictionary<Guid, Submission> submissions)
        {
            Configuration = config;
            IsApproved = isApproved;
            UserName = userName;
            Submissions = submissions;
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

        public bool AddPhoenixScore(Guid chartId, PhoenixScore score, Uri uri)
        {
            if (Submissions.ContainsKey(chartId) && Submissions[chartId].Score >= score) return false;

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
            public Uri PhotoUrl { get; set; }
        }
    }
}

using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class CoOpRating
    {
        public IDictionary<int, DifficultyLevel> Ratings { get; }
        public int VoteCount { get; }
        public Guid ChartId { get; }

        public CoOpRating(Guid chartId, int voteCount, IDictionary<int, DifficultyLevel> ratings)
        {
            Ratings = ratings;
            VoteCount = voteCount;
            ChartId = chartId;
        }

        public DifficultyLevel Average => (int)Math.Round(Ratings.Average(r => r.Value));
        public DifficultyLevel Minimum => Ratings.Min(r => (int)r.Value);
        public DifficultyLevel Maximum => Ratings.Max(r => (int)r.Value);
    }
}
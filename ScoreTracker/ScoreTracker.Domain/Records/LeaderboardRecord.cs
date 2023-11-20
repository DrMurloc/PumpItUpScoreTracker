using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record LeaderboardRecord(int Place, Guid UserId, Name UserName, int TotalScore,
        TimeSpan TotalRestTime, double AverageDifficulty, int ChartsPlayed, SubmissionVerificationType VerificationType,
        bool NeedsApproval, Uri? VideoUrl)
    {
        public TournamentSession Session { get; set; }
        public decimal PassRate => Session.Entries.Count(e => !e.IsBroken) / (decimal)Session.Entries.Count;
        public PhoenixScore AverageScore => (int)Session.Entries.Average(e => e.Score);
        public int HighestLevel => Session.Entries.Max(e => (int)e.Chart.Level);
        public int LowestLevel => Session.Entries.Min(e => (int)e.Chart.Level);
        public PhoenixPlate AveragePlate => (PhoenixPlate)(int)Math.Floor(Session.Entries.Average(e => (int)e.Plate));
    }
}
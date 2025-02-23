using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class TournamentSession
    {
        public bool NeedsApproval { get; private set; } = true;
        public SubmissionVerificationType? ApprovedVerificationType { get; private set; }

        public void Approve()
        {
            NeedsApproval = false;
            ApprovedVerificationType = VerificationType;
        }

        public SubmissionVerificationType VerificationType { get; private set; }
        public ICollection<Uri> PhotoUrls { get; } = new List<Uri>();
        public Uri? VideoUrl { get; set; }

        public void AddPhoto(Uri photo)
        {
            PhotoUrls.Add(photo);
            NeedsApproval = true;
        }

        public void RemovePhoto(Uri photo)
        {
            PhotoUrls.Remove(photo);
            NeedsApproval = true;
        }

        public void SetVerificationType(SubmissionVerificationType type)
        {
            if (type == SubmissionVerificationType.InPerson || type == SubmissionVerificationType.Unverified ||
                type == ApprovedVerificationType)
            {
                NeedsApproval = false;
            }
            else
            {
                NeedsApproval = true;
            }

            VerificationType = type;
        }

        public Guid UsersId { get; }
        public Guid TournamentId => _configuration.Id;
        private readonly TournamentConfiguration _configuration;
        public IList<Entry> Entries { get; }

        public int CurrentScore { get; }

        public TournamentSession(Guid userId, TournamentConfiguration configuration)
        {
            _configuration = configuration;
            Entries = new List<Entry>();
            CurrentScore = 0;
            UsersId = userId;
        }

        public TournamentSession(Guid userId, TournamentConfiguration configuration, IEnumerable<Entry> entries)
        {
            _configuration = configuration;
            Entries = entries.ToList();
            CurrentScore = Entries.Sum(e => e.SessionScore);
            UsersId = userId;
        }

        public TimeSpan CurrentRestTime => _configuration.MaxTime - TotalPlayTime;

        public TimeSpan AverageTimeBetweenCharts =>
            Entries.Count <= 1 ? _configuration.MaxTime : CurrentRestTime / Entries.Count;

        public TimeSpan AverageTimeWithAddedChart(Chart chart)
        {
            var charts = Entries.Select(e => e.Chart).Append(chart).ToArray();
            var totalPlayTime = TimeSpan.FromTicks(charts.Sum(c => c.Song.Duration.Ticks));
            var restTime = _configuration.MaxTime - totalPlayTime;

            return charts.Length <= 1 ? _configuration.MaxTime : restTime / charts.Length;
        }

        public int TotalScore => Entries.Sum(c => c.SessionScore);

        public TimeSpan TotalPlayTime =>
            TimeSpan.FromTicks(Entries.Select(e => e.Chart).Sum(c => c.Song.Duration.Ticks));

        public bool CanAdd(Chart chart)
        {
            if (_configuration.Scoring.GetScorelessScore(chart) == 0) return false;
            if (TotalPlayTime + chart.Song.Duration > _configuration.MaxTime) return false;

            return _configuration.AllowRepeats || !Entries.Any(c =>
                c.Chart.Level == chart.Level && c.Chart.Type == chart.Type && c.Chart.Song.Name == chart.Song.Name);
        }

        public void Swap(Entry oldEntry, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            var index = Entries.IndexOf(oldEntry);
            if (index == -1) return;

            Entries[index] = oldEntry with
            {
                Score = score, Plate = plate, IsBroken = isBroken,
                SessionScore = (int)_configuration.Scoring.GetScore(oldEntry.Chart, score, plate, isBroken)
            };
            NeedsApproval = true;
        }

        public void Remove(Entry entry)
        {
            Entries.Remove(entry);
            NeedsApproval = true;
        }

        public void AddWithoutApproval(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            if (!CanAdd(chart))
            {
                throw new ArgumentException($"{chart.Song.Name} {chart.DifficultyString} is invalid for this session");
            }

            var basePoints = _configuration.Scoring.GetScore(chart, score, plate, isBroken, false);
            var withBonus = _configuration.Scoring.GetScore(chart, score, plate, isBroken);
            Entries.Add(
                new Entry(chart, score, plate, isBroken,
                    (int)withBonus, (int)(withBonus - basePoints)));
        }

        public void Add(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            AddWithoutApproval(chart, score, plate, isBroken);
            NeedsApproval = true;
        }

        public sealed record Entry(Chart Chart, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken,
            int SessionScore, int BonusPoints)
        {
        }
    }
}
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class TournamentConfiguration
    {
        public Guid Id { get; }
        public Name Name { get; set; }

        public TimeSpan MaxTime { get; set; } = TimeSpan.FromMinutes(105);
        public bool AllowRepeats { get; set; } = false;
        public bool IsStarted => StartDate == null || StartDate <= DateTimeOffset.Now;
        public bool IsEnded => EndDate != null && DateTimeOffset.Now > EndDate;
        public bool IsActive => IsStarted && !IsEnded;
        public bool IsMom { get; }
        public bool IsHighlighted { get; }

        public TournamentConfiguration(ScoringConfiguration scoringConfiguration) : this(Guid.NewGuid(), "Unnamed",
            scoringConfiguration, false, false)
        {
            Scoring = scoringConfiguration;
        }

        public TournamentConfiguration(Guid id, Name name, ScoringConfiguration scoringConfiguration,
            bool isHighlighted, bool isMom)
        {
            Id = id;
            Name = name;
            Scoring = scoringConfiguration;
            IsHighlighted = isHighlighted;
            IsMom = isMom;
        }

        public ScoringConfiguration Scoring { get; }

        public DateTimeOffset? EndDate { get; set; }
        public DateTimeOffset? StartDate { get; set; }
    }
}
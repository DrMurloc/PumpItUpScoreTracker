using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class TournamentConfiguration
    {
        public Guid Id { get; }
        public Name Name { get; set; }

        public TimeSpan MaxTime { get; set; } = TimeSpan.FromMinutes(105);
        public bool AllowRepeats { get; set; } = false;

        public TournamentConfiguration(ScoringConfiguration scoringConfiguration) : this(Guid.NewGuid(), "Unnamed",
            scoringConfiguration)
        {
            Scoring = scoringConfiguration;
        }

        public TournamentConfiguration(Guid id, Name name, ScoringConfiguration scoringConfiguration)
        {
            Id = id;
            Name = name;
            Scoring = scoringConfiguration;
        }

        public ScoringConfiguration Scoring { get; }

        public DateTimeOffset? EndDate { get; set; }
        public DateTimeOffset? StartDate { get; set; }
    }
}
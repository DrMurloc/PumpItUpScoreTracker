using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Randomizer.Infrastructure.Entities
{
    internal sealed class RandomizerDrawEntity
    {
        [Key] public Guid Id { get; set; }

        // Context: exactly one of UserId/TournamentId is set. Personal keeps one rolling
        // draw (filtered unique index); tournaments hold many named draws — matches.
        public Guid? UserId { get; set; }
        public Guid? TournamentId { get; set; }

        // The match name (field-test round 6): required for tournament draws, null for
        // personal ones. A match is a named draw — nothing more.
        public string? Name { get; set; }

        // The spectator link token — minted once per context, survives redraws.
        public Guid Slug { get; set; }

        [Required] public string Mix { get; set; } = "Phoenix";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}

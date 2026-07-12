using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Randomizer.Infrastructure.Entities
{
    internal sealed class RandomizerDrawEntity
    {
        [Key] public Guid Id { get; set; }

        // Context: exactly one of UserId/TournamentId is set (filtered unique indexes in
        // the model contribution enforce one active draw per context).
        public Guid? UserId { get; set; }
        public Guid? TournamentId { get; set; }

        // The spectator link token — minted once per context, survives redraws.
        public Guid Slug { get; set; }

        [Required] public string Mix { get; set; } = "Phoenix";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}

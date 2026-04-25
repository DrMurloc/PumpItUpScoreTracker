using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views
{
    [ExcludeFromCodeCoverage]
    public sealed record MatchPlayer(Name Name, int Seed, ulong? DiscordId, string Notes = "",
        bool PotentialConflict = false)
    {
    }
}

using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record CoOpTeam(Name TeamName, CoOpPlayer Player1, CoOpPlayer Player2, int? Seed)
    {
    }
}

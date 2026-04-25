using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record CoOpTeam(Name TeamName, CoOpPlayer Player1, CoOpPlayer Player2, int? Seed)
    {
    }
}

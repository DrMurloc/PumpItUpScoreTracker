using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record WeeklyTournamentEntry(Guid UserId, Guid ChartId, PhoenixScore Score, PhoenixPlate Plate,
        bool IsBroken, Uri? PhotoUrl, double CompetitiveLevel)
    {
    }
}

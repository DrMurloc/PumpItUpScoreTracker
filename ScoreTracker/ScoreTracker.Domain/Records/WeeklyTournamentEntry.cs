using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record WeeklyTournamentEntry(Guid UserId, Guid ChartId, PhoenixScore Score, PhoenixPlate Plate,
        bool IsBroken, Uri? PhotoUrl, double CompetitiveLevel)
    {
    }
}

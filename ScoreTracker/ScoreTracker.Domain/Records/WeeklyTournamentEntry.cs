using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    // Plate is null on a broken entry — the game awards no plate for a failed stage
    // (docs/design/score-truth-model.md D8). Broken entries still count for the board.
    public sealed record WeeklyTournamentEntry(Guid UserId, Guid ChartId, PhoenixScore Score, PhoenixPlate? Plate,
        bool IsBroken, Uri? PhotoUrl, double CompetitiveLevel)
    {
    }
}

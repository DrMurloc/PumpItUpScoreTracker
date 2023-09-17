using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record TournamentRecord(Guid Id, Name Name, int CurrentParticipants, DateTimeOffset? StartDate,
        DateTimeOffset? EndDate)
    {
    }
}
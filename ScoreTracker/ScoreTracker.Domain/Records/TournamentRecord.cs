using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record TournamentRecord(Guid Id, Name Name, int CurrentParticipants, TournamentType Type,
        string Location,
        bool IsHighlighted,
        Uri? LinkOverride,
        DateTimeOffset? StartDate,
        DateTimeOffset? EndDate,
        bool IsMoM)
    {
    }
}

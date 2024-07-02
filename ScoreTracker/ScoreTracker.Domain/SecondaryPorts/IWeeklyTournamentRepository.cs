using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IWeeklyTournamentRepository
    {
        Task<IEnumerable<Guid>> GetAlreadyPlayedCharts(CancellationToken cancellationToken);
        Task ClearAlreadyPlayedCharts(IEnumerable<Guid> chartIds, CancellationToken cancellationToken);
        Task WriteAlreadyPlayedCharts(IEnumerable<Guid> chartIds, CancellationToken cancellationToken);
        Task WriteHistories(IEnumerable<UserTourneyHistory> histories, CancellationToken cancellationToken);
        Task ClearTheBoard(CancellationToken cancellationToken);
        Task RegisterWeeklyChart(WeeklyTournamentChart chart, CancellationToken cancellationToken);
        Task<IEnumerable<WeeklyTournamentChart>> GetWeeklyCharts(CancellationToken cancellationToken);
        Task<IEnumerable<WeeklyTournamentEntry>> GetEntries(Guid? chartId, CancellationToken cancellationToken);
        Task SaveEntry(WeeklyTournamentEntry entry, CancellationToken cancellationToken);
        Task<IEnumerable<DateTimeOffset>> GetPastDates(CancellationToken cancellationToken);

        Task<IEnumerable<WeeklyTournamentEntry>> GetPastEntries(DateTimeOffset date,
            CancellationToken cancellationToken);
    }
}

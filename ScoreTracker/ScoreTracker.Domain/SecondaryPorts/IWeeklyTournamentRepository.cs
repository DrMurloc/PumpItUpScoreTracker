using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IWeeklyTournamentRepository
    {
        Task<IEnumerable<Guid>> GetAlreadyPlayedCharts(MixEnum mix, CancellationToken cancellationToken);
        Task ClearAlreadyPlayedCharts(MixEnum mix, IEnumerable<Guid> chartIds, CancellationToken cancellationToken);
        Task WriteAlreadyPlayedCharts(MixEnum mix, IEnumerable<Guid> chartIds, CancellationToken cancellationToken);

        Task WriteHistories(MixEnum mix, IEnumerable<UserTourneyHistory> histories,
            CancellationToken cancellationToken);

        Task ClearTheBoard(MixEnum mix, CancellationToken cancellationToken);
        Task RegisterWeeklyChart(MixEnum mix, WeeklyTournamentChart chart, CancellationToken cancellationToken);
        Task<IEnumerable<WeeklyTournamentChart>> GetWeeklyCharts(MixEnum mix, CancellationToken cancellationToken);

        Task<IEnumerable<WeeklyTournamentEntry>> GetEntries(MixEnum mix, Guid? chartId,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Live entries with their trust source (weekly-charts-overhaul.md M5). The shared
        ///     <see cref="WeeklyTournamentEntry" /> deliberately does not carry the source — the
        ///     api/weeklyCharts wire shape is pinned — so source-aware reads use this.
        /// </summary>
        Task<IEnumerable<(WeeklyTournamentEntry Entry, ChallengeEntrySource Source)>> GetEntriesWithSources(
            MixEnum mix, Guid? chartId, CancellationToken cancellationToken);

        Task SaveEntry(MixEnum mix, WeeklyTournamentEntry entry, ChallengeEntrySource source,
            CancellationToken cancellationToken);
        Task<IEnumerable<DateTimeOffset>> GetPastDates(MixEnum mix, CancellationToken cancellationToken);

        Task<IEnumerable<WeeklyTournamentEntry>> GetPastEntries(MixEnum mix, DateTimeOffset date,
            CancellationToken cancellationToken);

        /// <summary>All entries across a set of finished weeks in one read (the monthly window).</summary>
        Task<IEnumerable<WeeklyTournamentEntry>> GetPastEntries(MixEnum mix,
            IReadOnlyCollection<DateTimeOffset> dates, CancellationToken cancellationToken);
    }
}

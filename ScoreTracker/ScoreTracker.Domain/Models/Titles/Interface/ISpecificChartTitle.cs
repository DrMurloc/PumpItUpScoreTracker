using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Interface
{
    public interface ISpecificChartTitle
    {
        /// <summary>
        ///     The catalog song name this title's chart is looked up by. <see cref="AppliesToChart" />
        ///     matches it exactly (case-insensitively), so a title naming the song the way the official
        ///     requirement text abbreviates it — "Phalanx" for <c>Phalanx "RS2018 edit"</c> — matches
        ///     nothing and silently never completes.
        /// </summary>
        Name SongName { get; }

        /// <summary>
        ///     The letter grade the chart asks for, or null when any unbroken result earns the title —
        ///     a boss breaker's clear. What the titles page prints beside the chart instead of the
        ///     description sentence, so it is read from the title rather than parsed out of its text.
        /// </summary>
        PhoenixLetterGrade? MinimumGrade { get; }

        bool AppliesToChart(Chart chart);
    }
}

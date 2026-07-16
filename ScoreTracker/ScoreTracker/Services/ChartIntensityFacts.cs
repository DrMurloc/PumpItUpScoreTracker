using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services;

/// <summary>
///     A chart's three intensity dimensions, ready to render
///     (docs/design/chart-similarity.md §3.2). The same decomposition the similarity
///     formula scores on: how fast it feels, how much of it is grind, how much is spikes.
///     Fractions are of the song's length, so they are comparable between two charts of
///     different durations — which is the only reason a chip like "Sustain 22% → 24%"
///     means anything.
/// </summary>
public sealed record ChartIntensityFacts(decimal? Nps, double? SustainFraction, double? BurstFraction)
{
    /// <summary>
    ///     Null when piucenter banked no step analysis. Sustain is a subset of time under
    ///     tension — the spikes are the remainder, which cannot go below zero however the
    ///     crawl rounds.
    /// </summary>
    public static ChartIntensityFacts? For(Chart chart, ChartStepAnalysisRecord? analysis)
    {
        if (analysis == null) return null;
        var seconds = chart.Song.Duration.TotalSeconds;

        double? Fraction(decimal? value)
        {
            return value != null && seconds > 0 ? (double)value.Value / seconds : null;
        }

        var burst = analysis.TimeUnderTensionSeconds != null && analysis.SustainTimeSeconds != null
            ? Math.Max(0, analysis.TimeUnderTensionSeconds.Value - analysis.SustainTimeSeconds.Value)
            : (decimal?)null;

        return new ChartIntensityFacts(analysis.Nps, Fraction(analysis.SustainTimeSeconds), Fraction(burst));
    }
}

namespace ScoreTracker.Translations.Wiring;

/// <summary>
///     Host-supplied settings for the translation pipeline. Everything that decides what a night
///     costs lives here rather than in code: the models, the ceiling, the count, and the prices
///     the ceiling is computed from.
/// </summary>
public sealed class TranslationsConfiguration
{
    public const string SectionName = "Translations";

    /// <summary>
    ///     Stage one reads an arbitrary language and judges its register; stage two renders known
    ///     English through a glossary. Separate settings because the two jobs are not equally
    ///     hard, even though both launch on the same model.
    /// </summary>
    public string PivotModelId { get; set; } = "claude-sonnet-5";

    public string FanOutModelId { get; set; } = "claude-sonnet-5";

    /// <summary>
    ///     The rolling 30-day dollar ceiling that parks the submit job. A fuse, not a budget:
    ///     against a realistic 100–300 comments a month it should never fire, and what it
    ///     protects against is a bug that re-queues the catalogue.
    /// </summary>
    public decimal MonthlyCeilingUsd { get; set; } = 30m;

    /// <summary>Smoothing under the fuse — one runaway night cannot spend a third of the month.</summary>
    public int NightlyCount { get; set; } = 50;

    /// <summary>
    ///     Effective batched prices per million tokens — the Batch API bills at half the
    ///     synchronous rate, and these defaults are Sonnet 5's standard list halved. They exist
    ///     so spend is computed from recorded usage rather than guessed.
    /// </summary>
    public decimal InputPerMillionUsd { get; set; } = 1.5m;

    public decimal OutputPerMillionUsd { get; set; } = 7.5m;

    /// <summary>
    ///     What one text is assumed to cost before its batches complete — the measured
    ///     per-comment figure from the workbench, used for the in-flight share of the ceiling
    ///     check and for the re-translation quote.
    /// </summary>
    public decimal EstimatedCostPerTextUsd { get; set; } = 0.016m;
}

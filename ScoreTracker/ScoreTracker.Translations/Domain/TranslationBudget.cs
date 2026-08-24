using ScoreTracker.Domain.Records;

namespace ScoreTracker.Translations.Domain;

/// <summary>
///     The two spend controls, as pure arithmetic. The ceiling is checked at submit time against
///     rolling actual usage plus an estimate for in-flight work — spending is not something to
///     discover afterwards — and the nightly count smooths underneath it so one runaway night
///     cannot take a third of the month.
/// </summary>
internal static class TranslationBudget
{
    /// <summary>
    ///     How many texts tonight's submit may take. Zero when the ceiling would be crossed —
    ///     the fuse blowing — and never more than the nightly count.
    /// </summary>
    public static int Allowance(decimal ceilingUsd, decimal rollingSpendUsd, decimal inFlightEstimateUsd,
        decimal estimatedCostPerTextUsd, int nightlyCount)
    {
        if (nightlyCount <= 0 || estimatedCostPerTextUsd <= 0) return 0;

        var headroom = ceilingUsd - rollingSpendUsd - inFlightEstimateUsd;
        if (headroom <= 0) return 0;

        return Math.Min(nightlyCount, (int)(headroom / estimatedCostPerTextUsd));
    }

    /// <summary>
    ///     What one call cost. Cache reads bill at a tenth of the input rate and writes at a
    ///     quarter over it; at this volume neither fires, but a ledger that silently ignored them
    ///     would mislead the moment volume changed.
    /// </summary>
    public static decimal Cost(LanguageModelUsage usage, decimal inputPerMillionUsd, decimal outputPerMillionUsd)
    {
        return (usage.InputTokens * inputPerMillionUsd
                + usage.CacheReadInputTokens * inputPerMillionUsd * 0.1m
                + usage.CacheCreationInputTokens * inputPerMillionUsd * 1.25m
                + usage.OutputTokens * outputPerMillionUsd) / 1_000_000m;
    }
}

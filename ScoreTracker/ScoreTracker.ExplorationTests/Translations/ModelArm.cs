using ScoreTracker.Domain.Records;

namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     One model in the sweep, with what it costs and how it has to be configured.
///     <para>
///         The arms are deliberately not configured identically, because the models do not accept
///         identical requests. Claude Opus 5 and Sonnet 5 think by default and have to be told not
///         to — thinking tokens bill at the output rate, which for a two-sentence translation is
///         most of the bill for none of the benefit. Haiku 4.5 rejects the effort parameter
///         outright and does not think unless asked, so it takes neither field. Pretending the
///         arms are uniform would just mean two of them erroring.
///     </para>
/// </summary>
internal sealed record ModelArm(
    string Name,
    string ModelId,
    decimal InputPerMillion,
    decimal OutputPerMillion,
    bool SupportsEffort,
    bool ThinksByDefault)
{
    /// <summary>
    ///     Standard list prices. Sonnet 5 carries a lower introductory rate through 2026-08-31;
    ///     the standard rate is used here so a report does not read as cheaper than the thing it
    ///     is measuring will cost a month from now.
    /// </summary>
    public static readonly ModelArm Opus =
        new("Opus 5", "claude-opus-5", 5.00m, 25.00m, SupportsEffort: true, ThinksByDefault: true);

    public static readonly ModelArm Sonnet =
        new("Sonnet 5", "claude-sonnet-5", 3.00m, 15.00m, SupportsEffort: true, ThinksByDefault: true);

    public static readonly ModelArm Haiku =
        new("Haiku 4.5", "claude-haiku-4-5", 1.00m, 5.00m, SupportsEffort: false, ThinksByDefault: false);

    public static readonly IReadOnlyList<ModelArm> All = new[] { Opus, Sonnet, Haiku };

    public static ModelArm For(string modelId)
    {
        return All.FirstOrDefault(a => a.ModelId == modelId)
               ?? throw new InvalidOperationException($"No pricing on file for '{modelId}'.");
    }

    /// <summary>
    ///     What one call cost. Cache reads bill at a tenth of the input rate and cache writes at
    ///     a quarter over it; at this volume neither fires, but a cost report that silently
    ///     ignored them would mislead the moment volume changed.
    /// </summary>
    public decimal Cost(LanguageModelUsage usage)
    {
        return (usage.InputTokens * InputPerMillion
                + usage.CacheReadInputTokens * InputPerMillion * 0.1m
                + usage.CacheCreationInputTokens * InputPerMillion * 1.25m
                + usage.OutputTokens * OutputPerMillion) / 1_000_000m;
    }
}

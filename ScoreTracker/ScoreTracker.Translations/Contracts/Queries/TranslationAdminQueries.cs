using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.Translations.Contracts.Queries;

/// <summary>The admin page's one read: where the pipeline stands and what it has spent.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetTranslationPipelineStatusQuery : IQuery<TranslationPipelineStatusRecord>;

/// <summary>
///     What a full re-translation would cost, quoted before <c>RetranslateAllCommand</c> runs —
///     anything automatic here is what spends fifty dollars with nobody pressing anything.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRetranslationCostEstimateQuery : IQuery<RetranslationEstimateRecord>;

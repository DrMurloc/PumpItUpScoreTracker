namespace ScoreTracker.Translations.Contracts;

/// <summary>
///     <paramref name="ClientConfigured" /> is the first thing the page shows: false means no
///     <c>ClaudeApi:ApiKey</c> is set and the pipeline is parked — the deliberate default.
///     <paramref name="RollingSpendUsd" /> is 30-day actual usage from completed batches;
///     <paramref name="InFlightEstimateUsd" /> covers submitted-but-unfinished work, because the
///     ceiling has to see money that is committed but not yet billed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TranslationPipelineStatusRecord(
    bool ClientConfigured,
    int PendingCount,
    int InFlightCount,
    int PivotedCount,
    int TranslatedCount,
    int FailedCount,
    DateTimeOffset? OldestPendingAt,
    decimal RollingSpendUsd,
    decimal InFlightEstimateUsd,
    decimal CeilingUsd,
    int NightlyCount,
    DateTimeOffset? LastSubmittedAt,
    DateTimeOffset? LastCollectedAt,
    IReadOnlyList<TranslationFailureRecord> RecentFailures);

[ExcludeFromCodeCoverage]
public sealed record TranslationFailureRecord(string SourceKey, string Reason, DateTimeOffset At);

[ExcludeFromCodeCoverage]
public sealed record RetranslationEstimateRecord(int TranslatedCount, decimal EstimatedUsd);

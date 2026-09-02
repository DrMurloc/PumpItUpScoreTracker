namespace ScoreTracker.Catalog.Contracts.Messages;

/// <summary>
///     Bus trigger: re-run step-chart enrichment from the archived .ssc corpus — newest banked
///     vintage, no upload (docs/design/step-chart-failure-map.md D7). Published by the admin
///     button; a no-op with a log line when no archive is configured or banked.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReprocessStepFilesCommand;

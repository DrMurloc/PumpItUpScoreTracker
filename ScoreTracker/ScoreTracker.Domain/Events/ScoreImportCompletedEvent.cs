using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events;

/// <summary>
///     Fat contract event: a batch of scores observed during an acquisition run
///     (official-site import today; other sources keep the vocabulary honest).
///     Primitives-only — doubles as a partner webhook body (ADR-001 D3). Supersedes the
///     thin <see cref="RecentScoreImportedEvent" />, dual-published during P3.
///     Mix is additive (Phoenix 2 rollout) — SchemaVersion stays 1; a missing mix on an
///     old payload means Phoenix.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ScoreImportCompletedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    int SchemaVersion,
    string Source,
    Guid UserId,
    MixEnum Mix,
    IReadOnlyList<ScoreImportCompletedEvent.ImportedScore> Scores)
{
    public const int CurrentSchemaVersion = 1;
    public const string OfficialImportSource = "officialImport";

    public static ScoreImportCompletedEvent Create(DateTimeOffset occurredAt, string source, Guid userId,
        MixEnum mix, IReadOnlyList<ImportedScore> scores)
    {
        return new ScoreImportCompletedEvent(Guid.NewGuid(), occurredAt, CurrentSchemaVersion, source, userId,
            mix, scores);
    }

    [ExcludeFromCodeCoverage]
    public sealed record ImportedScore(Guid ChartId, int Score, string Plate, bool IsBroken);
}

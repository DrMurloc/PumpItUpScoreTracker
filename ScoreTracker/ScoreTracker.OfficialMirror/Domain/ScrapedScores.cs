using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Commands;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     One import's read of the official site: the bests off My Best Scores, and every play
///     the recently-played window showed. The two have different jobs — the bests decide the
///     record, the plays are journal history (docs/design/score-truth-model.md D3, D5) — so
///     they travel separately rather than being merged into one list.
/// </summary>
internal sealed record ScrapedScores(
    IReadOnlyList<OfficialRecordedScore> Bests,
    IReadOnlyList<RecordObservedPlaysCommand.ObservedPlay> Plays);

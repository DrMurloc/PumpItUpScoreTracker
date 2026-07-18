using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record ScoreJournalEntry(
    DateTimeOffset OccurredAt,
    string Source,
    Guid UserId,
    Guid ChartId,
    PhoenixScore? Score,
    PhoenixPlate? Plate,
    bool IsBroken,
    MixEnum Mix = MixEnum.Phoenix,
    Guid? SessionId = null,
    JudgementCounts? Judgements = null)
{
    public const string ManualSource = "manual";
    public const string OfficialImportSource = "officialImport";
    public const string CsvSource = "csv";

    /// <summary>
    ///     The 2026-06 journal seed from PhoenixRecord — history, not activity; volume
    ///     reads exclude it. Only the seed migration ever writes this value.
    /// </summary>
    public const string BackfillSource = "backfill";
}

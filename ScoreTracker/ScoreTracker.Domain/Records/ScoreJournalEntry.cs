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
    JudgementCounts? Judgements = null,
    // Whether this play became the record when it was written. False for the plays the
    // official site's recently-played list reports that never beat a best.
    bool IsBest = true,
    // The legacy axes. Score above is a PhoenixScore and caps at 1,000,000; era scores run far
    // past that (45,282,000 is the largest in production, and 76% of scored legacy records
    // exceed the Phoenix ceiling), so a legacy number cannot travel in it. The grade has no
    // Phoenix counterpart at all -- on XX and older the letter IS the plate.
    //
    // The pair is mutually exclusive with Score/Plate and the Mix says which side is live.
    // Both map to the same two columns underneath; the type split exists so neither model can
    // be read through the other's parser.
    XXScore? LegacyScore = null,
    XXLetterGrade? LegacyGrade = null)
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

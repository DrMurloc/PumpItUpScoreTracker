using ScoreTracker.SharedKernel.Enums;
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
    bool IsBroken)
{
    public const string ManualSource = "manual";
    public const string OfficialImportSource = "officialImport";
}

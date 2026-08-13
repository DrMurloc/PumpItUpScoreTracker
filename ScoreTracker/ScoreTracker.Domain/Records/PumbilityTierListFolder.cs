using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records;

/// <summary>
///     One chart's standing on the PUMBILITY tier list: how many of the cohort's top-50 pools
///     hold it, and the tier that count banded into (docs/design/pumbility-tier-list.md).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityTierListRecord(Guid ChartId, int Appearances, TierListCategory Category, int Order);

/// <summary>A cohort's whole folder: what it holds, and how many players it speaks for.</summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityTierListFolder(IReadOnlyList<PumbilityTierListRecord> Entries, int CohortSize);

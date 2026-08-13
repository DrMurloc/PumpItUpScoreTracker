using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>One folder the PUMBILITY lens can speak for.</summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityFolderRecord(ChartType ChartType, int Level);

using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>A title with the share of titled players who hold it (0–1).</summary>
[ExcludeFromCodeCoverage]
internal sealed record SnowflakeTitle(Name Title, double HolderShare);

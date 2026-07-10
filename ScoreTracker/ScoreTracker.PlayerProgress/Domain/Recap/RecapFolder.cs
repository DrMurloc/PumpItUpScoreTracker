using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

[ExcludeFromCodeCoverage]
internal sealed record RecapFolder(ChartType Type, DifficultyLevel Level, int Size, int Passed);

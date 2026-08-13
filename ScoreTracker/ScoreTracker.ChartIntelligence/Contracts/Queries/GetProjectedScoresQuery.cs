using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     What a player would be expected to score on each chart in a folder — the same number the
///     personalized Score list is built from, exposed on its own so a surface can print it
///     beside a chart without ranking anything.
///     <para>
///         A chart no peer near the player's level has played is simply absent. Absent means "no
///         opinion", never zero, and a caller that fills the gap with a number is inventing one.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetProjectedScoresQuery(ChartType ChartType, DifficultyLevel Level, MixEnum Mix,
    Guid? UserId = null) : IQuery<IReadOnlyDictionary<Guid, PhoenixScore>>;

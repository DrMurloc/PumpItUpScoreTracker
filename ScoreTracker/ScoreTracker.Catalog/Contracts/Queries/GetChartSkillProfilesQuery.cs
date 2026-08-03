using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     PIU Center step analysis, assembled from the metric bag.
///     <c>GetChartBadgeCoverageQuery</c> returns only the <c>badge_fraction:*</c> family — enough for
///     the similarity formula that consumes it, but not for a reader who also wants NPS, the top-3
///     picks, practice ranks or the difficulty prediction.
///     <para>
///         Null <see cref="ChartIds" /> reads every chart that has analysis; the bulk endpoint uses
///         that and a single-chart read passes one id.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartSkillProfilesQuery(IReadOnlyList<Guid>? ChartIds = null)
    : IQuery<IReadOnlyList<ChartSkillProfile>>;

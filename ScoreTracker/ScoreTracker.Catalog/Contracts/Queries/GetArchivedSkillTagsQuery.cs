using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The retired hand tags for a set of charts, as display names
///     (docs/design/nuke-old-skill-categories.md §7). This exists for exactly one reader — the
///     Chabala tier list, where his own vocabulary belongs — and charts analyzed after the
///     crawler took over simply have none.
///     <para>
///         These names map to nothing. They are not badges, they carry no family, and nothing
///         may colour or group by them: that association is the thing the rollup's removal was
///         about.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetArchivedSkillTagsQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>;

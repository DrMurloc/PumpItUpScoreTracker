using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Known board tags for the mix.
///     <para>
///         <paramref name="CurrentBoardsOnly" /> false (the default) returns every tag ever seen,
///         departed players included — history is searchable, which is what the Players view wants.
///         True narrows to tags that placed in the latest sealed snapshot, which is what a rival
///         PICKER wants: offering a departed tag hands somebody a permanently empty rivalry
///         (docs/design/rivals.md D21).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialPlayerNamesQuery(MixEnum Mix, bool CurrentBoardsOnly = false)
    : IQuery<IReadOnlyList<string>>;

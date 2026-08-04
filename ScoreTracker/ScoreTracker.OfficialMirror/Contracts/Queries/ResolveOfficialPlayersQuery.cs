using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     <see cref="ResolveOfficialPlayerQuery" /> for a set of tags at once. A roster resolves every
///     board-only rival it holds on one render, and a query per tag would put a burst of round
///     trips behind a page that is mostly waiting on this.
///     <para>
///         Tags the mirror has never seen are simply absent from the result — the caller decides
///         whether that reads as "no longer on the boards" or as nothing at all.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ResolveOfficialPlayersQuery(MixEnum Mix, IReadOnlyCollection<string> Tags)
    : IQuery<IReadOnlyList<OfficialPlayerResolution>>;

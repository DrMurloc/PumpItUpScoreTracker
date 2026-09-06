using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     What March of Murlocs has to say about one night of play (D32): whether it is already on a
///     board, whether a window inside it would make a session, and which board a new one would go
///     on. Null when there is no live season at all.
///     <para>
///         The night's own bounds are only the search range — the window slides inside them,
///         because a night can carry plays a skipped import left behind and the session can sit
///         anywhere in it.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DetectMoMSessionQuery(Guid UserId, MixEnum Mix, DateTimeOffset From, DateTimeOffset To)
    : IQuery<MoMOnRamp?>;

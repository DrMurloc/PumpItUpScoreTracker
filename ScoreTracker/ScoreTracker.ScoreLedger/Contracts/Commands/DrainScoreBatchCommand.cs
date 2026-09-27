using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Announces the player's open two-minute batch now instead of when it goes quiet — for a CSV upload,
///     which knows when it has saved its last row.
///     <para>
///         It only brings the batch's deadline forward and hands the drain to the bus, the same message the
///         timer sends. Nothing is taken here, so a drain that never happens — the page closed, the publish
///         failed — leaves the batch where it was, and the timer already scheduled for it announces it two
///         minutes later as it always did.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DrainScoreBatchCommand(Guid UserId, MixEnum Mix) : IRequest;

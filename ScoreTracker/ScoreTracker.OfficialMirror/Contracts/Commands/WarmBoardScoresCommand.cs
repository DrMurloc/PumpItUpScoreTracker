using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Commands;

/// <summary>
///     Loads the mix's board scores into memory ahead of anyone asking for them
///     (docs/design/pumbility-overhaul.md §6.14). Sent once per mix at startup.
///     <para>
///         A command rather than a hosted service inside the vertical: a vertical may not reference
///         the hosting abstractions, and the store is internal, so the host asks for the warm-up
///         through the same MediatR seam it uses for everything else.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WarmBoardScoresCommand(MixEnum Mix) : IRequest;

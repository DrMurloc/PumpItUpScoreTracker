using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Replaces the active draw for a context with a fresh card set. Null TournamentId
    ///     = the current user's personal draw. The context's spectator slug is stable —
    ///     redraws swap the cards under the same link. Chart selection happens upstream
    ///     (the page dispatches GetRandomChartsQuery); this command only records the pull.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record CreateDrawCommand(Guid? TournamentId, MixEnum Mix, IReadOnlyList<Guid> ChartIds)
        : IRequest<DrawDto>
    {
    }
}

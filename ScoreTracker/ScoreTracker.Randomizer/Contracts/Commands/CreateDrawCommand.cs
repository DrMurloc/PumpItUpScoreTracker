using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Personal (null TournamentId): replaces the user's one rolling draw, stable slug.
    ///     Tournament: creates a NEW named match draw (MatchName required) with its own
    ///     stable slug — RedrawCardsCommand is how an existing match refills. Chart
    ///     selection happens upstream (the page dispatches GetRandomChartsQuery); this
    ///     command only records the pull.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record CreateDrawCommand(Guid? TournamentId, MixEnum Mix, IReadOnlyList<Guid> ChartIds,
        string? MatchName = null) : IRequest<DrawDto>
    {
    }
}

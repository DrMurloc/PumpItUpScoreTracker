using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Domain
{
    /// <summary>
    ///     Draw persistence. A context (user XOR tournament) has at most one active draw;
    ///     its slug is minted once and survives redraws, so spectator links stay stable.
    /// </summary>
    internal interface IDrawRepository
    {
        Task<DrawDto> ReplaceDraw(Guid? userId, Guid? tournamentId, MixEnum mix, IReadOnlyList<Guid> chartIds,
            CancellationToken cancellationToken);

        Task SetCardState(Guid drawId, Guid pullId, DrawCardState state, CancellationToken cancellationToken);
        Task<DrawDto> ClearVetoed(Guid drawId, CancellationToken cancellationToken);
        Task<DrawDto> AddChart(Guid drawId, Guid chartId, CancellationToken cancellationToken);
        Task<DrawDto?> GetDraw(Guid drawId, CancellationToken cancellationToken);
        Task<DrawDto?> GetActiveDraw(Guid? userId, Guid? tournamentId, CancellationToken cancellationToken);
        Task<DrawDto?> GetBySlug(Guid slug, CancellationToken cancellationToken);
    }
}

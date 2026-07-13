using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Domain
{
    /// <summary>
    ///     Draw persistence. Personal context: one rolling draw per user, slug minted once.
    ///     Tournament context: many named draws — matches — each with its own stable slug;
    ///     redraws refill in place so spectator links keep working.
    /// </summary>
    internal interface IDrawRepository
    {
        Task<DrawDto> ReplacePersonalDraw(Guid userId, MixEnum mix, IReadOnlyList<Guid> chartIds,
            CancellationToken cancellationToken);

        Task<DrawDto> CreateTournamentDraw(Guid tournamentId, string name, MixEnum mix,
            IReadOnlyList<Guid> chartIds, CancellationToken cancellationToken);

        Task<DrawDto> RedrawCards(Guid drawId, MixEnum mix, IReadOnlyList<Guid> chartIds,
            CancellationToken cancellationToken);

        Task RenameDraw(Guid drawId, string name, CancellationToken cancellationToken);
        Task SetCardState(Guid drawId, Guid pullId, DrawCardState state, CancellationToken cancellationToken);
        Task<DrawDto> ClearVetoed(Guid drawId, CancellationToken cancellationToken);
        Task<DrawDto> AddChart(Guid drawId, Guid chartId, CancellationToken cancellationToken);
        Task<DrawDto?> GetDraw(Guid drawId, CancellationToken cancellationToken);
        Task<DrawDto?> GetActiveDraw(Guid? userId, Guid? tournamentId, CancellationToken cancellationToken);
        Task<DrawDto?> GetBySlug(Guid slug, CancellationToken cancellationToken);
        Task<IEnumerable<DrawDto>> GetTournamentDraws(Guid tournamentId, CancellationToken cancellationToken);
        Task DeleteDraw(Guid drawId, CancellationToken cancellationToken);
    }
}

using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Domain
{
    internal interface IRandomizerRepository
    {
        Task SaveSettings(Guid userId, Name settingsName, RandomSettings settings, MixEnum mix,
            CancellationToken cancellationToken);

        Task DeleteSettings(Guid userId, Name settingsName, CancellationToken cancellationToken);

        Task SaveTournamentSettings(Guid tournamentId, Name settingsName, RandomSettings settings, MixEnum mix,
            CancellationToken cancellationToken);

        Task DeleteTournamentSettings(Guid tournamentId, Name settingsName, CancellationToken cancellationToken);

        /// <summary>Mints the settings' share token, or returns the existing one.</summary>
        Task<Guid> EnsureShareToken(Guid userId, Name settingsName, CancellationToken cancellationToken);
    }
}

using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Domain
{
    internal interface IRandomizerRepository
    {
        Task SaveSettings(Guid userId, Name settingsName, RandomSettings settings, CancellationToken cancellationToken);
        Task DeleteSettings(Guid userId, Name settingsName, CancellationToken cancellationToken);
    }
}

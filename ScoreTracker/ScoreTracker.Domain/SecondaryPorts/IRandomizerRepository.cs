using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IRandomizerRepository
    {
        Task SaveSettings(Guid userId, Name settingsName, RandomSettings settings, CancellationToken cancellationToken);
        Task DeleteSettings(Guid userId, Name settingsName, CancellationToken cancellationToken);
    }
}

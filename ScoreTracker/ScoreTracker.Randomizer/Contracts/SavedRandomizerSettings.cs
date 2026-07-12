using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Contracts
{
    [ExcludeFromCodeCoverage]
    public sealed record SavedRandomizerSettings(Name SettingsName, RandomSettings Settings)
    {
    }
}

using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record SavedRandomizerSettings(Name SettingsName, RandomSettings Settings)
    {
    }
}

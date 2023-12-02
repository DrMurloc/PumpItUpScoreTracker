using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class RandomSettings
    {
        public IDictionary<int, int> LevelWeights { get; set; } = DifficultyLevel.All.ToDictionary(l => (int)l, l => 0);

        public IDictionary<int, int> DoubleLevelWeights { get; set; } =
            DifficultyLevel.All.ToDictionary(l => (int)l, l => 0);

        public IDictionary<ChartType, int> ChartTypeWeights { get; set; } =
            Enum.GetValues<ChartType>().ToDictionary(t => t, t => 0);

        public IDictionary<SongType, int> SongTypeWeights { get; set; } =
            Enum.GetValues<SongType>().ToDictionary(t => t, t => 0);

        public IDictionary<int, int> PlayerCountWeights { get; set; } = new Dictionary<int, int>
        {
            { 2, 0 },
            { 3, 0 },
            { 4, 0 },
            { 5, 0 }
        };

        public bool AllowRepeats { get; set; } = false;
        public int Count { get; set; } = 3;

        public bool HasWeightedSetting => LevelWeights.Any(kv => kv.Value > 1)
                                          || DoubleLevelWeights.Any(kv =>
                                              kv.Value > 1) ||
                                          ChartTypeWeights.Any(kv => kv.Value > 1)
                                          || PlayerCountWeights.Any(kvp =>
                                              kvp.Value > 1)
                                          ||
                                          SongTypeWeights.Any(kvp =>
                                              kvp.Value > 1);
    }
}

﻿using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed record RandomSettings
    {
        public IDictionary<int, int> LevelWeights { get; set; } = DifficultyLevel.All.ToDictionary(l => (int)l, l => 0);

        public IDictionary<int, int> DoubleLevelWeights { get; set; } =
            DifficultyLevel.All.ToDictionary(l => (int)l, l => 0);

        public IDictionary<SongType, int> SongTypeWeights { get; set; } =
            Enum.GetValues<SongType>().ToDictionary(t => t, t => 0);

        public void ClearChartTypeMinimums()
        {
            ChartTypeMinimums =
                new[] { ChartType.Single, ChartType.Double, ChartType.CoOp }.ToDictionary(c => c, c => (int?)null);
        }

        public void ClearLevelMinimums()
        {
            LevelMinimums =
                DifficultyLevel.All.ToDictionary(l => (int)l, l => (int?)null);
        }

        public void ClearChartTypeLevelMinimums()
        {
            ChartTypeLevelMinimums = DifficultyLevel.All
                .SelectMany(l => new[] { $"S{l}", $"D{l}", $"CoOp{l}" })
                .ToDictionary(k => k, k => (int?)null);
        }

        public IDictionary<int, int> PlayerCountWeights { get; set; } = new Dictionary<int, int>
        {
            { 2, 0 },
            { 3, 0 },
            { 4, 0 },
            { 5, 0 }
        };

        public bool AllowRepeats { get; set; } = false;
        public int Count { get; set; } = 3;
        public bool UseScoringLevels { get; set; } = false;

        public IDictionary<ChartType, int?> ChartTypeMinimums { get; set; } =
            new[] { ChartType.Single, ChartType.Double, ChartType.CoOp }.ToDictionary(c => c, c => (int?)null);

        public IDictionary<int, int?> LevelMinimums { get; set; } =
            DifficultyLevel.All.ToDictionary(l => (int)l, l => (int?)null);

        public IDictionary<string, int?> ChartTypeLevelMinimums { get; set; } = DifficultyLevel.All
            .SelectMany(l => new[] { $"S{l}", $"D{l}", $"CoOp{l}" })
            .ToDictionary(k => k, k => (int?)null);

        public bool HasWeightedSetting => LevelWeights.Any(kv => kv.Value > 1)
                                          || DoubleLevelWeights.Any(kv =>
                                              kv.Value > 1)
                                          || PlayerCountWeights.Any(kvp =>
                                              kvp.Value > 1)
                                          ||
                                          SongTypeWeights.Any(kvp =>
                                              kvp.Value > 1);
    }
}

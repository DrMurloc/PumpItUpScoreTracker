using ScoreTracker.SharedKernel.Models;
﻿using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed class QualifiersConfiguration
{
    public QualifiersConfiguration(IEnumerable<Chart> charts,
        IDictionary<Guid, int> adjustments, Name scoringType, ulong notificationChannel, int playCount,
        DateTimeOffset? cutoffTime, bool allCharts, MixEnum mix = MixEnum.Phoenix)
    {
        Charts = charts.ToArray();
        ScoringType = scoringType;
        NoteCountAdjustments = adjustments;
        foreach (var chart in Charts)
            if (!NoteCountAdjustments.ContainsKey(chart.Id))
                NoteCountAdjustments[chart.Id] = 0;
        NotificationChannel = notificationChannel;
        PlayCount = playCount;
        CutoffTime = cutoffTime;
        AllCharts = allCharts;
        Mix = mix;
    }

    public ulong NotificationChannel { get; }
    public IReadOnlyCollection<Chart> Charts { get; }
    public bool AllCharts { get; set; } = false;
    public IDictionary<Guid, int> NoteCountAdjustments { get; }
    public Name ScoringType { get; }
    public int PlayCount { get; }
    public DateTimeOffset? CutoffTime { get; }

    // The mix this tournament's qualifiers are pinned to (rows backfill Phoenix).
    public MixEnum Mix { get; }
}

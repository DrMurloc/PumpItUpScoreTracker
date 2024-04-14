using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed class QualifiersConfiguration
{
    public QualifiersConfiguration(IEnumerable<Chart> charts,
        IDictionary<Guid, int> adjustments, Name scoringType, ulong notificationChannel, int playCount)
    {
        Charts = charts.ToArray();
        ScoringType = scoringType;
        NoteCountAdjustments = adjustments;
        foreach (var chart in Charts)
            if (!NoteCountAdjustments.ContainsKey(chart.Id))
                NoteCountAdjustments[chart.Id] = 0;
        NotificationChannel = notificationChannel;
        PlayCount = playCount;
    }

    public ulong NotificationChannel { get; }
    public IReadOnlyCollection<Chart> Charts { get; }
    public IDictionary<Guid, int> NoteCountAdjustments { get; }
    public Name ScoringType { get; }
    public int PlayCount { get; }
}
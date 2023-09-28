
namespace ScoreTracker.Domain.Models
{
    public sealed class QualifiersConfiguration
    {
        public IReadOnlyCollection<Chart> Charts { get; }
        public IDictionary<Guid, int> NoteCountAdjustments { get; }

        public QualifiersConfiguration(IEnumerable<Chart> charts,
            IDictionary<Guid, int> adjustments)
        {
            Charts = charts.ToArray();
            NoteCountAdjustments = adjustments;
            foreach (var chart in Charts)
            {
                if (!NoteCountAdjustments.ContainsKey(chart.Id))
                {
                    NoteCountAdjustments[chart.Id] = 0;
                }
            }
        }
    }
}

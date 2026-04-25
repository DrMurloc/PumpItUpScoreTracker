using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record PiuGameUcsEntry(int Id, Chart Chart,
        string Description)
    {
    }
}

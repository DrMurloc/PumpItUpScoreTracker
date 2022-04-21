using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

public sealed class StepArtistTitle : Title
{
    public StepArtistTitle(Name artistName) : base($"{artistName} Follower",
        $"Play charts made by {artistName} 100 times", "Step Artist")
    {
    }

    public override bool DoesAttemptApply(BestChartAttempt attempt)
    {
        return false;
    }
}
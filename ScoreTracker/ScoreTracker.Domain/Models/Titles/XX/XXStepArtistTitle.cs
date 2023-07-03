using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXStepArtistTitle : XXTitle
{
    public XXStepArtistTitle(Name artistName) : base($"{artistName} Follower",
        $"Play charts made by {artistName} 100 times", "Step Artist")
    {
    }

    public override bool DoesAttemptApply(BestXXChartAttempt attempt)
    {
        return false;
    }
}
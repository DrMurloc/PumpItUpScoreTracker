using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXArtistTitle : XXTitle
{
    public XXArtistTitle(Name artistName) : base($"{artistName} Follower",
        $"Play songs composed by {artistName} 100 times", "Artist")
    {
    }

    public override bool DoesAttemptApply(BestXXChartAttempt attempt)
    {
        return false;
    }
}
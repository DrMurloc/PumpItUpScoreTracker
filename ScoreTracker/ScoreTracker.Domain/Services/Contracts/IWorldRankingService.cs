namespace ScoreTracker.Domain.Services.Contracts
{
    public interface IWorldRankingService
    {
        Task CalculateWorldRankings(CancellationToken cancellationToken);
    }
}

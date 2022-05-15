namespace ScoreTracker.Domain.Services.Contracts;

public interface IUserAccessService
{
    Task<bool> HasAccessTo(Guid targetUserId, CancellationToken cancellationToken = default);
}
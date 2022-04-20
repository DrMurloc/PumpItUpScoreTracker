using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IUserRepository
{
    Task SaveUser(User user, CancellationToken cancellationToken = default);
    Task CreateDiscordLogin(Guid userId, ulong discordId, CancellationToken cancellationToken = default);
    Task<User> GetUserByDiscordLogin(ulong discordId, CancellationToken cancellationToken = default);
}
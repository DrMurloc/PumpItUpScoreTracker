using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IUserRepository
{
    Task SaveUser(User user, CancellationToken cancellationToken = default);

    Task CreateExternalLogin(Guid userId, string loginProviderName, string externalId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<User>> SearchForUsersByName(string searchText, CancellationToken cancellationToken = default);
    Task<User?> GetUser(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetUserByExternalLogin(string loginProviderName, string externalId,
        CancellationToken cancellationToken = default);

    Task<IDictionary<string, string>> GetUserUiSettings(Guid userId, CancellationToken cancellationToken = default);

    Task SaveUserUiSettings(Guid userId, IDictionary<string, string> settings,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetUserApiToken(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetUserByApiToken(Guid apiToken, CancellationToken cancellationToken = default);
    Task SetUserApiToken(Guid userId, Guid apiToken, CancellationToken cancellationToken = default);
}
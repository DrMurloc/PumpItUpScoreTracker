using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Identity's published read contract (ADR-001): consumers outside Identity read user
///     records through this, never through <see cref="IUserRepository" /> (which becomes
///     Identity-internal at the P6 teardown). Writes go through Identity's contract
///     commands.
/// </summary>
public interface IUserReader
{
    Task<User?> GetUser(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetUsers(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}

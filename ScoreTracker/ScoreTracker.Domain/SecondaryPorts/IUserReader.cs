using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

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

    /// <summary>
    ///     Accounts whose game tag matches any of these, <b>ignoring spaces and case</b>. The site
    ///     stores a tag as <c>NAME #1234</c> and piugame writes it <c>NAME#1234</c>, so an exact
    ///     comparison finds nothing at all — it is the only reason this read is not a plain
    ///     equality (docs/design/pumbility-overhaul.md D61). Tags nobody claims are simply absent,
    ///     and a tag two accounts claim returns both: the caller decides.
    /// </summary>
    Task<IReadOnlyList<User>> GetUsersByGameTags(IReadOnlyCollection<string> gameTags,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     One sign-in provider's external ids for many accounts at once, keyed by user id.
    ///     Accounts with no login for that provider are absent rather than null-valued. Exists so a
    ///     sweep over a roster is one read instead of one per member; an account holding two logins
    ///     for the same provider is not expected, and the first wins.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetExternalLogins(IEnumerable<Guid> userIds,
        string loginProviderName, CancellationToken cancellationToken = default);
}

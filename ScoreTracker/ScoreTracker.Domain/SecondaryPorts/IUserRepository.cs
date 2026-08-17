using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IUserRepository
{
    Task SaveUser(User user, CancellationToken cancellationToken = default);

    Task CreateExternalLogin(Guid userId, string loginProviderName, string externalId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExternalLoginRecord>> GetExternalLogins(Guid userId,
        CancellationToken cancellationToken = default);

    Task RemoveExternalLogin(Guid userId, string loginProviderName, string externalId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<User>> SearchForUsersByName(string searchText, CancellationToken cancellationToken = default);

    /// <summary>
    ///     The per-keystroke search: players whose site name or game tag contains the term, limited
    ///     to public players plus the ids in <paramref name="alsoVisible" />, best matches first —
    ///     an exact name, then a name that starts with the term, then anything that contains it,
    ///     alphabetical inside each rung. The whole predicate, the order and the cap run in SQL:
    ///     a one-character term over the player table has to stay cheap, and a player named
    ///     "D" has to come first.
    /// </summary>
    Task<IReadOnlyList<User>> SearchVisibleUsers(string term, int take, IReadOnlyCollection<Guid> alsoVisible,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<User>> GetUsersByGameTag(Name gameTag, CancellationToken cancellationToken = default);
    Task<User?> GetUser(Guid userId, CancellationToken cancellationToken = default);
    Task<DateTimeOffset> GetClaimsInvalidatedAt(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Takes one deep scan from this account's balance, or returns false when none are left.
    ///     The read and the decrement are ONE statement: a check-then-write would let two tabs, or
    ///     one impatient double-click, both pass the same last scan.
    /// </summary>
    Task<bool> TrySpendDeepScan(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetDeepScansRemaining(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sets every account's balance back to the monthly allowance.</summary>
    Task ResetDeepScans(int allowance, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetUsers(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    Task<User?> GetUserByExternalLogin(string loginProviderName, string externalId,
        CancellationToken cancellationToken = default);

    Task<IDictionary<string, string>> GetUserUiSettings(Guid userId, CancellationToken cancellationToken = default);

    Task SaveUserUiSettings(Guid userId, IDictionary<string, string> settings,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetUserApiToken(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetUserByApiToken(Guid apiToken, CancellationToken cancellationToken = default);
    Task SetUserApiToken(Guid userId, Guid apiToken, CancellationToken cancellationToken = default);

    Task CreateCountry(CountryRecord country, CancellationToken cancellationToken = default);
    Task<Uri?> GetCountryImage(Name countryName, CancellationToken cancellationToken = default);
    Task<IEnumerable<CountryRecord>> GetCountries(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Registered players + distinct countries represented. Serves the anonymous
    ///     front door (docs/design/front-door.md D6/D7) — the implementation caches.
    /// </summary>
    Task<PlayerbaseCounts> GetPlayerbaseCounts(CancellationToken cancellationToken = default);
}
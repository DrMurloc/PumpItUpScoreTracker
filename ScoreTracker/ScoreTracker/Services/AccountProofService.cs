using Microsoft.Extensions.Caching.Memory;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Short-lived "this user proved control of that account" facts backing the merge wizard's
///     prove step (login-overhaul design: one successful sign-in per account). Singleton over
///     IMemoryCache because proofs are recorded during controller HTTP requests (OAuth verify
///     callbacks) but consumed inside Blazor circuits — different DI scopes, same process.
/// </summary>
public sealed class AccountProofService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _cache;

    public AccountProofService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void RecordProof(Guid proverUserId, Guid provenUserId)
    {
        _cache.Set(Key(proverUserId, provenUserId), true, Ttl);
    }

    public bool HasProof(Guid proverUserId, Guid provenUserId)
    {
        return proverUserId == provenUserId || _cache.TryGetValue(Key(proverUserId, provenUserId), out _);
    }

    private static string Key(Guid proverUserId, Guid provenUserId)
    {
        return $"{nameof(AccountProofService)}_{proverUserId}_{provenUserId}";
    }
}

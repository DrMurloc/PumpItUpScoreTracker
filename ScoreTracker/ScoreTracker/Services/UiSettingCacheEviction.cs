using MediatR;
using MediatR.Pipeline;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Evicts the cached settings the moment a UI setting changes. Both the shell and the
///     language provider read those settings through a minutes-long <see cref="IMemoryCache" />
///     entry, so without this hook a signed-in mix switch (or /Account theme override, game tag,
///     avatar write, language change) stays invisible until the cache expires.
///     <para>
///         Every settings write funnels through <see cref="SaveUserUiSettingCommand" /> or
///         <see cref="ClearUserUiSettingCommand" />, which makes this the single eviction point —
///         both are registered against this one class in Program.cs. A third command that touches
///         the blob without arriving here would reintroduce the stale-for-five-minutes bug.
///     </para>
/// </summary>
public sealed class UiSettingCacheEviction :
    IRequestPostProcessor<SaveUserUiSettingCommand, Unit>,
    IRequestPostProcessor<ClearUserUiSettingCommand, Unit>
{
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserAccessor _currentUser;

    public UiSettingCacheEviction(IMemoryCache cache, ICurrentUserAccessor currentUser)
    {
        _cache = cache;
        _currentUser = currentUser;
    }

    public Task Process(ClearUserUiSettingCommand request, Unit response, CancellationToken cancellationToken)
    {
        return Evict();
    }

    public Task Process(SaveUserUiSettingCommand request, Unit response, CancellationToken cancellationToken)
    {
        return Evict();
    }

    private Task Evict()
    {
        if (_currentUser.IsLoggedIn)
            _cache.Remove(ShellModelFactory.SettingsCacheKey(_currentUser.User.Id));

        return Task.CompletedTask;
    }
}

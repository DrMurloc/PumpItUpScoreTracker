using MediatR;
using MediatR.Pipeline;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Evicts the shell's cached settings the moment a UI setting is saved. The shell reads
///     settings through a minutes-long <see cref="IMemoryCache" /> entry and prefers them over
///     the anonymous mix cookie, so without this hook a signed-in mix switch (or /Account theme
///     override, game tag, avatar write) stays invisible until the cache expires — every
///     settings write funnels through <see cref="SaveUserUiSettingCommand" />, making this the
///     single eviction point.
/// </summary>
public sealed class UiSettingSavedCacheEviction : IRequestPostProcessor<SaveUserUiSettingCommand, Unit>
{
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserAccessor _currentUser;

    public UiSettingSavedCacheEviction(IMemoryCache cache, ICurrentUserAccessor currentUser)
    {
        _cache = cache;
        _currentUser = currentUser;
    }

    public Task Process(SaveUserUiSettingCommand request, Unit response, CancellationToken cancellationToken)
    {
        if (_currentUser.IsLoggedIn)
            _cache.Remove(ShellModelFactory.SettingsCacheKey(_currentUser.User.Id));

        return Task.CompletedTask;
    }
}

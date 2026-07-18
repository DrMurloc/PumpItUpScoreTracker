using MediatR;
using MediatR.Pipeline;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The shell serves signed-in users their settings (including the current mix) through a
///     cached read that wins over the anonymous cookie, so a settings save must evict that
///     cache or the change stays invisible until the TTL — a mix switch that "does nothing"
///     for five minutes. The pipeline fact sends the real command through MediatR with the
///     same closed-type registration Program.cs uses, proving the post-processor actually
///     runs on save.
/// </summary>
public sealed class UiSettingSavedCacheEvictionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static User LoggedInUser()
    {
        return new User(UserId, "Test User", true, null, new Uri("https://example.com/avatar.png"), null);
    }

    [Fact]
    public async Task Saving_a_setting_through_the_mediator_evicts_the_shell_settings_cache()
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        currentUser.Setup(c => c.User).Returns(LoggedInUser());
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddMemoryCache();
        services.AddSingleton(currentUser.Object);
        services.AddSingleton(users.Object);
        services.AddMediatR(o =>
        {
            o.AddRequestPostProcessor<IRequestPostProcessor<SaveUserUiSettingCommand, Unit>,
                UiSettingSavedCacheEviction>();
            o.RegisterServicesFromAssemblies(typeof(SaveUserUiSettingCommand).Assembly);
        });
        await using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<IMemoryCache>();
        cache.Set(ShellModelFactory.SettingsCacheKey(UserId),
            (IDictionary<string, string>)new Dictionary<string, string> { ["Universal__CurrentMix"] = "Phoenix" });

        await provider.GetRequiredService<IMediator>()
            .Send(new SaveUserUiSettingCommand("Universal__CurrentMix", "Phoenix2"));

        Assert.False(cache.TryGetValue(ShellModelFactory.SettingsCacheKey(UserId), out _),
            "The save completed without evicting the shell settings cache — the switch stays stale until the TTL.");
    }

    [Fact]
    public async Task Anonymous_saves_leave_the_cache_alone_and_do_not_throw()
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(c => c.IsLoggedIn).Returns(false);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var unrelatedKey = ShellModelFactory.SettingsCacheKey(UserId);
        cache.Set(unrelatedKey, new Dictionary<string, string>());

        await new UiSettingSavedCacheEviction(cache, currentUser.Object)
            .Process(new SaveUserUiSettingCommand("Universal__CurrentMix", "Phoenix2"), Unit.Value,
                CancellationToken.None);

        Assert.True(cache.TryGetValue(unrelatedKey, out _));
    }
}

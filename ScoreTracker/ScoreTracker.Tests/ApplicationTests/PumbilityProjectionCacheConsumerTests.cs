using System;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.Domain.Events;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PumbilityProjectionCacheConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    /// <summary>
    ///     The projection prices its targets off the player's competitive level, which lives in the
    ///     stats the rating step writes after the score event. An import announces the moment it has
    ///     saved, so a page loaded in between caches a projection on the old level — a first import's
    ///     is empty — and without this eviction it would stand for a day.
    /// </summary>
    [Fact]
    public async Task RecalculatedStatsDropTheProjectionBuiltBeforeThem()
    {
        using var cache = new PumbilityProjectionCache();
        var computed = 0;
        Task<ProjectionSweep> Compute()
        {
            computed++;
            return Task.FromResult<ProjectionSweep>(null!);
        }

        await cache.GetOrAdd(UserId, MixEnum.Phoenix2, Compute);
        await new PumbilityProjectionCacheConsumer(cache)
            .Handle(new PlayerStatsUpdatedEvent(UserId, null!, MixEnum.Phoenix2), CancellationToken.None);
        await cache.GetOrAdd(UserId, MixEnum.Phoenix2, Compute);

        Assert.Equal(2, computed);
    }

    [Fact]
    public async Task StatsOnOneMixLeaveTheOthersProjectionAlone()
    {
        using var cache = new PumbilityProjectionCache();
        var computed = 0;
        Task<ProjectionSweep> Compute()
        {
            computed++;
            return Task.FromResult<ProjectionSweep>(null!);
        }

        await cache.GetOrAdd(UserId, MixEnum.Phoenix, Compute);
        await new PumbilityProjectionCacheConsumer(cache)
            .Handle(new PlayerStatsUpdatedEvent(UserId, null!, MixEnum.Phoenix2), CancellationToken.None);
        await cache.GetOrAdd(UserId, MixEnum.Phoenix, Compute);

        Assert.Equal(1, computed);
    }
}

using System;
using ScoreTracker.OfficialMirror.Infrastructure;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ImportConcurrencyGuardTests
{
    [Fact]
    public void SecondBeginForTheSameUserIsRefusedUntilTheFirstEnds()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();

        Assert.True(guard.TryBegin(user));
        Assert.False(guard.TryBegin(user));

        guard.End(user);
        Assert.True(guard.TryBegin(user));
    }

    [Fact]
    public void DifferentUsersEachGetTheirOwnSlot()
    {
        var guard = new ImportConcurrencyGuard();

        Assert.True(guard.TryBegin(Guid.NewGuid()));
        Assert.True(guard.TryBegin(Guid.NewGuid()));
    }

    [Fact]
    public void EndingASlotThatWasNeverHeldIsHarmless()
    {
        var guard = new ImportConcurrencyGuard();

        guard.End(Guid.NewGuid());
    }
}

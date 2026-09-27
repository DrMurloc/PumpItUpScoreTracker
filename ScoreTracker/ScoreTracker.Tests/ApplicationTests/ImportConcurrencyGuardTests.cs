using System;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ImportConcurrencyGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static ImportSlotOutcome Begin(ImportConcurrencyGuard guard, Guid user, MixEnum mix = MixEnum.Phoenix,
        DateTimeOffset? at = null, bool cooldownApplies = true)
    {
        return guard.TryBegin(user, mix, at ?? Now, cooldownApplies).Outcome;
    }

    [Fact]
    public void SecondBeginForTheSameUserIsRefusedUntilTheFirstEnds()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();

        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, user));
        Assert.Equal(ImportSlotOutcome.AlreadyRunning, Begin(guard, user));

        guard.End(user);
        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, user));
    }

    [Fact]
    public void OneImportAtATimeWhateverTheMix()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();

        Begin(guard, user, MixEnum.Phoenix);

        Assert.Equal(ImportSlotOutcome.AlreadyRunning, Begin(guard, user, MixEnum.Phoenix2));
    }

    [Fact]
    public void DifferentUsersEachGetTheirOwnSlot()
    {
        var guard = new ImportConcurrencyGuard();

        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, Guid.NewGuid()));
        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, Guid.NewGuid()));
    }

    [Fact]
    public void EndingASlotThatWasNeverHeldIsHarmless()
    {
        var guard = new ImportConcurrencyGuard();

        guard.End(Guid.NewGuid());
    }

    [Fact]
    public void AnImportOnTheSameMixWaitsFiveMinutesFromTheLastStart()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();
        Begin(guard, user);
        guard.Started(user, MixEnum.Phoenix, Now);
        guard.End(user);

        var early = guard.TryBegin(user, MixEnum.Phoenix, Now.AddMinutes(3), true);

        Assert.Equal(ImportSlotOutcome.CoolingDown, early.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(2), early.RetryAfter);
        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, user, at: Now.Add(ImportConcurrencyGuard.Cooldown)));
    }

    [Fact]
    public void ARefusedBeginHoldsNothing()
    {
        // Refused for the cooldown, the user must not be left looking busy: nothing else could save.
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();
        guard.Started(user, MixEnum.Phoenix, Now);

        Begin(guard, user, at: Now.AddMinutes(1));

        Assert.False(guard.IsRunning(user, MixEnum.Phoenix));
        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, user, MixEnum.Phoenix2, Now.AddMinutes(1)));
    }

    [Fact]
    public void EachMixHasItsOwnClock()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();
        guard.Started(user, MixEnum.Phoenix, Now);

        Assert.Equal(ImportSlotOutcome.Taken, Begin(guard, user, MixEnum.Phoenix2, Now.AddMinutes(1)));
    }

    [Fact]
    public void ADeepScanIsNeverHeldBackByTheClock()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();
        guard.Started(user, MixEnum.Phoenix, Now);

        Assert.Equal(ImportSlotOutcome.Taken,
            Begin(guard, user, at: Now.AddMinutes(1), cooldownApplies: false));
    }

    [Fact]
    public void RunningIsPerMixAndEndsWithTheSlot()
    {
        var guard = new ImportConcurrencyGuard();
        var user = Guid.NewGuid();
        Begin(guard, user, MixEnum.Phoenix2);

        Assert.True(guard.IsRunning(user, MixEnum.Phoenix2));
        Assert.False(guard.IsRunning(user, MixEnum.Phoenix));
        Assert.False(guard.IsRunning(Guid.NewGuid(), MixEnum.Phoenix2));

        guard.End(user);
        Assert.False(guard.IsRunning(user, MixEnum.Phoenix2));
    }
}

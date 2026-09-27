using System;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ImportInProgressHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnImportIsRunningFromThePressToTheEndOnItsOwnMixOnly()
    {
        var guard = new ImportConcurrencyGuard();
        var handler = new ImportInProgressHandler(guard);
        var user = Guid.NewGuid();

        guard.TryBegin(user, MixEnum.Phoenix2, Now, true);

        Assert.True(await handler.Handle(new GetImportInProgressQuery(user, MixEnum.Phoenix2), CancellationToken.None));
        Assert.False(await handler.Handle(new GetImportInProgressQuery(user, MixEnum.Phoenix), CancellationToken.None));

        guard.End(user);
        Assert.False(await handler.Handle(new GetImportInProgressQuery(user, MixEnum.Phoenix2), CancellationToken.None));
    }
}

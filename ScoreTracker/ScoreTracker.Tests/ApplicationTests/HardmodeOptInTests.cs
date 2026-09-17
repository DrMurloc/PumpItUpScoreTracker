using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Reading the Hardmode switch (docs/design/hardmode-leaderboard.md D30) — the one read the session
///     page, the Discord card and the feed capture all go through.
/// </summary>
public sealed class HardmodeOptInTests
{
    private static readonly Guid Player = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ASettingWithAValueIsOn()
    {
        _mediator.Setup(m => m.Send(It.Is<GetUserUiSettingsQuery>(q => q.UserId == Player),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HardmodeOptIn.SettingKey] = "true"
            });

        Assert.True(await HardmodeOptIn.Read(_mediator.Object, Player, CancellationToken.None));
    }

    [Fact]
    public async Task AFailedReadCountsAsOff()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("settings unavailable"));

        Assert.False(await HardmodeOptIn.Read(_mediator.Object, Player, CancellationToken.None));
    }

    [Fact]
    public async Task ACancelledReadIsNotSwallowed()
    {
        // Off is the answer to a failure, not to the caller giving up: a cancelled circuit or
        // consumer has to stop, not carry on as if the player had switched Hardmode off.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancelled.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            HardmodeOptIn.Read(_mediator.Object, Player, cancelled.Token));
    }
}

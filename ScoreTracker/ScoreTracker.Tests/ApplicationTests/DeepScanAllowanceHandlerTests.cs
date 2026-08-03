using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.Identity.Contracts.Queries;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The deep-scan allowance, as the Official Mirror sees it: ask for a scan, ask how many are
///     left, and the monthly refill. Identity owns the User row, so the mirror never reaches for
///     the column itself.
/// </summary>
public sealed class DeepScanAllowanceHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (DeepScanAllowanceHandlers Handlers, Mock<IUserRepository> Users) Build()
    {
        var users = new Mock<IUserRepository>();
        return (new DeepScanAllowanceHandlers(users.Object), users);
    }

    [Fact]
    public async Task SpendingAScanAsksTheRepositoryToTakeOneFromTheBalance()
    {
        var (handlers, users) = Build();
        users.Setup(u => u.TrySpendDeepScan(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.True(await handlers.Handle(new SpendDeepScanCommand(UserId), CancellationToken.None));
        users.Verify(u => u.TrySpendDeepScan(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEmptyBalanceRefusesRatherThanGoingNegative()
    {
        var (handlers, users) = Build();
        users.Setup(u => u.TrySpendDeepScan(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.False(await handlers.Handle(new SpendDeepScanCommand(UserId), CancellationToken.None));
    }

    [Fact]
    public async Task TheRemainingBalanceIsReadStraightFromTheAccount()
    {
        var (handlers, users) = Build();
        users.Setup(u => u.GetDeepScansRemaining(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        Assert.Equal(2, await handlers.Handle(new GetDeepScansRemainingQuery(UserId), CancellationToken.None));
    }

    [Fact]
    public async Task TheMonthlyJobRefillsEveryAccountToTheAllowance()
    {
        var (handlers, users) = Build();
        var context = new Mock<ConsumeContext<ResetDeepScansCommand>>();
        context.SetupGet(c => c.Message).Returns(new ResetDeepScansCommand());
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await handlers.Consume(context.Object);

        // Set to the allowance, not incremented: an unused month does not bank scans for the next.
        users.Verify(u => u.ResetDeepScans(DeepScanAllowance.PerMonth, It.IsAny<CancellationToken>()), Times.Once);
    }
}

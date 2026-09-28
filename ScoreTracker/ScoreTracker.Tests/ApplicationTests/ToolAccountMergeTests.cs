using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     What an account merge does to the tools the retired account made. A merge keeps one account
///     and purges the other's data after the grace window; tools are the exception, so a maker keeps
///     every tool whichever account they keep.
/// </summary>
public sealed class ToolAccountMergeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Survivor = Guid.Parse("dddddddd-4444-4444-4444-444444444444");
    private static readonly Guid Retired = Guid.Parse("eeeeeeee-5555-5555-5555-555555555555");

    private readonly Mock<IToolMakerBanRepository> _bans = new();
    private readonly Mock<IToolRepository> _tools = new();

    private AccountMergeConsumer Consumer()
    {
        return new AccountMergeConsumer(_tools.Object, _bans.Object, FakeDateTime.At(Now).Object);
    }

    [Fact]
    public async Task TheRetiredAccountsToolsMoveToTheAccountKeptWithItsMakerConnected()
    {
        var planner = Guid.NewGuid();
        var sheet = Guid.NewGuid();
        _tools.Setup(t => t.MoveTools(Retired, Survivor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { planner, sheet });

        await Consumer().Consume(Message(new AccountsMergedEvent(Survivor, Retired)));

        _tools.Verify(t => t.MoveTools(Retired, Survivor, It.IsAny<CancellationToken>()), Times.Once);
        _tools.Verify(t => t.GrantShare(planner, Survivor, ShareSource.Direct, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _tools.Verify(t => t.GrantShare(sheet, Survivor, ShareSource.Direct, Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>A ban switches a maker's tools off; a merge must not be the way to switch them back on.</summary>
    [Fact]
    public async Task ABannedMakersToolsStayBehind()
    {
        _bans.Setup(b => b.GetBan(Retired, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolMakerBan(Retired, Now, Guid.NewGuid(), null));

        await Consumer().Consume(Message(new AccountsMergedEvent(Survivor, Retired)));

        _tools.Verify(t => t.MoveTools(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _tools.Verify(t => t.GrantShare(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ShareSource>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ConsumeContext<T> Message<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }
}

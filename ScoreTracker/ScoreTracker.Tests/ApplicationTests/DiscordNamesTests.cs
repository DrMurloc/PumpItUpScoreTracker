using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The handle behind a maker's linked Discord account: asked of the bot by id, remembered, and
///     never allowed to fail the page that wanted it.
/// </summary>
public sealed class DiscordNamesTests
{
    private const ulong DiscordId = 123456789012345678;

    private readonly Mock<IBotClient> _bot = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private DiscordNames Names()
    {
        return new DiscordNames(_bot.Object, _cache, NullLogger<DiscordNames>.Instance);
    }

    [Fact]
    public async Task TheHandleIsWhatDiscordReportsAndIsRememberedAfterward()
    {
        _bot.Setup(b => b.GetUser(DiscordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotUser(DiscordId, "stepmaniac_77", "StepManiac"));
        var names = Names();

        Assert.Equal("stepmaniac_77", await names.HandleOf(DiscordId.ToString(), CancellationToken.None));
        Assert.Equal("stepmaniac_77", await names.HandleOf(DiscordId.ToString(), CancellationToken.None));

        _bot.Verify(b => b.GetUser(DiscordId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The bot being down or never started costs the page a name, not the page — and the failure is
    // not remembered, so the next render asks again.
    [Fact]
    public async Task ABotThatCannotAnswerGivesNoHandleAndIsAskedAgainNextTime()
    {
        _bot.Setup(b => b.GetUser(DiscordId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Client was never started"));
        var names = Names();

        Assert.Null(await names.HandleOf(DiscordId.ToString(), CancellationToken.None));
        Assert.Null(await names.HandleOf(DiscordId.ToString(), CancellationToken.None));

        _bot.Verify(b => b.GetUser(DiscordId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AnIdThatIsNotADiscordIdIsNeverLookedUp()
    {
        Assert.Null(await Names().HandleOf("not-an-id", CancellationToken.None));

        _bot.Verify(b => b.GetUser(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

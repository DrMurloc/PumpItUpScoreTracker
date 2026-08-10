using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Application;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ClearUserUiSettingHandlerTests
{
    [Fact]
    public async Task RemovesOnlyTheNamedSetting()
    {
        var user = new UserBuilder().Build();
        var existing = new Dictionary<string, string> { { "Culture", "en-US" }, { "theme", "dark" } };
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new ClearUserUiSettingHandler(users.Object, currentUser.Object);
        await handler.Handle(new ClearUserUiSettingCommand("Culture"), CancellationToken.None);

        users.Verify(u => u.SaveUserUiSettings(user.Id,
            It.Is<IDictionary<string, string>>(s => !s.ContainsKey("Culture") && s["theme"] == "dark"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Clearing what was never set is a no-op, not a write. The blob is one row per player,
    ///     and rewriting it unchanged would evict every reader's cache for nothing.
    /// </summary>
    [Fact]
    public async Task WritesNothingWhenTheSettingWasNeverThere()
    {
        var user = new UserBuilder().Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { { "theme", "dark" } });
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new ClearUserUiSettingHandler(users.Object, currentUser.Object);
        await handler.Handle(new ClearUserUiSettingCommand("Culture"), CancellationToken.None);

        users.Verify(u => u.SaveUserUiSettings(It.IsAny<Guid>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

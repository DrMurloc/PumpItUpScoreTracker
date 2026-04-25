using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SaveUserUiSettingHandlerTests
{
    [Fact]
    public async Task PersistsNewValueAlongsideExistingSettings()
    {
        var user = new UserBuilder().Build();
        var existing = new Dictionary<string, string> { { "theme", "dark" } };
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new SaveUserUiSettingHandler(users.Object, currentUser.Object);
        await handler.Handle(new SaveUserUiSettingCommand("language", "en-US"), CancellationToken.None);

        users.Verify(u => u.SaveUserUiSettings(user.Id,
            It.Is<IDictionary<string, string>>(s => s["theme"] == "dark" && s["language"] == "en-US"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OverwritesExistingValueForSameKey()
    {
        var user = new UserBuilder().Build();
        var existing = new Dictionary<string, string> { { "theme", "dark" } };
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new SaveUserUiSettingHandler(users.Object, currentUser.Object);
        await handler.Handle(new SaveUserUiSettingCommand("theme", "light"), CancellationToken.None);

        users.Verify(u => u.SaveUserUiSettings(user.Id,
            It.Is<IDictionary<string, string>>(s => s["theme"] == "light" && s.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

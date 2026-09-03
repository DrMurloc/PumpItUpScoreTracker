using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     An avatar is stored twice — on the User row for the claims, and as a UI setting for the
///     static shell's app-bar avatar — so these tests assert on the pair every time. A change that
///     satisfies one and not the other is the exact failure this handler exists to prevent: the
///     top-right corner disagreeing with every other surface, which reads as a caching bug.
/// </summary>
public sealed class UpdateUserGameProfileHandlerTests
{
    private static readonly Uri ExistingAvatar = new("https://piuimages.arroweclip.se/avatars/existing.png");
    private static readonly Uri ScrapedAvatar = new("https://piuimages.arroweclip.se/avatars/p2/scraped.png");

    private static (UpdateUserGameProfileHandler handler, Mock<IUserRepository> users, Mock<IMediator> mediator)
        Build(User existing)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(existing);
        var mediator = new Mock<IMediator>();
        return (new UpdateUserGameProfileHandler(currentUser.Object, users.Object, mediator.Object), users, mediator);
    }

    [Fact]
    public async Task AnImportWritesTheScrapedAvatarToBothPlacesItIsStored()
    {
        var existing = new UserBuilder().WithProfileImage(ExistingAvatar).Build();
        var (handler, users, mediator) = Build(existing);

        await handler.Handle(new UpdateUserGameProfileCommand(Name.From("NEWTAG"), ScrapedAvatar),
            CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(saved =>
                saved.ProfileImage == ScrapedAvatar && saved.GameTag == Name.From("NEWTAG")),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.SettingName == "ProfileImage" && c.NewValue == ScrapedAvatar.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The scraper misses the avatar often enough that persisting the miss was a real bug —
    ///     it wiped good avatars sporadically. A null means "keep what you have", in both stores.
    /// </summary>
    [Fact]
    public async Task AScrapeThatFoundNoAvatarChangesNeitherStore()
    {
        var existing = new UserBuilder().WithProfileImage(ExistingAvatar).Build();
        var (handler, users, mediator) = Build(existing);

        await handler.Handle(new UpdateUserGameProfileCommand(Name.From("NEWTAG"), null),
            CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(saved => saved.ProfileImage == ExistingAvatar),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<SaveUserUiSettingCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

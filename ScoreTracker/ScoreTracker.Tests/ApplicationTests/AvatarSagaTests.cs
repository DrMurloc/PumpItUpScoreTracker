using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class AvatarSagaTests
{
    private static readonly Uri Imported = new("https://piuimages.arroweclip.se/avatars/imported.png");
    private static readonly Uri Chosen = new("https://piuimages.arroweclip.se/avatars/p2/chosen.png");

    private static (AvatarSaga saga, Mock<IUserRepository> users, Mock<IMediator> mediator) Build(User existing)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(existing);
        var mediator = new Mock<IMediator>();
        return (new AvatarSaga(currentUser.Object, users.Object, mediator.Object), users, mediator);
    }

    [Fact]
    public async Task PinningWritesTheChoiceToBothPlacesTheAvatarIsStored()
    {
        var user = new UserBuilder().WithProfileImage(Imported).WithImportedProfileImage(Imported).Build();
        var (saga, users, mediator) = Build(user);

        await saga.Handle(new PinAvatarCommand(Chosen), CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(s =>
            s.ProfileImage == Chosen && s.AvatarIsPinned), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.SettingName == "ProfileImage" && c.NewValue == Chosen.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Pinning must not throw away what the importer last saw — that record is the only thing
    ///     that makes unpinning instant instead of a wait for the next import.
    /// </summary>
    [Fact]
    public async Task PinningKeepsWhatTheImporterLastSaw()
    {
        var user = new UserBuilder().WithProfileImage(Imported).WithImportedProfileImage(Imported).Build();
        var (saga, users, _) = Build(user);

        await saga.Handle(new PinAvatarCommand(Chosen), CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(s => s.ImportedProfileImage == Imported),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnpinningRestoresTheImportedAvatarStraightAwayInBothPlaces()
    {
        var user = new UserBuilder().WithPinnedAvatar(Chosen).WithImportedProfileImage(Imported).Build();
        var (saga, users, mediator) = Build(user);

        await saga.Handle(new UnpinAvatarCommand(), CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(s =>
            s.ProfileImage == Imported && !s.AvatarIsPinned), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<SaveUserUiSettingCommand>(c =>
                c.NewValue == Imported.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     An account that has never imported has nothing to restore. Keeping what it is wearing
    ///     beats swapping in stock art the player never asked for.
    /// </summary>
    [Fact]
    public async Task UnpinningWithNothingRecordedKeepsTheCurrentPicture()
    {
        var user = new UserBuilder().WithPinnedAvatar(Chosen).WithImportedProfileImage(null).Build();
        var (saga, users, _) = Build(user);

        await saga.Handle(new UnpinAvatarCommand(), CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(s =>
            s.ProfileImage == Chosen && !s.AvatarIsPinned), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A profile picture renders on other players' screens. Letting a player point it at any
    ///     address they like would make the field an image embed with a request log attached.
    /// </summary>
    [Theory]
    [InlineData("https://example.invalid/tracker.png")]
    [InlineData("https://piuimages.arroweclip.se/songs/not-an-avatar.png")]
    [InlineData("http://piuimages.arroweclip.se/avatars/downgraded.png")]
    [InlineData("https://piuimages.arroweclip.se.evil.test/avatars/lookalike.png")]
    public async Task PinningRejectsAnythingOutsideTheAvatarCdn(string url)
    {
        var user = new UserBuilder().WithProfileImage(Imported).Build();
        var (saga, users, mediator) = Build(user);

        await Assert.ThrowsAsync<InvalidAvatarException>(() =>
            saga.Handle(new PinAvatarCommand(new Uri(url)), CancellationToken.None));

        users.Verify(u => u.SaveUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(m => m.Send(It.IsAny<SaveUserUiSettingCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReadingBackReportsWhichStateTheAccountIsIn()
    {
        var user = new UserBuilder().WithPinnedAvatar(Chosen).WithImportedProfileImage(Imported).Build();
        var (saga, _, _) = Build(user);

        var result = await saga.Handle(new GetMyAvatarQuery(), CancellationToken.None);

        Assert.Equal(Chosen, result.ImageUrl);
        Assert.True(result.IsPinned);
        Assert.Equal(Imported, result.ImportedImageUrl);
    }
}

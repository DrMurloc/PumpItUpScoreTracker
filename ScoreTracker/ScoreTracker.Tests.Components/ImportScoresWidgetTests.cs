using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.UiNotifications;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class ImportScoresWidgetTests : ComponentTestBase
{
    private readonly Mock<ISnackbar> _snackbar = new();

    public ImportScoresWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/avatar.png"), null));
        var store = new Mock<IImportCredentialClientStore>();
        store.Setup(s => s.Read(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredCredentialBlob(Guid.NewGuid(), "sealed", 0));
        Services.AddSingleton(store.Object);
        Services.AddSingleton<IUiNotificationHub>(new UiNotificationHub());
        Services.AddScoped<BrokenScorePreference>();
        Services.AddSingleton(_snackbar.Object);
    }

    private IRenderedComponent<ImportScoresWidget> Render()
    {
        return RenderComponent<ImportScoresWidget>(p => p
            .Add(c => c.Widget, new HomePageWidgetRecord(Guid.NewGuid(), "ImportScores", null, 0, "1x1", "{}", 1))
            .Add(c => c.EffectiveMix, MixEnum.Phoenix2));
    }

    [Fact]
    public async Task AnImportInsideTheCooldownSaysHowLongIsLeftAndOffersTheButtonAgain()
    {
        // Owner, 2026-09-27: five minutes between imports on a mix, told as an error toast.
        Mediator.Setup(m => m.Send(It.Is<StartOfficialImportCommand>(c => c.Mix == MixEnum.Phoenix2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportStartResult(ImportStartOutcome.CoolingDown, TimeSpan.FromSeconds(61)));
        var cut = Render();
        cut.WaitForAssertion(() => Assert.Contains("Import Scores", cut.Markup));

        await cut.FindAll("button")[0].ClickAsync(new MouseEventArgs());

        _snackbar.Verify(s => s.Add("You can import again in 2 min.", Severity.Error,
            It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);
        cut.WaitForAssertion(() => Assert.Contains("Import Scores", cut.Markup));
    }
}

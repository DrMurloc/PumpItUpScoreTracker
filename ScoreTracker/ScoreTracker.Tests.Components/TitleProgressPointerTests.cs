using System;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The line left where the Phoenix 2 folder-title track used to draw its bar: shown where the
///     page says the folder has title progress, and gone for good once the × has written its flag.
/// </summary>
public sealed class TitleProgressPointerTests : ComponentTestBase
{
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private string? _stored;

    public TitleProgressPointerTests()
    {
        _uiSettings.Setup(u => u.GetSetting(TitleProgressPointer.DismissedKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync(() => _stored);
        _uiSettings.Setup(u => u.SetSetting(TitleProgressPointer.DismissedKey, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string value, CancellationToken _) => _stored = value)
            .Returns(Task.CompletedTask);
        Services.AddSingleton(_uiSettings.Object);
    }

    private IRenderedComponent<TitleProgressPointer> RenderPointer(bool show) =>
        RenderComponent<TitleProgressPointer>(p => p.Add(c => c.Show, show));

    [Fact]
    public void PointsAtThePumbilityPageWhereTheFolderHasTitleProgress()
    {
        var cut = RenderPointer(show: true);

        cut.WaitForAssertion(() =>
        {
            var link = cut.Find("[data-testid=title-pointer] a");
            Assert.Equal("/Pumbility/Breakdown", link.GetAttribute("href"));
            Assert.Contains("Looking for title progress?", cut.Markup);
            Assert.Contains("See the PUMBILITY page", cut.Markup);
        });
    }

    [Fact]
    public void RendersNothingWhereTheFolderHasNoTitleProgress()
    {
        var cut = RenderPointer(show: false);

        cut.WaitForAssertion(() => _uiSettings.Verify(u =>
            u.GetSetting(TitleProgressPointer.DismissedKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()), Times.Once));
        Assert.Empty(cut.FindAll("[data-testid=title-pointer]"));
    }

    [Fact]
    public async Task TheCloseWritesTheFlagAndHidesTheLine()
    {
        var cut = RenderPointer(show: true);
        cut.WaitForAssertion(() => cut.Find("[data-testid=title-pointer-dismiss]"));

        await cut.Find("[data-testid=title-pointer-dismiss]").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=title-pointer]")));
        _uiSettings.Verify(u => u.SetSetting(TitleProgressPointer.DismissedKey, "true", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void StaysGoneOnceDismissed()
    {
        _stored = "true";

        var cut = RenderPointer(show: true);

        // The flag is read before anything paints, so a dismissed player never sees a flash of it.
        cut.WaitForAssertion(() => _uiSettings.Verify(u =>
            u.GetSetting(TitleProgressPointer.DismissedKey, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()), Times.Once));
        Assert.Empty(cut.FindAll("[data-testid=title-pointer]"));
    }
}

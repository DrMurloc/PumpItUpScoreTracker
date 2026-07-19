using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The export dialog: builds the CSV endpoint URL from the page's own filter
///     parameters, keeps column picks as a UiSetting, and hides My columns from
///     signed-out visitors.
/// </summary>
public sealed class ChartsExportDialogTests : ComponentTestBase
{
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public ChartsExportDialogTests()
    {
        _uiSettings.Setup(u => u.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        Services.AddSingleton(_uiSettings.Object);
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment RenderDialog(bool signedIn, IReadOnlyDictionary<string, object?>? filters = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartsExportDialog>(1);
            builder.AddAttribute(2, nameof(ChartsExportDialog.Visible), true);
            builder.AddAttribute(3, nameof(ChartsExportDialog.FilterParameters),
                filters ?? new Dictionary<string, object?> { ["LevelMin"] = 19, ["Type"] = "Double" });
            builder.AddAttribute(4, nameof(ChartsExportDialog.TotalCount), 212);
            builder.AddAttribute(5, nameof(ChartsExportDialog.Mix), MixEnum.Phoenix);
            builder.AddAttribute(6, nameof(ChartsExportDialog.SignedIn), signedIn);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void TheDownloadUrlCarriesTheFiltersColumnsAndShape()
    {
        var cut = RenderDialog(signedIn: false);

        cut.WaitForAssertion(() =>
        {
            var download = cut.FindAll("a").Single(a => a.TextContent.Contains("Download CSV"));
            var href = download.GetAttribute("href")!;
            Assert.Contains("/Charts/Export.csv", href);
            Assert.Contains("LevelMin=19", href);
            Assert.Contains("Type=Double", href);
            Assert.Contains("Shape=Grouped", href);
            Assert.Contains("Columns=Song", href);
        });
    }

    [Fact]
    public void ColumnPicksPersistAndReorderToRegistryOrder()
    {
        var cut = RenderDialog(signedIn: false);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".srp-badge-opt")));

        // Artist sits before NPS in the registry; picking it after the defaults must not
        // put it last in the file.
        cut.FindAll(".srp-badge-opt").Single(o => o.TextContent.Trim() == "Artist").Click();

        cut.WaitForAssertion(() =>
        {
            _uiSettings.Verify(u => u.SetSetting("Charts__Export__Columns",
                It.Is<string>(v => v.Contains("Artist")), It.IsAny<CancellationToken>()), Times.Once);
            var href = cut.FindAll("a").Single(a => a.TextContent.Contains("Download CSV")).GetAttribute("href")!;
            Assert.Contains(Uri.EscapeDataString("Song,Artist,Type"), href);
        });
    }

    [Fact]
    public void MyColumnsAreHiddenFromSignedOutVisitors()
    {
        var cut = RenderDialog(signedIn: false);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".srp-badge-opt")));
        Assert.DoesNotContain("My Phoenix score", cut.Markup);
    }

    [Fact]
    public void MyColumnsRenderForSignedInVisitors()
    {
        var cut = RenderDialog(signedIn: true);

        cut.WaitForAssertion(() => Assert.Contains("My Phoenix score", cut.Markup));
    }
}

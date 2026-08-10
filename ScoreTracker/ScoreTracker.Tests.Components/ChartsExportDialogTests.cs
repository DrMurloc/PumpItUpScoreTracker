using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private IRenderedFragment RenderDialog(bool signedIn, IReadOnlyDictionary<string, object?>? filters = null,
        MixEnum mix = MixEnum.Phoenix)
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
            builder.AddAttribute(5, nameof(ChartsExportDialog.Mix), mix);
            builder.AddAttribute(6, nameof(ChartsExportDialog.SignedIn), signedIn);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void TheDownloadUrlCarriesTheFiltersAndColumns()
    {
        var cut = RenderDialog(signedIn: false);

        cut.WaitForAssertion(() =>
        {
            var download = cut.FindAll("a").Single(a => a.TextContent.Contains("Download CSV"));
            var href = download.GetAttribute("href")!;
            Assert.Contains("/Charts/Export.csv", href);
            Assert.Contains("LevelMin=19", href);
            Assert.Contains("Type=Double", href);
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

    [Fact]
    public async Task AGroupHeaderCountsWhatItOffersAndTicksAllOfIt()
    {
        var cut = RenderDialog(signedIn: false);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".srp-export-all")));

        var chartGroup = cut.FindAll(".srp-export-all")[0];
        Assert.Equal("All 17", chartGroup.TextContent.Trim());

        await chartGroup.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var href = cut.FindAll("a").Single(a => a.TextContent.Contains("Download CSV")).GetAttribute("href")!;
            // Every chart column, still in registry order rather than press order.
            Assert.Contains(Uri.EscapeDataString("ChartId,Song,ChartUrl,Artist"), href);
            Assert.Contains(Uri.EscapeDataString("NPS,Badges"), href);
        });
    }

    [Fact]
    public async Task PressingAFullGroupAgainClearsIt()
    {
        var cut = RenderDialog(signedIn: false);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".srp-export-all")));

        var chartGroup = cut.FindAll(".srp-export-all")[0];
        await chartGroup.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.FindAll(".srp-export-all")[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Only that group empties — the Community default is untouched, so this is not a
        // reset button wearing a group's label.
        cut.WaitForAssertion(() =>
        {
            var selected = cut.FindAll(".srp-badge-opt-sel").Select(o => o.TextContent.Trim()).ToArray();
            Assert.Equal(new[] { "✓ Pass Difficulty" }, selected);
        });
    }

    /// <summary>
    ///     The group used to offer both families on every mix and return the wrong one blank.
    ///     A column the picker hides must also be undownloadable, which is why both sides
    ///     resolve through ChartExport.ColumnsFor.
    /// </summary>
    [Fact]
    public void MyColumnsAreScopedToTheSearchedMixScoringFamily()
    {
        var phoenix = RenderDialog(signedIn: true);
        phoenix.WaitForAssertion(() => Assert.Contains("My Phoenix score", phoenix.Markup));
        Assert.DoesNotContain("My legacy grade", phoenix.Markup);

        var legacy = RenderDialog(signedIn: true, mix: MixEnum.XX);
        legacy.WaitForAssertion(() => Assert.Contains("My legacy grade", legacy.Markup));
        Assert.DoesNotContain("My Phoenix score", legacy.Markup);
    }
}

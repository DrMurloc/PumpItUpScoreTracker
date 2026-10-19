using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     /UploadXXScores, the spreadsheet upload for XX and the older mixes. Its example used to sit inside the
///     Supported Formats dialog, where players did not find it (docs/design/import-scores-refresh.md, amended
///     2026-10-18).
/// </summary>
public sealed class UploadXXScoresPageTests : ComponentTestBase
{
    public UploadXXScoresPageTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        UiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.XX);
    }

    [Fact]
    public async Task TheExampleDownloadsFromTheCardWithoutOpeningSupportedFormats()
    {
        var helpers = JSInterop.SetupModule("./js/helpers.js");
        helpers.SetupVoid("downloadFileFromStream", _ => true).SetVoidResult();
        var cut = RenderComponent<UploadXXScores>();

        await cut.FindAll("button").First(b => b.TextContent.Contains("Download Example")).ClickAsync(new MouseEventArgs());

        Assert.Equal("scoresExample.csv", helpers.VerifyInvoke("downloadFileFromStream").Arguments[0]);
    }
}

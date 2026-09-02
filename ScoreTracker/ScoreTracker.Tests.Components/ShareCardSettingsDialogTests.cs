using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The download settings dialog: choices persist under the shared key, the exclusive trio
///     resolves last-press-wins, sub-toggles ride their parents, and the example is the real
///     renderer's bytes.
/// </summary>
public sealed class ShareCardSettingsDialogTests : ComponentTestBase
{
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private readonly List<string> _saved = new();

    public ShareCardSettingsDialogTests()
    {
        _uiSettings.Setup(u => u.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        _uiSettings.Setup(u => u.SetSetting(ShareCardOptions.SettingKey, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, string value, CancellationToken _) => _saved.Add(value))
            .Returns(Task.CompletedTask);
        Services.AddSingleton(_uiSettings.Object);
        Mediator.Setup(m => m.Send(It.IsAny<GetTierListShareCardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private static TierListShareCard SampleCard()
    {
        return new TierListShareCard("Singles 20", "sub", "stamp", "#3FA9F5", "#070B15", "#161E2C",
            "#E9EFF7", "#93A3B8", "https://example.invalid", null,
            new[] { new TierListShareCard.Row("Hard", "#FB8C00", Array.Empty<TierListShareCard.Tile>()) });
    }

    private IRenderedFragment RenderDialog(bool signedIn = true, bool offersPumbility = true,
        Func<ShareCardOptions, Task<TierListShareCard?>>? sampleBuilder = null,
        EventCallback<ShareCardDownloadRequest>? onDownload = null, string? stored = null)
    {
        if (stored != null)
            _uiSettings.Setup(u => u.GetSetting(ShareCardOptions.SettingKey, It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()))
                .ReturnsAsync(stored);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ShareCardSettingsDialog>(1);
            builder.AddAttribute(2, nameof(ShareCardSettingsDialog.Visible), true);
            builder.AddAttribute(3, nameof(ShareCardSettingsDialog.SignedIn), signedIn);
            builder.AddAttribute(4, nameof(ShareCardSettingsDialog.OffersPumbility), offersPumbility);
            builder.AddAttribute(5, nameof(ShareCardSettingsDialog.FileName), "TierList_test.png");
            if (sampleBuilder != null)
                builder.AddAttribute(6, nameof(ShareCardSettingsDialog.SampleBuilder), sampleBuilder);
            if (onDownload != null)
                builder.AddAttribute(7, nameof(ShareCardSettingsDialog.OnDownload), onDownload.Value);
            builder.CloseComponent();
        });
    }

    private static Task Flip(IRenderedFragment cut, string testId, bool value)
    {
        return cut.Find($"input[data-testid={testId}]").ChangeAsync(new ChangeEventArgs { Value = value });
    }

    [Fact]
    public async Task SubTogglesRideTheirParents()
    {
        var cut = RenderDialog();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-scores]")));
        Assert.Empty(cut.FindAll("[data-testid=sc-opt-broken]"));
        Assert.Empty(cut.FindAll("[data-testid=sc-opt-expected]"));

        await Flip(cut, "sc-opt-scores", true);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-broken]")));

        await Flip(cut, "sc-opt-pumbility", true);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-expected]")));
    }

    [Fact]
    public async Task EveryChangePersistsUnderTheSharedKey()
    {
        var cut = RenderDialog();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-names]")));

        await Flip(cut, "sc-opt-names", true);

        cut.WaitForAssertion(() =>
        {
            var last = ShareCardOptions.Parse(_saved.Last());
            Assert.True(last.SongNames);
            Assert.True(last.LetterGrades);
        });
    }

    [Fact]
    public async Task TheExclusiveTrioResolvesLastPressWins()
    {
        var cut = RenderDialog();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-colorgrade]")));

        await Flip(cut, "sc-opt-colorgrade", true);
        cut.WaitForAssertion(() => Assert.True(ShareCardOptions.Parse(_saved.Last()).ColorByLetterGrade));

        await Flip(cut, "sc-opt-btop50", true);
        cut.WaitForAssertion(() =>
        {
            var last = ShareCardOptions.Parse(_saved.Last());
            Assert.True(last.BoundaryTop50);
            Assert.False(last.ColorByLetterGrade);
        });

        await Flip(cut, "sc-opt-colorplate", true);
        cut.WaitForAssertion(() =>
        {
            var last = ShareCardOptions.Parse(_saved.Last());
            Assert.True(last.ColorByPlate);
            Assert.False(last.BoundaryTop50);
        });
    }

    [Fact]
    public async Task TurningPassOffTakesTheColorModesWithIt()
    {
        var cut = RenderDialog();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-colorgrade]")));

        await Flip(cut, "sc-opt-colorgrade", true);
        await Flip(cut, "sc-opt-bpass", false);

        cut.WaitForAssertion(() =>
        {
            var last = ShareCardOptions.Parse(_saved.Last());
            Assert.False(last.BoundaryPass);
            Assert.False(last.ColorByLetterGrade);
            Assert.True(cut.Find("input[data-testid=sc-opt-colorgrade]").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void SignedOutVisitorsSeeOnlyTheImpersonalOptions()
    {
        var cut = RenderDialog(signedIn: false);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-names]")));
        Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-skills]"));
        Assert.Empty(cut.FindAll("[data-testid=sc-opt-grades]"));
        Assert.Empty(cut.FindAll("[data-testid=sc-opt-bpass]"));
    }

    [Fact]
    public void FoldersWithoutPumbilityOfferNeitherTheChipNorTheGoldBoundary()
    {
        var cut = RenderDialog(offersPumbility: false);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-opt-scores]")));
        Assert.Empty(cut.FindAll("[data-testid=sc-opt-pumbility]"));
        Assert.Empty(cut.FindAll("[data-testid=sc-opt-btop50]"));
    }

    [Fact]
    public void TheExampleIsTheRenderersOwnBytes()
    {
        ShareCardOptions? sampled = null;
        var cut = RenderDialog(sampleBuilder: options =>
        {
            sampled = options;
            return Task.FromResult<TierListShareCard?>(SampleCard());
        });

        cut.WaitForAssertion(() =>
        {
            var img = cut.Find("[data-testid=sc-preview]");
            Assert.StartsWith("data:image/png;base64,AQID", img.GetAttribute("src"));
        });
        Assert.NotNull(sampled);
    }

    [Fact]
    public async Task DownloadHandsTheHostTheOptionsTheExampleShowed()
    {
        ShareCardDownloadRequest? request = null;
        var cut = RenderDialog(stored: "v1,SongNames,LetterGrades,BoundaryPass",
            onDownload: EventCallback.Factory.Create<ShareCardDownloadRequest>(this, r => request = r));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sharecard-download]")));

        await cut.Find("[data-testid=sharecard-download]").ClickAsync(new MouseEventArgs());

        Assert.NotNull(request);
        Assert.True(request!.Options.SongNames);
        Assert.False(request.Options.Plates);
        Assert.False(request.Token.IsCancellationRequested);
    }

    /// <summary>A host stub that holds the download open until its token fires, like the real loop would.</summary>
    private (IRenderedFragment Cut, Func<ShareCardDownloadRequest?> Request, Func<Task> Finished) RenderMidDownload()
    {
        ShareCardDownloadRequest? captured = null;
        var gate = new TaskCompletionSource();
        var cut = RenderDialog(onDownload: EventCallback.Factory.Create<ShareCardDownloadRequest>(this,
            async r =>
            {
                captured = r;
                r.Progress(new ShareCardFetchProgress(8, 61, false));
                using var registration = r.Token.Register(() => gate.TrySetResult());
                await gate.Task;
            }));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sharecard-download]")));
        var clickTask = cut.Find("[data-testid=sharecard-download]").ClickAsync(new MouseEventArgs());
        return (cut, () => captured, () => clickTask);
    }

    [Fact]
    public async Task EverythingDisablesTheMomentTheDownloadStarts()
    {
        var (cut, _, finished) = RenderMidDownload();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Fetching chart art — 8 of 61", cut.Find("[data-testid=sc-progress]").TextContent);
            Assert.True(cut.Find("input[data-testid=sc-opt-names]").HasAttribute("disabled"));
            Assert.True(cut.Find("input[data-testid=sc-opt-scores]").HasAttribute("disabled"));
            Assert.True(cut.Find("input[data-testid=sc-opt-btop50]").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid=sharecard-download]").HasAttribute("disabled"));
            Assert.Equal("Cancel", cut.Find("[data-testid=sharecard-cancel]").TextContent.Trim());
        });

        await cut.Find("[data-testid=sharecard-cancel]").ClickAsync(new MouseEventArgs());
        await finished();
    }

    [Fact]
    public async Task CancelStopsTheLoopAndReturnsTheDialogEditable()
    {
        var (cut, request, finished) = RenderMidDownload();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-progress]")));

        await cut.Find("[data-testid=sharecard-cancel]").ClickAsync(new MouseEventArgs());
        await finished();

        Assert.True(request()!.Token.IsCancellationRequested);
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid=sc-progress]"));
            Assert.False(cut.Find("input[data-testid=sc-opt-names]").HasAttribute("disabled"));
            Assert.Equal("Close", cut.Find("[data-testid=sharecard-cancel]").TextContent.Trim());
        });
    }

    [Fact]
    public async Task ClosingTheDialogCancelsTheDownload()
    {
        var (cut, request, finished) = RenderMidDownload();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=sc-progress]")));

        await cut.Find(".mud-overlay").ClickAsync(new MouseEventArgs());
        await finished();

        Assert.True(request()!.Token.IsCancellationRequested);
    }
}

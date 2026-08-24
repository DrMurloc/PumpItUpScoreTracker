using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MudBlazor.Services;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components.ChartComments;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The translated states of a comment row and the tab's Read-in picker. The resolution
///     itself is the vertical's (CommentDisplayResolutionTests); what is asserted here is that
///     Web renders exactly what it is handed — badge, transient flip, queued note, sticky pick —
///     and decides nothing.
/// </summary>
public sealed class CommentTranslationDisplayTests : TestContext
{
    private static readonly Guid Chart = Guid.Parse("cccccccc-0000-0000-0000-00000000000c");

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    private readonly User _viewer = new(Guid.NewGuid(), Name.From("ERRLENA"), true, null,
        new Uri("https://example.com/a.png"), Name.From("US"));

    public CommentTranslationDisplayTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_currentUser.Object);
        Services.AddSingleton<IStringLocalizer<App>>(new PassThroughLocalizer());
        Services.AddSingleton(_uiSettings.Object);

        _currentUser.Setup(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User).Returns(() => _viewer);
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommentScopesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommentScopeRecord(CommentAudience.Public, Name.From("Public")) });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommentConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentConsentRecord(false, false));
    }

    private static CommentRecord Translated(out CommentRecord original)
    {
        var originalBody = new[] { CommentSpan.OfText("드릴 구간이 전부예요") };
        var record = new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From("TUSA"),
            Name.From("KR"), new Uri("https://example.com/t.png"),
            new[] { CommentSpan.OfText("The drill section is the whole chart") }, 3, false, false, false,
            DateTimeOffset.UtcNow.AddDays(-1), null, null, Array.Empty<CommentRecord>(),
            new CommentTranslationRecord("ko", true, "en-US", originalBody, new[] { "en-US", "es-ES" }, false));
        original = record;

        return record;
    }

    private IRenderedComponent<CommentRow> RenderRow(CommentRecord comment)
    {
        return RenderComponent<CommentRow>(p => p.Add(r => r.Comment, comment).Add(r => r.CanInteract, true));
    }

    [Fact]
    public async Task ATranslatedBodyIsBadgedAndFlipsToTheOriginalTransiently()
    {
        var row = RenderRow(Translated(out _));

        Assert.Contains("The drill section", row.Markup);
        // The badge names the language in itself — the reader it matters to recognises it.
        Assert.Contains("한국어", row.Find("[data-testid^='translated-']").TextContent);

        await row.Find("[data-testid^='show-original-']").ClickAsync(new MouseEventArgs());

        Assert.Contains("드릴 구간이 전부예요", row.Markup);
        Assert.Contains("Show translation", row.Markup);

        await row.Find("[data-testid^='show-original-']").ClickAsync(new MouseEventArgs());

        Assert.Contains("The drill section", row.Markup);
    }

    [Fact]
    public void AQueuedCommentShowsTheAuthorsWordsWithTheBadge()
    {
        var pending = new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From("rafa_pump"),
            Name.From("BR"), new Uri("https://example.com/r.png"),
            new[] { CommentSpan.OfText("o BPM muda no final?") }, 1, false, false, false,
            DateTimeOffset.UtcNow, null, null, Array.Empty<CommentRecord>(),
            new CommentTranslationRecord(null, false, null, Array.Empty<CommentSpan>(),
                Array.Empty<string>(), true));

        var row = RenderRow(pending);

        Assert.Contains("o BPM muda no final?", row.Markup);
        Assert.NotNull(row.Find("[data-testid^='queued-']"));
        Assert.Empty(row.FindAll("[data-testid^='show-original-']"));
    }

    [Fact]
    public void AnUntranslatedRowCarriesNoBadgeAndNoFlip()
    {
        var plain = new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From("TUSA"),
            Name.From("KR"), new Uri("https://example.com/t.png"),
            new[] { CommentSpan.OfText("own words") }, 0, false, false, false,
            DateTimeOffset.UtcNow, null, null, Array.Empty<CommentRecord>(),
            new CommentTranslationRecord("en", false, null, Array.Empty<CommentSpan>(),
                new[] { "es-ES" }, false));

        var row = RenderRow(plain);

        Assert.Empty(row.FindAll("[data-testid^='translated-']"));
        Assert.Empty(row.FindAll("[data-testid^='queued-']"));
        Assert.Empty(row.FindAll("[data-testid^='show-original-']"));
    }

    /// <summary>
    ///     The menus render their items through a popover, so these tests put a
    ///     MudPopoverProvider in the tree — the same shape PersonalizedBreakdownPageTests uses.
    /// </summary>
    private IRenderedFragment RenderTabWithPopovers()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartCommentsTab>(1);
            builder.AddAttribute(2, nameof(ChartCommentsTab.ChartId), Chart);
            builder.AddAttribute(3, nameof(ChartCommentsTab.Active), true);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task PickingALocalizationIsRememberedAndPickingOriginalClearsIt()
    {
        var page = new CommentPageRecord(new[] { Translated(out _) }, 1, false, TranslationOffered: true);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        var tab = RenderTabWithPopovers();

        await tab.Find("[data-testid='cmt-read-in']").ClickAsync(new MouseEventArgs());
        await tab.Find("[data-testid='cmt-read-in-es-ES']").ClickAsync(new MouseEventArgs());
        _uiSettings.Verify(u => u.SetSetting("Comments__ReadIn", "es-ES", It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(m => m.Send(It.Is<GetChartCommentsQuery>(q => q.PreferredLocale == "es-ES"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // The clear is named Automatic, not Original: it restores the DEFAULT resolution,
        // which for a mapped-language reader still shows renderings. The author's words are
        // the per-comment Show original flip, never this menu.
        await tab.Find("[data-testid='cmt-read-in']").ClickAsync(new MouseEventArgs());
        await tab.Find("[data-testid='cmt-read-in-auto']").ClickAsync(new MouseEventArgs());
        _uiSettings.Verify(u => u.SetSetting("Comments__ReadIn", string.Empty, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnArmedPageOffersTheFullLanguageSetBeforeAnythingIsTranslated()
    {
        // The picker's whole audience is the reader whose language never renders — they state
        // the preference FIRST, and renderings meet it as they land. A picker built from what
        // the page already holds would never appear for exactly that reader.
        var untranslated = new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From("TUSA"),
            Name.From("KR"), new Uri("https://example.com/t.png"),
            new[] { CommentSpan.OfText("아직 번역 안 됨") }, 0, false, false, false,
            DateTimeOffset.UtcNow, null, null, Array.Empty<CommentRecord>(),
            new CommentTranslationRecord(null, false, null, Array.Empty<CommentSpan>(),
                Array.Empty<string>(), true));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentPageRecord(new[] { untranslated }, 1, false, TranslationOffered: true));

        var tab = RenderTabWithPopovers();

        await tab.Find("[data-testid='cmt-read-in']").ClickAsync(new MouseEventArgs());
        // All five fixed languages on offer, language-only labels, before any rendering exists.
        foreach (var locale in new[] { "en-US", "es-ES", "fr-FR", "ko-KR", "pt-BR" })
            Assert.NotNull(tab.Find($"[data-testid='cmt-read-in-{locale}']"));
        Assert.Contains("한국어", tab.Find("[data-testid='cmt-read-in-ko-KR']").TextContent);
        Assert.DoesNotContain("대한민국", tab.Markup);
    }

    [Fact]
    public void AParkedPageWithNoRenderingsOffersNoPicker()
    {
        var plain = new CommentRecord(Guid.NewGuid(), Chart, Guid.NewGuid(), Name.From("TUSA"),
            Name.From("KR"), new Uri("https://example.com/t.png"),
            new[] { CommentSpan.OfText("own words") }, 0, false, false, false,
            DateTimeOffset.UtcNow, null, null, Array.Empty<CommentRecord>(),
            new CommentTranslationRecord(null, false, null, Array.Empty<CommentSpan>(),
                Array.Empty<string>(), false));
        _mediator.Setup(m => m.Send(It.IsAny<GetChartCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentPageRecord(new[] { plain }, 1, false));

        var tab = RenderComponent<ChartCommentsTab>(p => p.Add(c => c.ChartId, Chart).Add(c => c.Active, true));

        Assert.DoesNotContain("cmt-read-in", tab.Markup);
    }

    [Fact]
    public async Task AReportStampsTheRenderingTheReporterWasReading()
    {
        var translated = Translated(out _);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentPageRecord(new[] { translated }, 1, false));
        var tab = RenderComponent<ChartCommentsTab>(p => p.Add(c => c.ChartId, Chart).Add(c => c.Active, true));
        var row = tab.FindComponent<CommentRow>();
        await tab.InvokeAsync(() => row.Instance.OnReport.InvokeAsync(translated));

        await tab.Find($"[data-testid='report-reason-{translated.Id}-HateOrDiscrimination']")
            .ChangeAsync(new ChangeEventArgs());
        await tab.Find($"[data-testid='report-go-{translated.Id}']").ClickAsync(new MouseEventArgs());

        // The reporter was reading the en-US rendering, and the report says so — a moderator
        // cannot judge a report against words the reporter never saw.
        _mediator.Verify(m => m.Send(It.Is<ReportCommentCommand>(c =>
                c.CommentId == translated.Id && c.ViewedLocale == "en-US"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AReportFiledAfterFlippingToTheOriginalStampsNoLocale()
    {
        var translated = Translated(out _);
        _mediator.Setup(m => m.Send(It.IsAny<GetChartCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentPageRecord(new[] { translated }, 1, false));
        var tab = RenderComponent<ChartCommentsTab>(p => p.Add(c => c.ChartId, Chart).Add(c => c.Active, true));
        await tab.Find($"[data-testid='show-original-{translated.Id}']").ClickAsync(new MouseEventArgs());

        var row = tab.FindComponent<CommentRow>();
        await tab.InvokeAsync(() => row.Instance.OnReport.InvokeAsync(translated));
        await tab.Find($"[data-testid='report-reason-{translated.Id}-SpamOrAdvertising']")
            .ChangeAsync(new ChangeEventArgs());
        await tab.Find($"[data-testid='report-go-{translated.Id}']").ClickAsync(new MouseEventArgs());

        _mediator.Verify(m => m.Send(It.Is<ReportCommentCommand>(c => c.ViewedLocale == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<App>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return Array.Empty<LocalizedString>();
        }
    }
}

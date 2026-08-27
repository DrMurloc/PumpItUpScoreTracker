using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using MudBlazor.Services;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Components.ChartComments;
using ScoreTracker.Web.Configuration;
using ScoreTracker.Web.Pages.Admin;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The two moderation queues. The community panel lives where the role machinery already is
///     and opens the dialog — its moderator is in the club, so the scope chip exists. The site
///     admin's page carries the reported words on the page instead, because an escalated comment
///     lives in a club they need not belong to.
/// </summary>
public sealed class ModerationQueueTests : TestContext
{
    private static readonly Guid Club = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");

    private static readonly Chart TestChart = new(Guid.Parse("cccccccc-0000-0000-0000-00000000000c"),
        MixEnum.Phoenix,
        new Song(Name.From("Baroque Virus"), SongType.Arcade, new Uri("https://example.com/bv.png"),
            TimeSpan.FromSeconds(90), Name.From("Artist"), null),
        ChartType.Single, DifficultyLevel.From(20), MixEnum.Phoenix, null, null);

    private static readonly User Admin = new(Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"),
        Name.From("DrMurloc"), true, null, new Uri("https://example.com/d.png"), Name.From("US"));

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();

    public ModerationQueueTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(o => o.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_currentUser.Object);
        Services.AddSingleton(_uiSettings.Object);
        Services.AddSingleton(Options.Create(new ChartCommentsConfiguration()));
        Services.AddSingleton<IStringLocalizer<App>>(new PassThroughLocalizer());
        Services.AddScoped<ChartScoringLevels>();

        _currentUser.Setup(u => u.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(u => u.User).Returns(Admin);
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MixEnum.Phoenix);
        _mediator.Setup(m => m.Send(
                It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { TestChart });
        _mediator.Setup(m => m.Send(It.IsAny<GetOpenCommentReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReportedCommentRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetSiteReportedCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SiteReportedCommentRecord>());

        // Last: this builds the service provider, so every registration above lands first.
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private static ReportedCommentRecord CommunityRow(Guid reportId) =>
        new(reportId, Guid.NewGuid(), TestChart.Id, Club, Name.From("Murloc Lab"), Guid.NewGuid(),
            Name.From("kimchi_stomper"), Name.From("ERRLENA"),
            CommentReportReason.HateOrDiscrimination, DateTimeOffset.UtcNow.AddHours(-2));

    private static SiteReportedCommentRecord SiteRow(Guid reportId, bool escalated) =>
        new(reportId, Guid.NewGuid(), TestChart.Id, escalated ? Club : null,
            escalated ? Name.From("Murloc Lab") : (Name?)null, Guid.NewGuid(),
            Name.From("bot_spam_9000"), Name.From("TUSA"),
            escalated ? CommentReportReason.HateOrDiscrimination : CommentReportReason.SpamOrAdvertising,
            DateTimeOffset.UtcNow.AddDays(-1),
            new[] { CommentSpan.OfText("the reported words") });

    // ----- the community panel -----------------------------------------------------------------

    [Fact]
    public void ThePanelRendersNothingAtAllWithoutOpenReports()
    {
        // An empty moderation panel is furniture; its absence is the empty state.
        var panel = RenderComponent<ReportedCommentsPanel>(p => p.Add(c => c.CommunityId, Club));

        Assert.Empty(panel.FindAll("[data-testid='reported-comments-panel']"));
    }

    [Fact]
    public async Task DismissSendsThePerQueueCommandAndReloads()
    {
        var reportId = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetOpenCommentReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommunityRow(reportId) });
        var panel = RenderComponent<ReportedCommentsPanel>(p => p.Add(c => c.CommunityId, Club));

        await panel.Find($"[data-testid='dismiss-{reportId}']").ClickAsync(new MouseEventArgs());

        // Community queue, not Site: this dismissal clears this panel and only this panel.
        _mediator.Verify(m => m.Send(It.Is<DismissCommentReportCommand>(c =>
                c.ReportId == reportId && c.Queue == CommentReportQueue.Community),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TheRowNamesTheClubTheChartAndBothSides()
    {
        var reportId = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetOpenCommentReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommunityRow(reportId) });

        var markup = RenderComponent<ReportedCommentsPanel>(p => p.Add(c => c.CommunityId, Club)).Markup;

        Assert.Contains("Baroque Virus", markup);
        Assert.Contains("kimchi_stomper", markup);
        Assert.Contains("ERRLENA", markup);
        Assert.Contains("Hate or discrimination", markup);
    }

    // ----- the site admin's page ---------------------------------------------------------------

    [Fact]
    public void TheSitePageCarriesTheWordsOnThePage()
    {
        var reportId = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetSiteReportedCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { SiteRow(reportId, escalated: true) });

        var page = RenderComponent<AdminComments>();

        // The read grant: the reported comment's body renders here, with the club named — and
        // Open is there too, into the thread with a read-only moderator chip for the club.
        var markup = page.Markup;
        Assert.Contains("the reported words", markup);
        Assert.Contains("Murloc Lab", markup);
        page.Find($"[data-testid='open-{reportId}']");
        page.Find($"[data-testid='remove-{reportId}']");
        page.Find($"[data-testid='dismiss-{reportId}']");
        page.Find($"[data-testid='lock-{reportId}']");
    }

    [Fact]
    public async Task OpenFromTheSitePageHandsTheDialogTheClubScopeAndTheComment()
    {
        // The dialog's own dependency set is ChartDetailsDialogTests' business; here the page's
        // job is to hand it the right parameters. The tab's moderator chip — the piece that makes
        // a foreign club readable — is pinned in ChartCommentsTabTests.
        var reportId = Guid.NewGuid();
        var row = SiteRow(reportId, escalated: true);
        _mediator.Setup(m => m.Send(It.IsAny<GetSiteReportedCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { row });
        var page = RenderComponent<AdminComments>();

        await page.Find($"[data-testid='open-{reportId}']").ClickAsync(new MouseEventArgs());

        var dialog = page.FindComponent<ChartDetailsDialog>();
        Assert.True(dialog.Instance.Visible);
        Assert.Equal(row.CommentId, dialog.Instance.FocusCommentId);
        Assert.Equal(CommentAudience.Community(Club), dialog.Instance.InitialCommentAudience);
        Assert.Equal(ChartDetailsDialog.DetailsTab.Comments, dialog.Instance.InitialTab);
    }

    [Fact]
    public void HellosSitInTheirOwnSectionUnderTheRealReports()
    {
        var real = SiteRow(Guid.NewGuid(), escalated: false);
        var hello = SiteRow(Guid.NewGuid(), escalated: false) with { Reason = CommentReportReason.JustWantAttention };
        _mediator.Setup(m => m.Send(It.IsAny<GetSiteReportedCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hello, real });

        var page = RenderComponent<AdminComments>();

        // Two headings, reports first; each card under its own.
        var reportsHeading = page.Find("[data-testid='admin-comments-reports-heading']");
        var hellosHeading = page.Find("[data-testid='admin-comments-hellos-heading']");
        var markup = page.Markup;
        Assert.True(markup.IndexOf("admin-comments-reports-heading", StringComparison.Ordinal)
                    < markup.IndexOf($"rcard-{real.ReportId}", StringComparison.Ordinal));
        Assert.True(markup.IndexOf($"rcard-{real.ReportId}", StringComparison.Ordinal)
                    < markup.IndexOf("admin-comments-hellos-heading", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("admin-comments-hellos-heading", StringComparison.Ordinal)
                    < markup.IndexOf($"rcard-{hello.ReportId}", StringComparison.Ordinal));
        Assert.Contains("I just want attention. Hi.", markup);
    }

    [Fact]
    public void APublicRowSaysPublicWhereTheClubWouldBe()
    {
        var reportId = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetSiteReportedCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { SiteRow(reportId, escalated: false) });

        Assert.Contains("Public", RenderComponent<AdminComments>().Markup);
    }

    [Fact]
    public async Task RemoveAndDismissSendTheirCommands()
    {
        var reportId = Guid.NewGuid();
        var row = SiteRow(reportId, escalated: true);
        _mediator.Setup(m => m.Send(It.IsAny<GetSiteReportedCommentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { row });
        var page = RenderComponent<AdminComments>();

        await page.Find($"[data-testid='remove-{reportId}']").ClickAsync(new MouseEventArgs());
        _mediator.Verify(m => m.Send(It.Is<RemoveCommentCommand>(c => c.CommentId == row.CommentId),
            It.IsAny<CancellationToken>()), Times.Once);

        await page.Find($"[data-testid='dismiss-{reportId}']").ClickAsync(new MouseEventArgs());
        _mediator.Verify(m => m.Send(It.Is<DismissCommentReportCommand>(c =>
                c.ReportId == reportId && c.Queue == CommentReportQueue.Site),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Returns the key, which is the English copy — enough to assert on.</summary>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ChartComments.Application;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Contracts.Commands;
using ScoreTracker.ChartComments.Contracts.Queries;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommentModerationSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ChartId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ClubId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly User Admin = new(Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713"),
        Name.From("DrMurloc"), true, null, new Uri("https://example.com/d.png"), Name.From("US"));

    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<ICommentReportRepository> _reports = new();
    private readonly Mock<ICommentRestrictionRepository> _restrictions = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserReader> _users = new();

    private readonly User _viewer = new(Guid.NewGuid(), Name.From("ERRLENA"), true, null,
        new Uri("https://example.com/a.png"), Name.From("US"));

    public CommentModerationSagaTests()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(() => _viewer);
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunityRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MyCommunityRoleRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMemberRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityMemberRoleRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityNamesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Name> { [ClubId] = Name.From("Murloc Lab") });
        _mediator.Setup(m => m.Send(It.IsAny<GetPublicToolsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PublicToolRecord>());
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        _reports.Setup(r => r.HasOpenFrom(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _restrictions.Setup(r => r.GetActive(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommentRestriction?)null);
    }

    private CommentModerationSaga Subject()
    {
        return new CommentModerationSaga(_comments.Object, _reports.Object, _restrictions.Object,
            _currentUser.Object, FakeDateTime.At(Now).Object, _mediator.Object, _users.Object,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private void StandingInClub(CommunityRole myRole, CommunityPermission myPermissions,
        params (Guid UserId, CommunityRole Role)[] members)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunityRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MyCommunityRoleRecord(ClubId, Name.From("Murloc Lab"), myRole, myPermissions)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMemberRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(members
                .Select(member => new CommunityMemberRoleRecord(member.UserId, member.Role))
                .Concat(new[] { new CommunityMemberRoleRecord(_viewer.Id, myRole) })
                .ToArray());
    }

    private Comment SavedComment(CommentAudience audience, Guid? author = null)
    {
        var comment = Comment.Post(ChartId, author ?? Guid.NewGuid(), audience, "the words", Now);
        _comments.Setup(c => c.GetById(comment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        return comment;
    }

    // ----- reporting ---------------------------------------------------------------------------

    [Fact]
    public async Task AReportIsFiledOpenOnBothDesks()
    {
        var comment = SavedComment(CommentAudience.Community(ClubId));

        await Subject().Handle(new ReportCommentCommand(comment.Id,
            CommentReportReason.HateOrDiscrimination), CancellationToken.None);

        _reports.Verify(r => r.Save(It.Is<CommentReport>(report =>
                report.CommentId == comment.Id && report.ReporterUserId == _viewer.Id &&
                report.IsOpenForCommunity && report.IsOpenForSite && report.RenderingLocale == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportingAgainWhileYoursIsOpenChangesNothing()
    {
        var comment = SavedComment(CommentAudience.Public);
        _reports.Setup(r => r.HasOpenFrom(comment.Id, _viewer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Subject().Handle(new ReportCommentCommand(comment.Id, CommentReportReason.OffTopic),
            CancellationToken.None);

        _reports.Verify(r => r.Save(It.IsAny<CommentReport>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task YourOwnCommentAndANoteAndAGhostAreUnreportable()
    {
        var mine = SavedComment(CommentAudience.Public, _viewer.Id);
        var note = SavedComment(CommentAudience.Private);
        var deleted = SavedComment(CommentAudience.Public);
        deleted.DeleteByAuthor(deleted.UserId, Now);

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new ReportCommentCommand(mine.Id, CommentReportReason.OffTopic), CancellationToken.None));
        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new ReportCommentCommand(note.Id, CommentReportReason.OffTopic), CancellationToken.None));
        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new ReportCommentCommand(deleted.Id, CommentReportReason.OffTopic), CancellationToken.None));
    }

    // ----- dismissal ---------------------------------------------------------------------------

    [Fact]
    public async Task ASiteDismissalNeedsTheSiteAdminAndClearsOnlyTheSiteSlot()
    {
        var report = CommentReport.File(Guid.NewGuid(), Guid.NewGuid(),
            CommentReportReason.HateOrDiscrimination, null, Now);
        _reports.Setup(r => r.GetById(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new DismissCommentReportCommand(report.Id, CommentReportQueue.Site), CancellationToken.None));

        _currentUser.SetupGet(c => c.User).Returns(Admin);
        await Subject().Handle(new DismissCommentReportCommand(report.Id, CommentReportQueue.Site),
            CancellationToken.None);

        _reports.Verify(r => r.Save(It.Is<CommentReport>(saved =>
                !saved.IsOpenForSite && saved.IsOpenForCommunity),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACommunityDismissalTakesRemovalStandingAndClearsOnlyTheirSlot()
    {
        var author = Guid.NewGuid();
        StandingInClub(CommunityRole.Admin, CommunityPermission.ModerateComments,
            (author, CommunityRole.Member));
        var comment = SavedComment(CommentAudience.Community(ClubId), author);
        var report = CommentReport.File(comment.Id, Guid.NewGuid(),
            CommentReportReason.HateOrDiscrimination, null, Now);
        _reports.Setup(r => r.GetById(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        await Subject().Handle(new DismissCommentReportCommand(report.Id, CommentReportQueue.Community),
            CancellationToken.None);

        // The escalated copy stays on the site admin's desk — escalation exists precisely for
        // the club that won't act.
        _reports.Verify(r => r.Save(It.Is<CommentReport>(saved =>
                !saved.IsOpenForCommunity && saved.IsOpenForSite),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BeingTheSiteAdminGrantsNothingOnTheCommunityQueue()
    {
        // Their desk is the Site queue; the club's desk follows the club's hierarchy.
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        var comment = SavedComment(CommentAudience.Community(ClubId));
        var report = CommentReport.File(comment.Id, Guid.NewGuid(),
            CommentReportReason.SpamOrAdvertising, null, Now);
        _reports.Setup(r => r.GetById(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new DismissCommentReportCommand(report.Id, CommentReportQueue.Community),
            CancellationToken.None));
    }

    [Fact]
    public async Task AnAdminCannotDismissAReportAgainstAFellowAdmin()
    {
        var author = Guid.NewGuid();
        StandingInClub(CommunityRole.Admin, CommunityPermission.ModerateComments,
            (author, CommunityRole.Admin));
        var comment = SavedComment(CommentAudience.Community(ClubId), author);
        var report = CommentReport.File(comment.Id, Guid.NewGuid(),
            CommentReportReason.OffTopic, null, Now);
        _reports.Setup(r => r.GetById(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new DismissCommentReportCommand(report.Id, CommentReportQueue.Community),
            CancellationToken.None));
    }

    // ----- mutes -------------------------------------------------------------------------------

    [Theory]
    [InlineData(CommunityRole.Admin, CommunityRole.Member, true)]
    [InlineData(CommunityRole.Admin, CommunityRole.Admin, false)]   // admins cannot mute admins
    [InlineData(CommunityRole.Creator, CommunityRole.Admin, true)]  // owners moderate admins
    [InlineData(CommunityRole.Creator, CommunityRole.Creator, false)]
    [InlineData(CommunityRole.Member, CommunityRole.Member, false)]
    public async Task MutingFollowsTheHierarchy(CommunityRole mine, CommunityRole theirs, bool allowed)
    {
        var target = Guid.NewGuid();
        StandingInClub(mine, CommunityPermission.ModerateComments, (target, theirs));

        if (allowed)
        {
            await Subject().Handle(new RestrictCommentingCommand(ClubId, target, "reason"),
                CancellationToken.None);
            _restrictions.Verify(r => r.Save(It.Is<CommentRestriction>(mute =>
                    mute.UserId == target && mute.CommunityId == ClubId &&
                    mute.RestrictedByUserId == _viewer.Id && mute.IsActive),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
                new RestrictCommentingCommand(ClubId, target, null), CancellationToken.None));
            _restrictions.Verify(r => r.Save(It.IsAny<CommentRestriction>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task NobodyMutesSomeoneWithNoSeat()
    {
        StandingInClub(CommunityRole.Creator, CommunityPermission.All);

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new RestrictCommentingCommand(ClubId, Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task MutingTwiceLandsOnOneMute()
    {
        var target = Guid.NewGuid();
        StandingInClub(CommunityRole.Creator, CommunityPermission.All, (target, CommunityRole.Member));
        _restrictions.Setup(r => r.GetActive(target, ClubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommentRestriction.Impose(target, ClubId, Guid.NewGuid(), null, Now));

        await Subject().Handle(new RestrictCommentingCommand(ClubId, target, null),
            CancellationToken.None);

        _restrictions.Verify(r => r.Save(It.IsAny<CommentRestriction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LiftingFollowsTheSameLadderAndStampsTheLift()
    {
        var target = Guid.NewGuid();
        StandingInClub(CommunityRole.Admin, CommunityPermission.ModerateComments,
            (target, CommunityRole.Member));
        _restrictions.Setup(r => r.GetActive(target, ClubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommentRestriction.Impose(target, ClubId, Guid.NewGuid(), null, Now.AddDays(-3)));

        await Subject().Handle(new LiftCommentRestrictionCommand(ClubId, target), CancellationToken.None);

        _restrictions.Verify(r => r.Save(It.Is<CommentRestriction>(mute =>
                !mute.IsActive && mute.LiftedAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- the community queue -----------------------------------------------------------------

    [Fact]
    public async Task TheQueueShowsOnlyReportsTheModeratorCouldActOn()
    {
        var member = Guid.NewGuid();
        var fellowAdmin = Guid.NewGuid();
        StandingInClub(CommunityRole.Admin, CommunityPermission.ModerateComments,
            (member, CommunityRole.Member), (fellowAdmin, CommunityRole.Admin));
        _reports.Setup(r => r.GetOpenForCommunities(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == ClubId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                QueueRow(Guid.NewGuid(), member),
                QueueRow(Guid.NewGuid(), fellowAdmin)
            });

        var rows = await Subject().Handle(new GetOpenCommentReportsQuery(), CancellationToken.None);

        // The report against the fellow admin waits for the creator rather than dangling in a
        // panel with no buttons.
        var row = Assert.Single(rows);
        Assert.Equal(member, row.ReportedUserId);
        Assert.Equal("Murloc Lab", row.CommunityName?.ToString());
    }

    [Fact]
    public async Task WithoutTheFlagThereIsNoQueueAtAll()
    {
        StandingInClub(CommunityRole.Admin, CommunityPermission.ManageUsers);

        Assert.Empty(await Subject().Handle(new GetOpenCommentReportsQuery(), CancellationToken.None));
        _reports.Verify(r => r.GetOpenForCommunities(It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- the site queue ----------------------------------------------------------------------

    [Fact]
    public async Task TheSiteQueueIsTheSiteAdminsAlone()
    {
        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new GetSiteReportedCommentsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task TheSiteQueueCarriesTheParsedWords()
    {
        _currentUser.SetupGet(c => c.User).Returns(Admin);
        _reports.Setup(r => r.GetOpenForSite(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { QueueRow(Guid.NewGuid(), Guid.NewGuid(), isPublic: true) });

        var rows = await Subject().Handle(new GetSiteReportedCommentsQuery(), CancellationToken.None);

        var row = Assert.Single(rows);
        // Spans, never a string — the page renders through the same components as the tab. A
        // null community name is a public comment.
        Assert.NotEmpty(row.Body);
        Assert.Null(row.CommunityName);
    }

    // ----- the restriction list ----------------------------------------------------------------

    [Fact]
    public async Task TheMuteListIsForModeratorsOfThatClubOnly()
    {
        StandingInClub(CommunityRole.Member, CommunityPermission.None);

        await Assert.ThrowsAsync<CommentNotAllowedException>(() => Subject().Handle(
            new GetCommunityCommentRestrictionsQuery(ClubId), CancellationToken.None));
    }

    [Fact]
    public async Task TheMuteListNamesBothSides()
    {
        var target = Guid.NewGuid();
        StandingInClub(CommunityRole.Creator, CommunityPermission.All, (target, CommunityRole.Member));
        _restrictions.Setup(r => r.GetActiveForCommunity(ClubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CommentRestriction.Impose(target, ClubId, _viewer.Id, "spam streak", Now)
            });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User(target, Name.From("kimchi_stomper"), true, null,
                    new Uri("https://example.com/k.png"), null),
                _viewer
            });

        var rows = await Subject().Handle(new GetCommunityCommentRestrictionsQuery(ClubId),
            CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("kimchi_stomper", row.UserName?.ToString());
        Assert.Equal("ERRLENA", row.RestrictedByName?.ToString());
        Assert.Equal("spam streak", row.Reason);
    }

    private static ReportQueueRow QueueRow(Guid reportId, Guid author, bool isPublic = false)
    {
        return new ReportQueueRow(reportId, Guid.NewGuid(), ChartId, author,
            isPublic ? null : ClubId, "the reported words", Guid.NewGuid(),
            CommentReportReason.HateOrDiscrimination, Now);
    }
}

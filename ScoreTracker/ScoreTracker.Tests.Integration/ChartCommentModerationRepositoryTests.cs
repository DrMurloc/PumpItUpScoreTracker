using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Migrations;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The moderation tables against a real migrated database: the permission backfill's exact
///     production SQL over seeded rows (the migration itself always runs against empty tables in
///     fixtures), the restriction lifecycle, and the two queue predicates — per-queue openness,
///     escalation routing, and deleted comments dropping out.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ChartCommentModerationRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.Parse("cccccccc-2222-2222-2222-22222222222c");
    private static readonly Guid Club = Guid.Parse("cccccccc-3333-3333-3333-33333333333c");
    private static readonly Guid OtherClub = Guid.Parse("cccccccc-4444-4444-4444-44444444444c");

    private readonly Guid _author = Guid.NewGuid();
    private readonly Guid _reporter = Guid.NewGuid();
    private readonly Guid _moderator = Guid.NewGuid();
    private readonly SqlServerFixture _fixture;

    public ChartCommentModerationRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFCommentRepository Comments => new(_fixture.DbContextFactory);
    private EFCommentReportRepository Reports => new(_fixture.DbContextFactory);
    private EFCommentRestrictionRepository Restrictions => new(_fixture.DbContextFactory);

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ----- the backfill ------------------------------------------------------------------------

    [Fact]
    public async Task TheBackfillMovesBothTrackedPopulationsInBothTablesAndNothingElse()
    {
        await using (var database = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            // Three memberships and three communities: the seed value, explicit All, and a
            // hand-picked subset that must come through untouched.
            database.Set<CommunityMembershipEntity>().AddRange(
                Membership(13), Membership(15), Membership(7));
            database.Set<CommunityEntity>().AddRange(
                Community("Seeded", 13), Community("AllKit", 15), Community("Picked", 7));
            await database.SaveChangesAsync();

            await database.Database.ExecuteSqlRawAsync(ChartCommentModeration.BackfillSql);
        }

        await using (var database = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            var memberships = await database.Set<CommunityMembershipEntity>()
                .OrderBy(m => m.Permissions).Select(m => m.Permissions).ToArrayAsync();
            var defaults = await database.Set<CommunityEntity>()
                .OrderBy(c => c.DefaultAdminPermissions).Select(c => c.DefaultAdminPermissions)
                .ToArrayAsync();

            Assert.Equal(new[] { 7, 29, 31 }, memberships);
            Assert.Equal(new[] { 7, 29, 31 }, defaults);
        }
    }

    // ----- restrictions ------------------------------------------------------------------------

    [Fact]
    public async Task AMuteRoundTripsAndOnlyActiveOnesAreRead()
    {
        var mute = CommentRestriction.Impose(_author, Club, _moderator, "spam streak", Now);
        var lifted = CommentRestriction.Impose(_author, OtherClub, _moderator, null, Now);
        lifted.Lift(Now.AddDays(1));
        await Restrictions.Save(mute);
        await Restrictions.Save(lifted);

        var active = await Restrictions.GetActive(_author, Club);
        Assert.NotNull(active);
        Assert.Equal("spam streak", active!.Reason);
        Assert.Equal(_moderator, active.RestrictedByUserId);

        // The lifted one is history, not state: invisible to every active read, still a row.
        Assert.Null(await Restrictions.GetActive(_author, OtherClub));
        Assert.Single(await Restrictions.GetActiveForUser(_author));
        Assert.Single(await Restrictions.GetActiveForCommunity(Club));
        Assert.Empty(await Restrictions.GetActiveForCommunity(OtherClub));
    }

    [Fact]
    public async Task LiftingPersistsThroughSave()
    {
        var mute = CommentRestriction.Impose(_author, Club, _moderator, null, Now);
        await Restrictions.Save(mute);

        var reloaded = await Restrictions.GetActive(_author, Club);
        reloaded!.Lift(Now.AddDays(2));
        await Restrictions.Save(reloaded);

        Assert.Null(await Restrictions.GetActive(_author, Club));
    }

    // ----- report queues -----------------------------------------------------------------------

    [Fact]
    public async Task TheCommunityQueueSeesItsOwnClubsOpenReportsOnly()
    {
        var clubComment = await SavedComment(CommentAudience.Community(Club));
        var otherClubComment = await SavedComment(CommentAudience.Community(OtherClub));
        var publicComment = await SavedComment(CommentAudience.Public);

        await Reports.Save(CommentReport.File(clubComment, _reporter,
            CommentReportReason.SpamOrAdvertising, null, Now));
        await Reports.Save(CommentReport.File(otherClubComment, _reporter,
            CommentReportReason.SpamOrAdvertising, null, Now));
        await Reports.Save(CommentReport.File(publicComment, _reporter,
            CommentReportReason.SpamOrAdvertising, null, Now));

        var rows = await Reports.GetOpenForCommunities(new[] { Club });

        var row = Assert.Single(rows);
        Assert.Equal(clubComment, row.CommentId);
        Assert.Equal(Club, row.CommunityId);
        Assert.Equal(Chart, row.ChartId);
        Assert.Equal(_author, row.AuthorUserId);
        Assert.Equal(CommentReportReason.SpamOrAdvertising, row.Reason);
    }

    [Fact]
    public async Task TheSiteQueueSeesPublicReportsAndEscalatedCommunityOnes()
    {
        var publicComment = await SavedComment(CommentAudience.Public);
        var hateInClub = await SavedComment(CommentAudience.Community(Club));
        var spamInClub = await SavedComment(CommentAudience.Community(Club));

        await Reports.Save(CommentReport.File(publicComment, _reporter,
            CommentReportReason.OffTopic, null, Now));
        await Reports.Save(CommentReport.File(hateInClub, _reporter,
            CommentReportReason.HateOrDiscrimination, null, Now));
        await Reports.Save(CommentReport.File(spamInClub, _reporter,
            CommentReportReason.SpamOrAdvertising, null, Now));

        var rows = await Reports.GetOpenForSite();

        // Public reports of any reason; community ones only when the reason escalates. The
        // spam-in-club report is the club's own business and never reaches this desk.
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.CommentId == publicComment);
        Assert.Contains(rows, r => r.CommentId == hateInClub);
        // The site queue carries the words — the open report is what grants the read.
        Assert.Equal("the reported words", Assert.Single(rows, r => r.CommentId == hateInClub).CommentText);
    }

    [Fact]
    public async Task DismissalIsPerQueueAndRemovalClearsBoth()
    {
        var hateInClub = await SavedComment(CommentAudience.Community(Club));
        var report = CommentReport.File(hateInClub, _reporter,
            CommentReportReason.HateOrDiscrimination, null, Now);
        await Reports.Save(report);

        // The club dismisses. Their panel empties; the escalated copy stays on the site desk —
        // escalation exists precisely for the club that won't act.
        report.ResolveForCommunity(_moderator, Now);
        await Reports.Save(report);

        Assert.Empty(await Reports.GetOpenForCommunities(new[] { Club }));
        Assert.Single(await Reports.GetOpenForSite());
        Assert.True(await Reports.HasOpenFrom(hateInClub, _reporter));

        // Removal closes everything that is still open.
        var reloaded = Assert.Single(await Reports.GetOpenForComment(hateInClub));
        reloaded.ResolveEverywhere(_moderator, Now.AddHours(1));
        await Reports.Save(reloaded);

        Assert.Empty(await Reports.GetOpenForSite());
        Assert.Empty(await Reports.GetOpenForComment(hateInClub));
        Assert.False(await Reports.HasOpenFrom(hateInClub, _reporter));
    }

    [Theory]
    [InlineData(CommentReportReason.OffTopic)]
    [InlineData(CommentReportReason.HateOrDiscrimination)]
    public async Task ASiteDismissalOnAPublicReportFreesTheReporter(CommentReportReason reason)
    {
        // A public report has no community desk — nothing can ever stamp that slot short of
        // removal. Counting it as "still open" after the site admin dismissed would leave the
        // reporter permanently unable to re-report, with the retry swallowed behind a success
        // toast. HasOpenFrom is routing-aware for exactly this.
        var publicComment = await SavedComment(CommentAudience.Public);
        var report = CommentReport.File(publicComment, _reporter, reason, null, Now);
        await Reports.Save(report);
        Assert.True(await Reports.HasOpenFrom(publicComment, _reporter));

        report.ResolveForSite(_moderator, Now);
        await Reports.Save(report);

        Assert.False(await Reports.HasOpenFrom(publicComment, _reporter));
    }

    [Fact]
    public async Task ACommunityDismissalFreesTheReporterUnlessTheReportEscalated()
    {
        // Non-escalating community report: the community desk is its only desk, so dismissal
        // there frees the reporter. An escalated one stays open until the site admin acts too.
        var spam = await SavedComment(CommentAudience.Community(Club));
        var hate = await SavedComment(CommentAudience.Community(Club));
        var spamReport = CommentReport.File(spam, _reporter, CommentReportReason.SpamOrAdvertising,
            null, Now);
        var hateReport = CommentReport.File(hate, _reporter, CommentReportReason.HateOrDiscrimination,
            null, Now);
        await Reports.Save(spamReport);
        await Reports.Save(hateReport);

        spamReport.ResolveForCommunity(_moderator, Now);
        hateReport.ResolveForCommunity(_moderator, Now);
        await Reports.Save(spamReport);
        await Reports.Save(hateReport);

        Assert.False(await Reports.HasOpenFrom(spam, _reporter));
        Assert.True(await Reports.HasOpenFrom(hate, _reporter));
    }

    [Fact]
    public async Task TwoRacingMutesLandOnOneRowAndALiftedMuteDoesNotBlockANewOne()
    {
        // The saga's check-then-insert is the polite path; the filtered unique index is the
        // guarantee. Filtered on LiftedAt IS NULL, because lifted rows are history and stack up.
        var first = CommentRestriction.Impose(_author, Club, _moderator, null, Now);
        await Restrictions.Save(first);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Restrictions.Save(CommentRestriction.Impose(_author, Club, Guid.NewGuid(), null, Now)));

        first.Lift(Now.AddDays(1));
        await Restrictions.Save(first);
        await Restrictions.Save(CommentRestriction.Impose(_author, Club, _moderator, "again", Now.AddDays(2)));

        var active = await Restrictions.GetActive(_author, Club);
        Assert.Equal("again", active!.Reason);
    }

    [Fact]
    public async Task AHelloReachesTheSiteDeskAloneAndItsOpennessIsTheSiteSlot()
    {
        // "I just want attention" on a community comment: never on the club's desk, on the site
        // desk regardless of escalation, and the reporter is freed by the SITE dismissal alone —
        // the community slot was never reachable, so it must not count as open.
        var clubComment = await SavedComment(CommentAudience.Community(Club));
        var hello = CommentReport.File(clubComment, _reporter, CommentReportReason.JustWantAttention,
            null, Now);
        await Reports.Save(hello);

        Assert.Empty(await Reports.GetOpenForCommunities(new[] { Club }));
        Assert.Single(await Reports.GetOpenForSite());
        Assert.True(await Reports.HasOpenFrom(clubComment, _reporter));

        hello.ResolveForSite(_moderator, Now);
        await Reports.Save(hello);

        Assert.Empty(await Reports.GetOpenForSite());
        Assert.False(await Reports.HasOpenFrom(clubComment, _reporter));
    }

    [Fact]
    public async Task ADeletedCommentsReportsAppearInNoQueue()
    {
        var reported = await SavedComment(CommentAudience.Community(Club));
        await Reports.Save(CommentReport.File(reported, _reporter,
            CommentReportReason.HateOrDiscrimination, null, Now));

        var comment = await Comments.GetById(reported);
        comment!.RemoveByModerator(_moderator, Now);
        await Comments.Save(comment);

        // Nothing to act on in either queue once the comment is gone — the rows stay for the
        // record, the queues just stop showing them.
        Assert.Empty(await Reports.GetOpenForCommunities(new[] { Club }));
        Assert.Empty(await Reports.GetOpenForSite());
    }

    private async Task<Guid> SavedComment(CommentAudience audience)
    {
        var comment = Comment.Post(Chart, _author, audience, "the reported words", Now);
        await Comments.Save(comment);
        return comment.Id;
    }

    private CommunityMembershipEntity Membership(int permissions)
    {
        return new CommunityMembershipEntity
        {
            Id = Guid.NewGuid(),
            CommunityId = Club,
            UserId = Guid.NewGuid(),
            Role = "Admin",
            Permissions = permissions
        };
    }

    private static CommunityEntity Community(string name, int defaultAdminPermissions)
    {
        return new CommunityEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwningUserId = Guid.NewGuid(),
            PrivacyType = "Public",
            DefaultAdminPermissions = defaultAdminPermissions
        };
    }
}

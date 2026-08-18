using System;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Domain.Exceptions;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CommentReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(3);
    private static readonly Guid Comment = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reporter = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ClubAdmin = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SiteAdmin = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static CommentReport Filed() =>
        CommentReport.File(Comment, Reporter, CommentReportReason.HateOrDiscrimination, null, Now);

    [Fact]
    public void ANewReportIsOpenInBothQueues()
    {
        var report = Filed();

        Assert.True(report.IsOpenForCommunity);
        Assert.True(report.IsOpenForSite);
        Assert.Equal(Reporter, report.ReporterUserId);
    }

    [Fact]
    public void FilingWithoutASignedInReporterThrows()
    {
        Assert.Throws<CommentNotAllowedException>(() =>
            CommentReport.File(Comment, Guid.Empty, CommentReportReason.OffTopic, null, Now));
    }

    [Fact]
    public void ACommunityDismissalClearsOnlyTheCommunitySlot()
    {
        var report = Filed();

        report.ResolveForCommunity(ClubAdmin, Now);

        // The per-queue rule: an escalated report stays on the site admin's desk even after the
        // club dismisses it — escalation exists precisely for the club that won't act.
        Assert.False(report.IsOpenForCommunity);
        Assert.True(report.IsOpenForSite);
        Assert.Equal(ClubAdmin, report.CommunityResolvedByUserId);
    }

    [Fact]
    public void ASiteDismissalClearsOnlyTheSiteSlot()
    {
        var report = Filed();

        report.ResolveForSite(SiteAdmin, Now);

        Assert.True(report.IsOpenForCommunity);
        Assert.False(report.IsOpenForSite);
        Assert.Equal(SiteAdmin, report.SiteResolvedByUserId);
    }

    [Fact]
    public void ResolutionIsIdempotentAndTheFirstStampStands()
    {
        var report = Filed();
        report.ResolveForCommunity(ClubAdmin, Now);

        report.ResolveForCommunity(SiteAdmin, Later);

        Assert.Equal(Now, report.CommunityResolvedAt);
        Assert.Equal(ClubAdmin, report.CommunityResolvedByUserId);
    }

    [Fact]
    public void RemovalResolvesEveryOpenSlotAndKeepsExistingStamps()
    {
        var report = Filed();
        report.ResolveForCommunity(ClubAdmin, Now);

        report.ResolveEverywhere(SiteAdmin, Later);

        Assert.False(report.IsOpenForCommunity);
        Assert.False(report.IsOpenForSite);
        Assert.Equal(ClubAdmin, report.CommunityResolvedByUserId); // the earlier dismissal survives
        Assert.Equal(SiteAdmin, report.SiteResolvedByUserId);
    }

    [Fact]
    public void ResolvingWithoutAModeratorThrows()
    {
        Assert.Throws<CommentNotAllowedException>(() => Filed().ResolveForSite(Guid.Empty, Now));
    }

    [Fact]
    public void StorageRoundTripKeepsTheStamps()
    {
        var report = CommentReport.FromStorage(new CommentReportState(
            Guid.NewGuid(), Comment, Reporter, CommentReportReason.SpamOrAdvertising, "es-ES", Now,
            CommunityResolvedAt: Later, CommunityResolvedByUserId: ClubAdmin));

        Assert.False(report.IsOpenForCommunity);
        Assert.True(report.IsOpenForSite);
        Assert.Equal("es-ES", report.RenderingLocale);
    }
}

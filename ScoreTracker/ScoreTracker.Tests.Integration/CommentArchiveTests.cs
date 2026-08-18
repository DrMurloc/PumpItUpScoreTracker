using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     What a community's deletion does to the comments it leaves behind, against a real migrated
///     database: the words move to the archive under the club's last known name, everything that
///     only meant something while the club lived goes — votes, revisions, reports open AND
///     resolved, mutes — bystanders are untouched, and a re-fired event finds nothing to move.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class CommentArchiveTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.Parse("cccccccc-5555-5555-5555-55555555555c");
    private static readonly Guid DoomedClub = Guid.Parse("cccccccc-6666-6666-6666-66666666666c");
    private static readonly Guid SurvivingClub = Guid.Parse("cccccccc-7777-7777-7777-77777777777c");

    private readonly Guid _author = Guid.NewGuid();
    private readonly Guid _replier = Guid.NewGuid();
    private readonly Guid _moderator = Guid.NewGuid();
    private readonly SqlServerFixture _fixture;

    public CommentArchiveTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFCommentRepository Comments => new(_fixture.DbContextFactory);
    private EFCommentReportRepository Reports => new(_fixture.DbContextFactory);
    private EFCommentRestrictionRepository Restrictions => new(_fixture.DbContextFactory);
    private EFCommentArchiveRepository Archive => new(_fixture.DbContextFactory);

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ADeletedClubsWholeFootprintMovesOrDiesAndBystandersStand()
    {
        // The doomed club: a root with a reply, a vote, a revision, one resolved and one open
        // report, and a mute.
        var root = Comment.Post(Chart, _author, CommentAudience.Community(DoomedClub), "the words", Now);
        await Comments.Save(root);
        var reply = Comment.Reply(root, _replier, "the answer", Now);
        await Comments.Save(reply);
        await Comments.AddVote(root.Id, _replier, Now);
        await Comments.WriteRevision(root.Id, "the earlier words", Now);
        var openReport = CommentReport.File(root.Id, _replier,
            CommentReportReason.HateOrDiscrimination, null, Now);
        var resolvedReport = CommentReport.File(reply.Id, _author,
            CommentReportReason.OffTopic, null, Now);
        resolvedReport.ResolveForCommunity(_moderator, Now);
        await Reports.Save(openReport);
        await Reports.Save(resolvedReport);
        await Restrictions.Save(CommentRestriction.Impose(_replier, DoomedClub, _moderator, null, Now));

        // Bystanders: another club's comment and mute, and a public comment.
        var survivor = Comment.Post(Chart, _author, CommentAudience.Community(SurvivingClub), "safe", Now);
        var publicComment = Comment.Post(Chart, _author, CommentAudience.Public, "also safe", Now);
        await Comments.Save(survivor);
        await Comments.Save(publicComment);
        await Restrictions.Save(CommentRestriction.Impose(_author, SurvivingClub, _moderator, null, Now));

        await Archive.ArchiveCommunity(DoomedClub, Name.From("Murloc Lab"), Now.AddHours(1));

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();

        // The words made it, name and timestamp attached, thread shape intact.
        var archived = await database.Set<CommentArchiveEntity>().ToArrayAsync();
        Assert.Equal(2, archived.Length);
        Assert.All(archived, row =>
        {
            Assert.Equal("Murloc Lab", row.CommunityName);
            Assert.Equal(Now.AddHours(1), row.ArchivedAt);
        });
        Assert.Contains(archived, row => row.Id == root.Id && row.Text == "the words");
        Assert.Contains(archived, row => row.Id == reply.Id && row.ParentCommentId == root.Id);

        // Everything that only meant something while the club lived is gone — including the
        // RESOLVED report, which would otherwise dangle at a row nobody can open.
        Assert.False(await database.Set<CommentEntity>().AnyAsync(c => c.CommunityId == DoomedClub));
        Assert.False(await database.Set<CommentVoteEntity>().AnyAsync(v => v.CommentId == root.Id));
        Assert.False(await database.Set<CommentRevisionEntity>().AnyAsync(r => r.CommentId == root.Id));
        Assert.False(await database.Set<CommentReportEntity>().AnyAsync());
        Assert.False(await database.Set<CommentRestrictionEntity>()
            .AnyAsync(r => r.CommunityId == DoomedClub));

        // Bystanders stand: the other club's comment and mute, and the public comment.
        Assert.True(await database.Set<CommentEntity>().AnyAsync(c => c.Id == survivor.Id));
        Assert.True(await database.Set<CommentEntity>().AnyAsync(c => c.Id == publicComment.Id));
        Assert.True(await database.Set<CommentRestrictionEntity>()
            .AnyAsync(r => r.CommunityId == SurvivingClub));
    }

    [Fact]
    public async Task ATombstoneRidesIntoTheArchiveAsIs()
    {
        // A purged author's stub keeps a thread open in life; in the archive it stays exactly
        // as anonymous as it was.
        var root = Comment.Post(Chart, _author, CommentAudience.Community(DoomedClub), "held open", Now);
        await Comments.Save(root);
        await Comments.Save(Comment.Reply(root, _replier, "the reply", Now));
        var loaded = await Comments.GetById(root.Id);
        loaded!.TombstoneForPurge(Now);
        await Comments.Save(loaded);

        await Archive.ArchiveCommunity(DoomedClub, Name.From("Murloc Lab"), Now.AddDays(1));

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        var stub = await database.Set<CommentArchiveEntity>().SingleAsync(row => row.Id == root.Id);
        Assert.Equal(Guid.Empty, stub.UserId);
        Assert.Equal(string.Empty, stub.Text);
    }

    [Fact]
    public async Task ARefiredEventFindsNothingToMove()
    {
        var root = Comment.Post(Chart, _author, CommentAudience.Community(DoomedClub), "once", Now);
        await Comments.Save(root);

        await Archive.ArchiveCommunity(DoomedClub, Name.From("Murloc Lab"), Now);
        await Archive.ArchiveCommunity(DoomedClub, Name.From("Murloc Lab"), Now.AddMinutes(5));

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        var row = Assert.Single(await database.Set<CommentArchiveEntity>().ToArrayAsync());
        // The first pass's stamp stands — the second found nothing to move.
        Assert.Equal(Now, row.ArchivedAt);
    }
}

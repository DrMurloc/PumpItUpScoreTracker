using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Comments are purged by hand, so <c>AccountPurgeTests</c>' generic sweep does not reach
///     them: it walks <c>UserOwned</c> manifests, and <c>CommentEntity</c> is deliberately not in
///     this vertical's (the manifest holds only the moderation tables, which have no orphan
///     problem).
///     <para>
///         The failure this guards is quiet. A blanket <c>DELETE … WHERE UserId</c> takes a root
///         out from under its replies, and every row-counting assertion still reads exactly right —
///         the account's rows really are gone. What breaks is a thread, later, in front of somebody
///         else.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ChartCommentPurgeTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.Parse("cccccccc-1111-1111-1111-11111111111c");

    private readonly Guid _bystander = Guid.NewGuid();
    private readonly SqlServerFixture _fixture;
    private readonly Guid _leaver = Guid.NewGuid();

    public ChartCommentPurgeTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFCommentRepository Comments => new(_fixture.DbContextFactory);

    private EFAccountPurgeRepository Purge =>
        new(_fixture.DbContextFactory, new FixedClock(Now));

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ARootSomebodyAnsweredSurvivesAsAnAnonymousStub()
    {
        var root = Comment.Post(Chart, _leaver, CommentAudience.Public, "the drill at 2:01", Now);
        await Comments.Save(root);
        var reply = Comment.Reply(root, _bystander, "agreed", Now);
        await Comments.Save(reply);
        await Comments.WriteRevision(root.Id, "an earlier wording", Now);

        await Purge.DeleteAllForUser(_leaver);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        var stub = await database.Set<CommentEntity>().SingleAsync(c => c.Id == root.Id);

        Assert.Equal(Guid.Empty, stub.UserId);
        Assert.Equal(string.Empty, stub.Text);
        Assert.NotNull(stub.DeletedAt);
        // The reply is still there, still attached, and still readable — which is the whole point
        // of not deleting the row it hangs from.
        Assert.True(await database.Set<CommentEntity>().AnyAsync(c => c.Id == reply.Id));
        // ⚠ The revision held the exact text the purge exists to remove, and carries no user key of
        // its own — nothing keyed on a user would ever have found it.
        Assert.False(await database.Set<CommentRevisionEntity>().AnyAsync(r => r.CommentId == root.Id));
    }

    [Fact]
    public async Task EverythingElseGoesOutright()
    {
        var lonely = Comment.Post(Chart, _leaver, CommentAudience.Public, "nobody answered this", Now);
        var note = Comment.Post(Chart, _leaver, CommentAudience.Private, "left foot", Now);
        var theirRoot = Comment.Post(Chart, _bystander, CommentAudience.Public, "somebody else's", Now);
        await Comments.Save(lonely);
        await Comments.Save(note);
        await Comments.Save(theirRoot);
        var myReply = Comment.Reply(theirRoot, _leaver, "my reply to them", Now);
        await Comments.Save(myReply);

        await Purge.DeleteAllForUser(_leaver);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.False(await database.Set<CommentEntity>().AnyAsync(c => c.UserId == _leaver));
        Assert.False(await database.Set<CommentEntity>().AnyAsync(c => c.Id == lonely.Id));
        Assert.False(await database.Set<CommentEntity>().AnyAsync(c => c.Id == note.Id));
        Assert.False(await database.Set<CommentEntity>().AnyAsync(c => c.Id == myReply.Id));
        // A stranger's root, which the leaver merely replied to, is untouched.
        Assert.True(await database.Set<CommentEntity>().AnyAsync(c => c.Id == theirRoot.Id));
    }

    [Fact]
    public async Task VotesGoBothWays()
    {
        var theirs = Comment.Post(Chart, _bystander, CommentAudience.Public, "theirs", Now);
        var mine = Comment.Post(Chart, _leaver, CommentAudience.Public, "mine", Now);
        await Comments.Save(theirs);
        await Comments.Save(mine);
        await Comments.AddVote(theirs.Id, _leaver, Now);
        await Comments.AddVote(mine.Id, _bystander, Now);

        await Purge.DeleteAllForUser(_leaver);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        // The vote they cast goes because it is theirs; the vote cast ON their comment goes
        // because the comment is going, and no purge keyed on the voter would ever reach it.
        Assert.False(await database.Set<CommentVoteEntity>().AnyAsync(v => v.UserId == _leaver));
        Assert.False(await database.Set<CommentVoteEntity>().AnyAsync(v => v.CommentId == mine.Id));
    }

    [Fact]
    public async Task PurgingATwiceLeavesTheSameResultAsPurgingItOnce()
    {
        // The purge event re-fires daily for a week, so a second pass must be a no-op rather than
        // a second helping of tombstones.
        var root = Comment.Post(Chart, _leaver, CommentAudience.Public, "answered", Now);
        await Comments.Save(root);
        await Comments.Save(Comment.Reply(root, _bystander, "yes", Now));

        await Purge.DeleteAllForUser(_leaver);
        await Purge.DeleteAllForUser(_leaver);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await database.Set<CommentEntity>().CountAsync(c => c.ChartId == Chart));
    }

    [Fact]
    public async Task PurgingNobodyMovesNothing()
    {
        var root = Comment.Post(Chart, _bystander, CommentAudience.Public, "still here", Now);
        await Comments.Save(root);
        await Comments.Save(Comment.Reply(root, _leaver, "so am I", Now));

        // Guid.Empty is what a tombstoned row is keyed to. A purge that took it at face value
        // would erase every stub on the site the first time it ran for a malformed message.
        await Purge.DeleteAllForUser(Guid.Empty);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await database.Set<CommentEntity>().CountAsync(c => c.ChartId == Chart));
    }

    [Fact]
    public async Task AStrangersPurgeTakesNothingOfYours()
    {
        var root = Comment.Post(Chart, _bystander, CommentAudience.Public, "mine", Now);
        await Comments.Save(root);
        await Comments.Save(Comment.Reply(root, _leaver, "and mine", Now));

        await Purge.DeleteAllForUser(Guid.NewGuid());

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await database.Set<CommentEntity>().CountAsync(c => c.ChartId == Chart));
    }

    [Fact]
    public async Task ReportsVanishWithTheCommentsTheyNameAndWithTheirReporter()
    {
        var lonely = Comment.Post(Chart, _leaver, CommentAudience.Public, "reported words", Now);
        var answered = Comment.Post(Chart, _leaver, CommentAudience.Public, "tombstoned words", Now);
        var theirs = Comment.Post(Chart, _bystander, CommentAudience.Public, "innocent", Now);
        await Comments.Save(lonely);
        await Comments.Save(answered);
        await Comments.Save(theirs);
        await Comments.Save(Comment.Reply(answered, _bystander, "keeps the thread", Now));

        var reports = new EFCommentReportRepository(_fixture.DbContextFactory);
        // Two against the leaver's comments — one that hard-deletes, one that tombstones — and
        // one the leaver FILED against a survivor, which the manifest reaches by ReporterUserId.
        await reports.Save(CommentReport.File(lonely.Id, _bystander,
            CommentReportReason.SpamOrAdvertising, null, Now));
        await reports.Save(CommentReport.File(answered.Id, _bystander,
            CommentReportReason.OffTopic, null, Now));
        await reports.Save(CommentReport.File(theirs.Id, _leaver,
            CommentReportReason.OffTopic, null, Now));

        await Purge.DeleteAllForUser(_leaver);

        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        // A stub with no words left is nothing a moderator can act on, and a report is its
        // reporter's row — so nothing survives: two went with the comments, one with its filer.
        Assert.False(await database.Set<CommentReportEntity>().AnyAsync());
        Assert.True(await database.Set<CommentEntity>().AnyAsync(c => c.Id == theirs.Id));
    }
}

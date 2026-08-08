using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     A leaked personal note is the worst thing this feature can do, and every suite above this
///     one runs on mocked ports — a mock records that the handler asked for the right audience,
///     never that the SQL honoured it. A repository whose WHERE clause is missing, or keyed to the
///     wrong column, passes all of them.
///     <para>
///         So: a decoy account holds a note and a club comment on the same chart as everybody else,
///         and no other reader's query may return either, under any scope or sort. This is the
///         shape <c>AccountPurgeTests</c> uses for the same reason.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ChartCommentAudienceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.Parse("cccccccc-0000-0000-0000-00000000000c");
    private static readonly Guid Club = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid OtherClub = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    private readonly Guid _decoy = Guid.NewGuid();
    private readonly SqlServerFixture _fixture;
    private readonly Guid _stranger = Guid.NewGuid();

    public ChartCommentAudienceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFCommentRepository Repository => new(_fixture.DbContextFactory);

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();

        // The decoy writes into every audience there is, on the chart everyone is reading.
        await Save(Comment.Post(Chart, _decoy, CommentAudience.Private, "left foot leads the 2:01 drill", Now));
        await Save(Comment.Post(Chart, _decoy, CommentAudience.Community(Club), "club only", Now));
        await Save(Comment.Post(Chart, _decoy, CommentAudience.Public, "public and fine", Now));
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(CommentSort.Top)]
    [InlineData(CommentSort.Newest)]
    public async Task AStrangersQueryNeverReturnsANoteOnAnyScopeOrSort(CommentSort sort)
    {
        foreach (var audience in new[]
                 {
                     CommentAudience.Public, CommentAudience.Private, CommentAudience.Community(Club),
                     CommentAudience.Community(OtherClub)
                 })
        {
            var rows = await Repository.GetForChart(Chart, audience, _stranger, sort, 20);

            Assert.DoesNotContain(rows, row => row.Text.Contains("left foot", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task TheNotesScopeReturnsOnlyYourOwn()
    {
        await Save(Comment.Post(Chart, _stranger, CommentAudience.Private, "my own note", Now));

        var mine = await Repository.GetForChart(Chart, CommentAudience.Private, _stranger, CommentSort.Top, 20);

        var row = Assert.Single(mine);
        Assert.Equal("my own note", row.Text);
        Assert.Equal(_stranger, row.UserId);
    }

    [Fact]
    public async Task ASignedOutReaderGetsNothingRatherThanEverybodysNotes()
    {
        // Guid.Empty is what a signed-out reader arrives as. If the predicate were written as
        // "UserId == viewerId" alone this would still be empty by luck; the explicit guard is
        // there so it stays empty when somebody seeds a row under Guid.Empty.
        Assert.Empty(await Repository.GetForChart(Chart, CommentAudience.Private, Guid.Empty,
            CommentSort.Top, 20));
    }

    [Fact]
    public async Task ACommunityScopeDoesNotBleedBetweenClubs()
    {
        var here = await Repository.GetForChart(Chart, CommentAudience.Community(Club), _stranger,
            CommentSort.Top, 20);
        var elsewhere = await Repository.GetForChart(Chart, CommentAudience.Community(OtherClub),
            _stranger, CommentSort.Top, 20);

        Assert.Single(here);
        Assert.Empty(elsewhere);
    }

    [Fact]
    public async Task CountRootsCountsWithTheSameGateTheReadUses()
    {
        // A count that ignored the audience would tell a stranger there are notes to open — which
        // is a smaller leak than the text and still a leak.
        Assert.Equal(0, await Repository.CountRoots(Chart, CommentAudience.Private, _stranger));
        Assert.Equal(1, await Repository.CountRoots(Chart, CommentAudience.Private, _decoy));
    }

    [Fact]
    public async Task RepliesComeBackWholeAndOrderedOldestFirst()
    {
        var root = Comment.Post(Chart, _stranger, CommentAudience.Public, "root", Now);
        await Save(root);
        await Save(Comment.Reply(root, _decoy, "first", Now));
        await Save(Comment.Reply(root, _stranger, "second", Now.AddMinutes(5)));

        var rows = await Repository.GetForChart(Chart, CommentAudience.Public, _stranger, CommentSort.Newest, 20);
        var replies = rows.Where(r => r.ParentCommentId == root.Id).ToArray();

        Assert.Equal(new[] { "first", "second" }, replies.Select(r => r.Text));
    }

    [Fact]
    public async Task TopOrdersByVotesAndPagingBoundsRootsWithoutTruncatingAThread()
    {
        var quiet = Comment.Post(Chart, _stranger, CommentAudience.Public, "quiet", Now);
        var loud = Comment.Post(Chart, _stranger, CommentAudience.Public, "loud", Now);
        await Save(quiet);
        await Save(loud);
        await Repository.AddVote(loud.Id, _decoy, Now);
        await Save(Comment.Reply(loud, _decoy, "answering the loud one", Now));

        var rows = await Repository.GetForChart(Chart, CommentAudience.Public, _stranger, CommentSort.Top, 1);

        var root = Assert.Single(rows.Where(r => r.ParentCommentId == null));
        Assert.Equal(loud.Id, root.Id);
        Assert.Equal(1, root.Votes);
        // One root asked for, and its reply still arrives: replies are not what the page bounds.
        Assert.Single(rows.Where(r => r.ParentCommentId == loud.Id));
    }

    [Fact]
    public async Task AVoteIsCountedOnceHoweverManyTimesItArrives()
    {
        var comment = Comment.Post(Chart, _stranger, CommentAudience.Public, "voted", Now);
        await Save(comment);

        await Repository.AddVote(comment.Id, _decoy, Now);
        await Repository.AddVote(comment.Id, _decoy, Now.AddSeconds(1));

        var row = Assert.Single((await Repository.GetForChart(Chart, CommentAudience.Public, _decoy,
            CommentSort.Top, 20)).Where(r => r.Id == comment.Id));
        Assert.Equal(1, row.Votes);
        Assert.True(row.ViewerVoted);
    }

    private Task Save(Comment comment)
    {
        return Repository.Save(comment);
    }
}

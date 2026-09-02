using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The second a comment points at, against the real column: stored with its three decimals,
///     read back on the row and on the aggregate, absent on a reply, unmoved by an edit
///     (docs/design/step-chart-comments D11).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ChartCommentAnchorTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.Parse("cccccccc-1111-1111-1111-11111111111c");

    private readonly Guid _author = Guid.NewGuid();
    private readonly SqlServerFixture _fixture;

    public ChartCommentAnchorTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFCommentRepository Repository => new(_fixture.DbContextFactory);

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TheSecondRoundTripsWithItsDecimals()
    {
        // Three decimals is what the step payload's row times carry; a column that rounded them
        // would put two people's "same quad" on different seconds and break the stack rule.
        var comment = Comment.Post(Chart, _author, CommentAudience.Public, "This quad is a bracket.", Now, 33.455m);
        await Repository.Save(comment);

        var loaded = await Repository.GetById(comment.Id);
        var row = Assert.Single(await Repository.GetForChart(Chart, CommentAudience.Public, _author,
            CommentSort.Top, 20));

        Assert.Equal(33.455m, loaded!.AnchorAt);
        Assert.Equal(33.455m, row.AnchorAt);
    }

    [Fact]
    public async Task AReplyStoresNoSecondAndAnEditKeepsTheRoots()
    {
        var root = Comment.Post(Chart, _author, CommentAudience.Public, "The drills start here.", Now, 29m);
        await Repository.Save(root);
        await Repository.Save(Comment.Reply(root, Guid.NewGuid(), "Right foot first.", Now));

        var loadedRoot = await Repository.GetById(root.Id);
        loadedRoot!.Edit(_author, "The drills start here — 16ths.", Now.AddMinutes(1));
        await Repository.Save(loadedRoot);

        var rows = await Repository.GetForChart(Chart, CommentAudience.Public, _author, CommentSort.Top, 20);
        Assert.Equal(29m, Assert.Single(rows, r => r.ParentCommentId == null).AnchorAt);
        Assert.Null(Assert.Single(rows, r => r.ParentCommentId == root.Id).AnchorAt);
    }

    [Fact]
    public async Task ACommentAboutTheWholeChartStoresNull()
    {
        var comment = Comment.Post(Chart, _author, CommentAudience.Public, "Best S21 in the folder.", Now);
        await Repository.Save(comment);

        Assert.Null((await Repository.GetById(comment.Id))!.AnchorAt);
    }

    /// <summary>
    ///     The decoy shape from ChartCommentAudienceTests, for the read the strip draws from: the
    ///     scope's living anchored roots in chart order with the reader's own note overlaid, the
    ///     reply behind its root — and not the decoy's note, the whole-chart comment, the other
    ///     scope's rows, or the deleted one, for any reader including a signed-out one.
    /// </summary>
    [Fact]
    public async Task TheMarksReadReturnsTheScopesAnchoredRootsPlusYourOwnNotesAndNobodyElses()
    {
        var decoy = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var club = Guid.Parse("aaaaaaaa-1111-1111-1111-11111111111a");
        var atTwentyNine = Comment.Post(Chart, decoy, CommentAudience.Public, "decoy public at 29", Now, 29m);
        await Repository.Save(atTwentyNine);
        await Repository.Save(Comment.Reply(atTwentyNine, stranger, "right foot first", Now));
        await Repository.Save(Comment.Post(Chart, decoy, CommentAudience.Public, "decoy public, whole chart", Now));
        await Repository.Save(Comment.Post(Chart, decoy, CommentAudience.Private, "decoy note at 10", Now, 10m));
        await Repository.Save(Comment.Post(Chart, decoy, CommentAudience.Community(club), "decoy club at 40", Now, 40m));
        await Repository.Save(Comment.Post(Chart, stranger, CommentAudience.Private, "my note at 66", Now, 66.2m));
        var deleted = Comment.Post(Chart, decoy, CommentAudience.Public, "deleted at 50", Now, 50m);
        deleted.DeleteByAuthor(decoy, Now);
        await Repository.Save(deleted);

        var asStranger = await Repository.GetAnchoredForChart(Chart, CommentAudience.Public, stranger);
        Assert.Equal(new[] { "decoy public at 29", "right foot first", "my note at 66" },
            asStranger.Select(r => r.Text));
        Assert.Equal(new[] { false, false, true }, asStranger.Select(r => r.IsNote));

        var signedOut = await Repository.GetAnchoredForChart(Chart, CommentAudience.Public, Guid.Empty);
        Assert.Equal(new[] { "decoy public at 29", "right foot first" }, signedOut.Select(r => r.Text));

        var notes = await Repository.GetAnchoredForChart(Chart, CommentAudience.Private, stranger);
        Assert.Equal("my note at 66", Assert.Single(notes).Text);

        var inClub = await Repository.GetAnchoredForChart(Chart, CommentAudience.Community(club), stranger);
        Assert.Equal(new[] { "decoy club at 40", "my note at 66" }, inClub.Select(r => r.Text));
    }
}

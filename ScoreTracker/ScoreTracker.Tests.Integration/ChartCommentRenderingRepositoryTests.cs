using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     StoreTranslation's two-part write — replace the rendering set, stamp the detected source
///     language — against the real unique key. The display rule reads both together, and a mock
///     cannot prove the transaction leaves no half-state behind.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ChartCommentRenderingRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Chart = Guid.Parse("cccccccc-2222-2222-2222-22222222222c");

    private readonly SqlServerFixture _fixture;

    public ChartCommentRenderingRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private EFCommentRepository Comments => new(_fixture.DbContextFactory);
    private EFCommentRenderingRepository Renderings => new(_fixture.DbContextFactory);

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ATranslationReplacesTheSetAndStampsTheSourceLanguage()
    {
        var comment = Comment.Post(Chart, Guid.NewGuid(), CommentAudience.Public, "안녕", Now);
        await Comments.Save(comment);
        await Renderings.StoreTranslation(comment.Id, "ko",
            new Dictionary<string, string> { ["es-ES"] = "hola", ["fr-FR"] = "salut" }, "first-pass", Now);

        // A re-translation lands a different set — the old French must not survive it.
        await Renderings.StoreTranslation(comment.Id, "ko",
            new Dictionary<string, string> { ["es-ES"] = "hola de nuevo" }, "second-pass", Now.AddDays(1));

        var rows = await Renderings.GetFor(new[] { comment.Id });
        var row = Assert.Single(rows);
        Assert.Equal(("es-ES", "hola de nuevo", "second-pass"), (row.Locale, row.Text, row.TranslatedBy));
        await using var database = await _fixture.DbContextFactory.CreateDbContextAsync();
        Assert.Equal("ko",
            (await database.Set<CommentEntity>().SingleAsync(c => c.Id == comment.Id)).SourceLanguage);
    }

    [Fact]
    public async Task DeleteForClearsOneCommentAndOnlyThatOne()
    {
        var mine = Comment.Post(Chart, Guid.NewGuid(), CommentAudience.Public, "mine", Now);
        var theirs = Comment.Post(Chart, Guid.NewGuid(), CommentAudience.Public, "theirs", Now);
        await Comments.Save(mine);
        await Comments.Save(theirs);
        await Renderings.StoreTranslation(mine.Id, "en",
            new Dictionary<string, string> { ["es-ES"] = "mío" }, "sonnet", Now);
        await Renderings.StoreTranslation(theirs.Id, "en",
            new Dictionary<string, string> { ["es-ES"] = "suyo" }, "sonnet", Now);

        await Renderings.DeleteFor(mine.Id);

        Assert.False(await Renderings.AnyFor(mine.Id));
        Assert.True(await Renderings.AnyFor(theirs.Id));
    }
}

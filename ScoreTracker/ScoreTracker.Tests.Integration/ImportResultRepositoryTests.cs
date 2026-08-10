using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Infrastructure;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The run rows behind restart recovery (docs/design/import-restart-recovery.md). These are
///     query-shape rules, so they are only really answerable against a real database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class ImportResultRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public ImportResultRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFImportResultRepository Repo() => new(_fixture.DbContextFactory);

    private async Task<Guid> OpenRun(Guid userId, DateTimeOffset startedAt, Guid? sessionId = null)
    {
        var repo = Repo();
        var id = await repo.Open(userId, MixEnum.Phoenix, ImportKind.Standard, null, startedAt);
        if (sessionId is { } s) await repo.AttachSession(id, s);
        return id;
    }

    [Fact]
    public async Task AnInterruptedRunIsOfferedOnceAndThenNeverAgain()
    {
        var userId = Guid.NewGuid();
        var repo = Repo();
        var runId = await OpenRun(userId, Now.AddMinutes(-20));
        await repo.MarkInterrupted(runId, Now.AddMinutes(-5));

        var first = await repo.GetUnacknowledgedInterrupted(userId);
        Assert.NotNull(first);
        Assert.Equal(runId, first!.Id);

        await repo.Acknowledge(runId, Now);

        Assert.Null(await repo.GetUnacknowledgedInterrupted(userId));
    }

    /// <summary>
    ///     ⚠ The notice says "import again". A run after the interrupted one means they already
    ///     did, and a run is only marked Interrupted at the NEXT boot — so a player who reimports
    ///     before that boot lands exactly here and would otherwise be told to do what they just
    ///     did.
    /// </summary>
    [Fact]
    public async Task AnInterruptedRunIsDroppedOnceTheyHaveImportedAgain()
    {
        var userId = Guid.NewGuid();
        var repo = Repo();
        var interrupted = await OpenRun(userId, Now.AddMinutes(-20));
        await repo.MarkInterrupted(interrupted, Now.AddMinutes(-15));

        var later = await OpenRun(userId, Now.AddMinutes(-10));
        await repo.Close(later, Now.AddMinutes(-9), ImportOutcome.Completed, 12);

        Assert.Null(await repo.GetUnacknowledgedInterrupted(userId));
    }

    /// <summary>A later run that ALSO died is still worth telling them about — it is the newest.</summary>
    [Fact]
    public async Task TheNewestInterruptedRunWinsWhenTwoInARowDied()
    {
        var userId = Guid.NewGuid();
        var repo = Repo();
        var older = await OpenRun(userId, Now.AddMinutes(-40));
        await repo.MarkInterrupted(older, Now.AddMinutes(-35));
        var newer = await OpenRun(userId, Now.AddMinutes(-20));
        await repo.MarkInterrupted(newer, Now.AddMinutes(-15));

        var offered = await repo.GetUnacknowledgedInterrupted(userId);

        Assert.Equal(newer, offered!.Id);
    }

    [Fact]
    public async Task AnotherPlayersInterruptedRunIsNeverOffered()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var repo = Repo();
        var runId = await OpenRun(theirs, Now.AddMinutes(-20));
        await repo.MarkInterrupted(runId, Now.AddMinutes(-15));

        Assert.Null(await repo.GetUnacknowledgedInterrupted(mine));
    }

    /// <summary>
    ///     MarkInterrupted only ever closes an OPEN row. A run that reported its own ending owns
    ///     that verdict — the recovery pass reaching it later must not overwrite it.
    /// </summary>
    [Fact]
    public async Task MarkInterruptedNeverOverwritesARunsOwnVerdict()
    {
        var userId = Guid.NewGuid();
        var repo = Repo();
        var runId = await OpenRun(userId, Now.AddMinutes(-20));
        await repo.Close(runId, Now.AddMinutes(-18), ImportOutcome.CredentialRejected, null);

        await repo.MarkInterrupted(runId, Now);

        var recent = await repo.GetRecent(userId, 10);
        Assert.Equal(ImportOutcome.CredentialRejected, recent.Single().Outcome);
    }

    [Fact]
    public async Task RunsAreFoundByTheSessionsTheRecoveryPassArrivesWith()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = await OpenRun(userId, Now.AddMinutes(-20), sessionId);
        await OpenRun(userId, Now.AddMinutes(-5), Guid.NewGuid());

        var found = await Repo().GetForSessions(new[] { sessionId });

        var run = Assert.Single(found);
        Assert.Equal(runId, run.Id);
        Assert.Equal(sessionId, run.SessionId);
        Assert.Null(run.FinishedAt);
    }

    [Fact]
    public async Task AskingForNoSessionsTouchesNothing()
    {
        Assert.Empty(await Repo().GetForSessions(Array.Empty<Guid>()));
    }

    /// <summary>
    ///     ⚠ Every outcome must survive being written. Outcomes are stored as their enum NAMES, and
    ///     the column was `nvarchar(16)` while `CredentialRejected` is 18 characters — so closing a
    ///     rejected-credential run threw a truncation error from inside the consumer's `finally`,
    ///     which nothing catches. The run stayed open, and a player who mistyped their password was
    ///     told their import "never reported back" instead of to check it.
    ///     <para>
    ///         Driven off the enum rather than a fixed list: a new member longer than the column is
    ///         the same bug again, and this is the only place that would notice.
    ///     </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryOutcome))]
    public async Task EveryOutcomeCanActuallyBeStored(ImportOutcome outcome)
    {
        var userId = Guid.NewGuid();
        var repo = Repo();
        var runId = await OpenRun(userId, Now.AddMinutes(-20));

        await repo.Close(runId, Now, outcome, 1);

        var stored = await repo.GetRecent(userId, 1);
        Assert.Equal(outcome, stored.Single().Outcome);
    }

    public static TheoryData<ImportOutcome> EveryOutcome()
    {
        var data = new TheoryData<ImportOutcome>();
        foreach (var outcome in Enum.GetValues<ImportOutcome>()) data.Add(outcome);
        return data;
    }
}

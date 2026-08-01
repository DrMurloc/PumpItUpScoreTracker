using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ScoreSessionQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid Restored = Guid.NewGuid();
    private static readonly Guid Removed = Guid.NewGuid();

    private readonly Mock<IScoreJournalRepository> _journal = new();
    private readonly Mock<IPhoenixRecordRepository> _records = new();
    private readonly Mock<IScoreSessionRepository> _sessions = new();

    private ScoreSessionQueryHandlers Build()
    {
        return new ScoreSessionQueryHandlers(_sessions.Object, _journal.Object, _records.Object);
    }

    private static ScoreJournalEntry Play(Guid chartId, Guid? sessionId, int minute)
    {
        return new ScoreJournalEntry(Now.AddMinutes(minute), ScoreJournalEntry.OfficialImportSource, UserId,
            chartId, (PhoenixScore)950_000, null, false, MixEnum.Phoenix, sessionId);
    }

    private static ScoreSessionRecord Session(Guid? owner = null)
    {
        return new ScoreSessionRecord(SessionId, owner ?? UserId, MixEnum.Phoenix,
            ScoreJournalEntry.OfficialImportSource, "SHIRONEKO", "2", Now, Now, 2, 1, 1);
    }

    [Fact]
    public async Task ThePreviewSplitsChartsIntoRestoredAndRemoved()
    {
        // The second count is the one that matters: a chart with no earlier play cannot be put
        // back, only removed.
        _sessions.Setup(s => s.Get(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(Session());
        _journal.Setup(j => j.GetSessionEntries(UserId, SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Play(Restored, SessionId, 10), Play(Removed, SessionId, 11) });
        _journal.Setup(j => j.GetChartHistories(UserId, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Play(Restored, SessionId, 10), Play(Restored, null, 1), Play(Removed, SessionId, 11)
            });

        var preview = await Build().Handle(new GetScoreSessionUndoPreviewQuery(UserId, SessionId),
            CancellationToken.None);

        Assert.Equal(1, preview!.ChartsRestored);
        Assert.Equal(1, preview.ChartsRemoved);
        Assert.Equal(2, preview.PlaysRemoved);
    }

    [Fact]
    public async Task ThePreviewRefusesSomebodyElsesSession()
    {
        _sessions.Setup(s => s.Get(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Session(Guid.NewGuid()));

        var preview = await Build().Handle(new GetScoreSessionUndoPreviewQuery(UserId, SessionId),
            CancellationToken.None);

        Assert.Null(preview);
    }

    [Fact]
    public async Task TheSessionListComesBackNewestFirstFromTheRepository()
    {
        _sessions.Setup(s => s.ListFor(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Session() });

        var sessions = await Build().Handle(new GetScoreSessionsQuery(UserId), CancellationToken.None);

        Assert.Single(sessions);
        Assert.Equal("SHIRONEKO", sessions[0].AccountTag);
    }

    [Fact]
    public async Task TheMixListIsWhateverTheLedgerReports()
    {
        // Every mix, not only the ones with scores — a picker that hides the empty ones is
        // indistinguishable from one that has forgotten they exist.
        _records.Setup(r => r.GetMixesWithScores(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MixScoreCount(MixEnum.Prime2, 6, false),
                new MixScoreCount(MixEnum.Phoenix, 3198, true)
            });

        var mixes = await Build().Handle(new GetMixesWithScoresQuery(UserId), CancellationToken.None);

        Assert.Equal(2, mixes.Count);
        Assert.Contains(mixes, m => m.Mix == MixEnum.Prime2 && !m.IsPrimary && m.ScoreCount == 6);
    }
}

public sealed class BeginScoreSessionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OpeningASessionRecordsWhoAndWhichCard()
    {
        // The tag and card are what make "I imported the wrong card" answerable, and they are
        // recorded once here rather than riding every score submission.
        var sessions = new Mock<IScoreSessionRepository>();
        var handler = new BeginScoreSessionHandler(sessions.Object, FakeDateTime.At(Now).Object);
        var userId = Guid.NewGuid();

        var id = await handler.Handle(new BeginScoreSessionCommand(userId, MixEnum.Phoenix,
            ScoreJournalEntry.OfficialImportSource, "SHIRONEKO", "2"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        sessions.Verify(s => s.Open(id, userId, MixEnum.Phoenix, ScoreJournalEntry.OfficialImportSource,
            "SHIRONEKO", "2", Now, It.IsAny<CancellationToken>()), Times.Once);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SessionFeedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();

    [Fact]
    public async Task NonPublicPlayersReadAsAnEmptyPage()
    {
        // Defense in depth behind the page's redirect-to-home.
        var ctx = new HandlerContext(isPublic: false);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        Assert.Equal(0, page.TotalGroups);
        Assert.Empty(page.Groups);
        ctx.Journal.Verify(j => j.GetSessionGroups(It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RowsClassifyAgainstTheChartsPriorJournalState()
    {
        // One chart's story: broken backfill -> first pass -> upscore -> legacy no-op.
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddDays(-3), 800000, isBroken: true, source: "backfill"),
            Entry(Now.AddDays(-2), 900000),
            Entry(Now.AddDays(-1), 950000),
            Entry(Now, 950000)
        };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var byTime = page.Groups.Single().Rows.OrderBy(r => r.OccurredAt).ToArray();
        Assert.Equal(ScoreEventClassification.Break, byTime[0].Classification);
        Assert.Equal(ScoreEventClassification.NewPass, byTime[1].Classification);
        Assert.Equal(ScoreEventClassification.Upscore, byTime[2].Classification);
        Assert.Equal(900000, byTime[2].PreviousBest);
        Assert.Equal(ScoreEventClassification.Played, byTime[3].Classification);
    }

    [Fact]
    public async Task PlateOnlyImprovementsClassifyAsUpscores()
    {
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddDays(-1), 950000, plate: PhoenixPlate.FairGame),
            Entry(Now, 950000, plate: PhoenixPlate.SuperbGame)
        };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var latest = page.Groups.Single().Rows.OrderBy(r => r.OccurredAt).Last();
        Assert.Equal(ScoreEventClassification.Upscore, latest.Classification);
    }

    [Fact]
    public async Task GroupsCarryDominantSourceAndTimeSpan()
    {
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddMinutes(-90), 900000, sessionId: sessionId, source: "officialImport"),
            Entry(Now, 950000, sessionId: sessionId, source: "officialImport")
        };
        ctx.GivenGroups(new JournalSessionRows(sessionId, null, MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var group = page.Groups.Single();
        Assert.Equal(sessionId, group.SessionId);
        Assert.Equal("officialImport", group.Source);
        Assert.Equal(Now.AddMinutes(-90), group.Start);
        Assert.Equal(Now, group.End);
        // Newest first within the group.
        Assert.Equal(Now, group.Rows.First().OccurredAt);
    }

    private static ScoreJournalEntry Entry(DateTimeOffset at, int score, bool isBroken = false,
        string source = "manual", PhoenixPlate? plate = PhoenixPlate.FairGame, Guid? sessionId = null)
    {
        return new ScoreJournalEntry(at, source, UserId, ChartId, score, plate, isBroken, MixEnum.Phoenix,
            sessionId);
    }

    private sealed class HandlerContext
    {
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public SessionFeedHandler Handler { get; }

        public HandlerContext(bool isPublic = true)
        {
            Users.Setup(u => u.GetUser(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserBuilder().WithId(UserId).WithIsPublic(isPublic).Build());
            Handler = new SessionFeedHandler(Journal.Object, Users.Object);
        }

        public void GivenGroups(params JournalSessionRows[] groups)
        {
            Journal.Setup(j => j.GetSessionGroups(UserId, It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((groups.Length, groups));
        }

        public void GivenHistories(IEnumerable<ScoreJournalEntry> rows)
        {
            Journal.Setup(j => j.GetChartHistories(UserId, It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(rows.ToArray());
        }
    }
}

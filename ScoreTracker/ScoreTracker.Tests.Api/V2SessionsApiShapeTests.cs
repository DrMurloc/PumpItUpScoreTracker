using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Controllers.Api.V2;
using ScoreTracker.Web.Dtos.ApiV2;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     <c>GET api/v2/players/{id}/sessions</c> — sessions as the site's Sessions page groups them: a
///     player's imports in one mix until eight hours pass without one, several imports to a night
///     (docs/design/session-breakdown.md §8.5).
/// </summary>
public sealed class V2SessionsApiShapeTests
{
    private static readonly Guid FirstImport = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SecondImport = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly Mock<IMediator> _mediator = new();
    private readonly PlayersController _controller;

    public V2SessionsApiShapeTests()
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(ApiTestData.PublicUser);
        _controller = new PlayersController(_mediator.Object, currentUser.Object, ApiTestClock.Accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Scheme = "https", Host = new HostString("piu"), Path = "/api/v2/players/me/sessions"
                    }
                }
            }
        };
    }

    [Fact]
    public async Task ANightOfImportsIsOneSessionListingEveryImport()
    {
        // The golden: a partner tool joins the sessionId on a journal entry or a score-push webhook
        // onto sessionIds, so both imports of the night have to be there.
        GivenSessions(new RecentSessionsPage.SessionGroup(SecondImport, new[] { FirstImport, SecondImport }, null,
            MixEnum.Phoenix, ScoreJournalEntry.OfficialImportSource, ApiTestData.Date1,
            ApiTestData.Date1.AddMinutes(40), new[]
            {
                Row(ApiTestData.Date1.AddMinutes(40), SecondImport),
                Row(ApiTestData.Date1, FirstImport)
            }));

        var result = await _controller.GetSessions("me", "Phoenix");

        JsonApproval.AssertWireShape("""
            {
              "data": [
                {
                  "sessionId": "77777777-7777-7777-7777-777777777777",
                  "sessionIds": [
                    "66666666-6666-6666-6666-666666666666",
                    "77777777-7777-7777-7777-777777777777"
                  ],
                  "mix": "Phoenix",
                  "source": "officialImport",
                  "startedAt": "2026-01-15T00:00:00+00:00",
                  "lastActivityAt": "2026-01-15T00:40:00+00:00",
                  "scoreCount": 2
                }
              ],
              "limit": 100,
              "total": null,
              "next": null
            }
            """, result);
    }

    [Fact]
    public async Task ActivityFromBeforeSessionCaptureHasNoIdsAtAll()
    {
        GivenSessions(new RecentSessionsPage.SessionGroup(null, Array.Empty<Guid>(),
            DateOnly.FromDateTime(ApiTestData.Date1.Date), MixEnum.Phoenix, "backfill", ApiTestData.Date1,
            ApiTestData.Date1, new[] { Row(ApiTestData.Date1, null) }));

        var result = await _controller.GetSessions("me", "Phoenix");

        var page = Assert.IsType<CursorPageDto<SessionDto>>(Assert.IsType<JsonResult>(result).Value);
        var session = Assert.Single(page.Data);
        Assert.Null(session.SessionId);
        Assert.Empty(session.SessionIds);
    }

    [Fact]
    public async Task TheListReachesPastAPlayersNewestFiftySessions()
    {
        // The read returns at most fifty sessions a page. The walk used to ask for 500 and step by
        // 500, so it stopped after the first fifty and reported no next page.
        GivenSessions(Enumerable.Range(0, 130)
            .Select(i => Session(MixEnum.Phoenix, ApiTestData.Date2.AddDays(-i)))
            .ToArray());

        var first = Page(await _controller.GetSessions("me", "Phoenix", limit: 100));
        var cursor = Uri.UnescapeDataString(first.Next!.Split("cursor=")[1]);
        var second = Page(await _controller.GetSessions("me", "Phoenix", cursor: cursor, limit: 100));

        Assert.Equal(100, first.Data.Length);
        Assert.Equal(30, second.Data.Length);
        Assert.Null(second.Next);
        Assert.Equal(130, first.Data.Concat(second.Data).Select(s => s.SessionId).Distinct().Count());
        _mediator.Verify(m => m.Send(
            It.Is<GetRecentSessionsQuery>(q => q.PageSize != GetRecentSessionsQuery.MaxPageSize),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheMixFilterHoldsAcrossTheReadsPages()
    {
        // The read pages every mix together, newest first; the endpoint answers for one.
        GivenSessions(Enumerable.Range(0, 80)
            .Select(i => Session(i % 2 == 0 ? MixEnum.Phoenix : MixEnum.Phoenix2, ApiTestData.Date2.AddDays(-i)))
            .ToArray());

        var page = Page(await _controller.GetSessions("me", "Phoenix2"));

        Assert.Equal(40, page.Data.Length);
        Assert.All(page.Data, s => Assert.Equal("Phoenix2", s.Mix));
        Assert.Null(page.Next);
    }

    private static CursorPageDto<SessionDto> Page(IActionResult result)
    {
        return Assert.IsType<CursorPageDto<SessionDto>>(Assert.IsType<JsonResult>(result).Value);
    }

    /// <summary>
    ///     Serves the sessions the way the real read does: newest first, paged, and clamped to
    ///     <see cref="GetRecentSessionsQuery.MaxPageSize" /> whatever the caller asks for.
    /// </summary>
    private void GivenSessions(params RecentSessionsPage.SessionGroup[] sessions)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetRecentSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<RecentSessionsPage> request, CancellationToken _) =>
            {
                var query = (GetRecentSessionsQuery)request;
                var size = Math.Clamp(query.PageSize, 1, GetRecentSessionsQuery.MaxPageSize);
                return new RecentSessionsPage(sessions.Length,
                    sessions.Skip((query.Page - 1) * size).Take(size).ToArray());
            });
    }

    private static RecentSessionsPage.SessionGroup Session(MixEnum mix, DateTimeOffset at)
    {
        var id = Guid.NewGuid();
        return new RecentSessionsPage.SessionGroup(id, new[] { id }, null, mix,
            ScoreJournalEntry.OfficialImportSource, at, at, new[] { Row(at, id) });
    }

    private static RecentSessionsPage.ScoreEventRecord Row(DateTimeOffset at, Guid? sessionId)
    {
        return new RecentSessionsPage.ScoreEventRecord(ApiTestData.ChartId1, at, 978210, "Marvelous Game", false,
            ScoreJournalEntry.OfficialImportSource, sessionId, ScoreEventClassification.Upscore, 950000);
    }
}

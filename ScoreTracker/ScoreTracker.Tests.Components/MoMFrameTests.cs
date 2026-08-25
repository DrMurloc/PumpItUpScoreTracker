using Bunit;
using Moq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Past Seasons dialog (march-of-murlocs.md §11.8): every season newest first with
///     its live chip, per-board session counts, the winner, and the viewer's own line —
///     each row a link to the season's dated URL.
/// </summary>
public sealed class MoMFrameTests : ComponentTestBase
{
    [Fact]
    public void DialogListsSeasonsWithWinnersAndTheViewersStanding()
    {
        var live = new MoMSeasonListing(
            new MoMSeasonRef(Guid.NewGuid(), "Summer 2026", 2026, 3),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero), true,
            new[]
            {
                new MoMBoardStanding(Guid.NewGuid(), MixEnum.Phoenix, ChartType.Double, 3,
                    "yimmythe42", 54118, 2, 51900, Guid.NewGuid())
            });
        var legacy = new MoMSeasonListing(
            new MoMSeasonRef(Guid.NewGuid(), "March of Murlocs 2", null, null),
            new DateTimeOffset(2024, 6, 8, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 8, 8, 0, 0, 0, TimeSpan.Zero), false,
            new[]
            {
                new MoMBoardStanding(Guid.NewGuid(), MixEnum.Phoenix, ChartType.Double, 17,
                    "FEFEMZ", 78691, null, null, null),
                new MoMBoardStanding(Guid.NewGuid(), MixEnum.Phoenix, ChartType.Single, 10,
                    "Franco", 65225, 1, 41782, Guid.NewGuid())
            });
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { live, legacy });

        var frame = RenderComponent<MoMFrame>();

        var rows = frame.FindAll(".mom-srow");
        Assert.Equal(2, rows.Count);
        Assert.Contains("running now", rows[0].TextContent);
        Assert.Contains("you were #2 — 51,900", rows[0].TextContent);
        Assert.Equal("/MarchOfMurlocs/2026/Summer", rows[0].GetAttribute("href"));
        // Legacy seasons route by hyphenated name; a sat-out board says so and a won one
        // says you won it.
        Assert.Equal("/MarchOfMurlocs/March-of-Murlocs-2", rows[1].GetAttribute("href"));
        Assert.Contains("you sat this one out", rows[1].TextContent);
        Assert.Contains("you won it", rows[1].TextContent);
        Assert.Contains("17 sessions", rows[1].TextContent);
    }
}

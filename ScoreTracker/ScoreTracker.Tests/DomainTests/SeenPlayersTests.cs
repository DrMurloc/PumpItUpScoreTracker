using System.Collections.Generic;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The debut seen-set's two halves: last week's players are seen without asking, and only the
///     rest of this week's players go to the history.
/// </summary>
public sealed class SeenPlayersTests
{
    private static PlacementRow Row(int playerId) => new(7, playerId, 1, 990_000);

    [Fact]
    public void OnlyPlayersMissingFromLastWeekNeedChecking()
    {
        var current = new[] { Row(1), Row(2), Row(2), Row(3) };

        var toCheck = SeenPlayers.ToCheck(current, new[] { Row(1), Row(4) });

        Assert.Equal(new[] { 2, 3 }, toCheck.OrderBy(id => id));
    }

    [Fact]
    public void WithNoPreviousWeekEveryPlayerNeedsChecking()
    {
        var toCheck = SeenPlayers.ToCheck(new[] { Row(1), Row(2) }, null);

        Assert.Equal(new[] { 1, 2 }, toCheck.OrderBy(id => id));
    }

    [Fact]
    public void TheSeenSetIsLastWeekPlusWhatEarlierWeeksHold()
    {
        var seen = SeenPlayers.Combine(new[] { Row(1) }, new HashSet<int> { 3 });

        Assert.Equal(new[] { 1, 3 }, seen.OrderBy(id => id));
    }
}

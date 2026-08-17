using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerVisibilityReaderTests
{
    private static readonly DateTimeOffset Added = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Mock<ICommunityReader> _communities = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Mock<IRivalRepository> _rivals = new();

    public PlayerVisibilityReaderTests()
    {
        _communities.Setup(c => c.GetUserCommunityMembers(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Name, IReadOnlyList<Guid>>());
        _rivals.Setup(r => r.GetRivalsOwnedBy(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalEdge>());
    }

    private PlayerVisibilityReader Reader() => new(_communities.Object, _rivals.Object);

    [Fact]
    public async Task AnonymousGetsTheEmptyAudienceWithoutReadingAnything()
    {
        var audience = await Reader().GetAudience(null, CancellationToken.None);

        Assert.Null(audience.ViewerId);
        Assert.Empty(audience.VisibleUserIds);
        _communities.Verify(c => c.GetUserCommunityMembers(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _rivals.Verify(r => r.GetRivalsOwnedBy(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EveryCommunityAMemberSharesWithYouIsNamedAndYourOwnSeatIsNot()
    {
        var mate = Guid.NewGuid();
        var other = Guid.NewGuid();
        _communities.Setup(c => c.GetUserCommunityMembers(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Name, IReadOnlyList<Guid>>
            {
                [Name.From("Seoul Pump")] = new[] { _me, mate, other },
                [Name.From("Doubles Club")] = new[] { _me, mate }
            });

        var audience = await Reader().GetAudience(_me, CancellationToken.None);

        Assert.Equal(new[] { Name.From("Doubles Club"), Name.From("Seoul Pump") },
            audience.SharedCommunitiesByMember[mate]);
        Assert.Equal(new[] { Name.From("Seoul Pump") }, audience.SharedCommunitiesByMember[other]);
        Assert.False(audience.SharedCommunitiesByMember.ContainsKey(_me));
        Assert.True(audience.VisibleUserIds.Contains(_me));
    }

    [Fact]
    public async Task OnlySiteRivalsJoinTheRivalBasisNeverATag()
    {
        var rival = Guid.NewGuid();
        _rivals.Setup(r => r.GetRivalsOwnedBy(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RivalEdge(Guid.NewGuid(), _me, rival, null, Added),
                new RivalEdge(Guid.NewGuid(), _me, null, "GHOST#0001", Added)
            });

        var audience = await Reader().GetAudience(_me, CancellationToken.None);

        Assert.Equal(new HashSet<Guid> { rival }, audience.RivalTargetIds);
        Assert.True(audience.Describe(rival, targetIsPublic: false).IsYourRival);
    }
}

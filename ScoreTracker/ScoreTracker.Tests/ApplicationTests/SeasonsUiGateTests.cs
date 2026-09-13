using Microsoft.Extensions.Options;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Seasons.Application;
using ScoreTracker.Seasons.Wiring;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SeasonsUiGateTests
{
    private static SeasonsUiGate Gate(bool flag, bool admin)
    {
        var user = new Mock<ICurrentUserAccessor>();
        user.SetupGet(u => u.IsLoggedInAsAdmin).Returns(admin);
        return new SeasonsUiGate(Options.Create(new SeasonsConfiguration { EnableUI = flag }), user.Object);
    }

    [Fact]
    public void ClosedByDefaultForEveryoneWhoIsNotAnAdmin()
    {
        Assert.False(Gate(flag: false, admin: false).IsOpen);
    }

    [Fact]
    public void AnAdminIsInsideWhateverTheFlagSays()
    {
        Assert.True(Gate(flag: false, admin: true).IsOpen);
    }

    [Fact]
    public void TheFlagOpensItForEverybody()
    {
        Assert.True(Gate(flag: true, admin: false).IsOpen);
    }
}

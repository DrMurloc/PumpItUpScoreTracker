using System.Reflection;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.Tests.E2E.Support;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     No E2E test can reach a live PiuGame site. Every <c>*Url</c> on
///     <see cref="PiuGameConfiguration" /> defaults to a production host, so a URL property the
///     fixture does not override sends this suite to the real site without anything failing.
///     Outside the E2E collection on purpose: it needs no Docker, no app and no browser.
/// </summary>
public sealed class PiuGameStubCoverageTests
{
    [Fact]
    public void TheFixtureStubsEveryPiuGameUrl()
    {
        var urls = typeof(PiuGameConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith("Url", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToArray();

        var unstubbed = urls.Except(E2EAppFixture.StubbedPiuGameUrls, StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(urls);
        Assert.True(unstubbed.Length == 0,
            "These PiuGame URLs keep their production default in the E2E suite, so a test can reach " +
            "the live site. Add each to E2EAppFixture.StubbedPiuGameUrls: " + string.Join(", ", unstubbed));
    }
}

using System;
using System.IO;
using System.Linq;
using ScoreTracker.CommunityTools.Infrastructure;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The one tool that keeps its own wire shape. PIU Tracker's integration predates this vertical
///     and 653 players were relying on it, so the seeded tool sends exactly what it always sent.
/// </summary>
public sealed class PiuTrackerSessionShapeTests
{
    /// <summary>
    ///     The id is written twice — in the seed migration's SQL and in the shape class — and only
    ///     one of them is compiled. If they drift, the tool exists but every delivery silently uses
    ///     the generic envelope, which PIU Tracker's endpoint does not understand. Nothing else would
    ///     catch that until a player noticed their sync had stopped.
    /// </summary>
    [Fact]
    public void TheWellKnownIdMatchesTheOneTheMigrationSeeds()
    {
        var migration = File.ReadAllText(FindMigration());

        Assert.Contains(PiuTrackerSessionShape.ToolId.ToString().ToUpperInvariant(), migration,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TUSA #1234", "https://piutracker.app:3002/api/sync/TUSA/1234")]
    // The space around the discriminator is not reliable in stored tags.
    [InlineData("TUSA#1234", "https://piutracker.app:3002/api/sync/TUSA/1234")]
    // Tags contain spaces; the path segments are escaped, not concatenated raw. AbsoluteUri rather
    // than ToString because ToString decodes the escapes back — what goes on the wire is
    // AbsoluteUri's PathAndQuery.
    [InlineData("DR MURLOC #7", "https://piutracker.app:3002/api/sync/DR%20MURLOC/7")]
    public void TheEndpointIsTheGameTagSplitIntoPathSegments(string gameTag, string expected)
    {
        var url = PiuTrackerSessionShape.Endpoint(new Uri("https://piutracker.app:3002/api/sync"), gameTag);

        Assert.Equal(expected, url.AbsoluteUri);
    }

    /// <summary>A tag with no discriminator must not throw — it produces an empty last segment.</summary>
    [Fact]
    public void ATagWithoutADiscriminatorStillProducesAUrl()
    {
        var url = PiuTrackerSessionShape.Endpoint(new Uri("https://piutracker.app:3002/api/sync"), "TUSA");

        Assert.Equal("https://piutracker.app:3002/api/sync/TUSA/", url.AbsoluteUri);
    }

    [Fact]
    public void TheBodyIsTheSessionAndNothingElse()
    {
        Assert.Equal("{\"sid\":\"abc123\"}", PiuTrackerSessionShape.Body("abc123"));
    }

    [Fact]
    public void EveryOtherToolGetsTheGenericEnvelope()
    {
        Assert.False(PiuTrackerSessionShape.Applies(Guid.NewGuid()));
        Assert.True(PiuTrackerSessionShape.Applies(PiuTrackerSessionShape.ToolId));
    }

    private static string FindMigration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "ScoreTracker.Data")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var match = Directory
            .GetFiles(Path.Combine(directory!.FullName, "ScoreTracker.Data", "Migrations"),
                "*_SeedPiuTrackerTool.cs")
            .SingleOrDefault();

        Assert.True(match is not null, "The SeedPiuTrackerTool migration is gone — the tool is not seeded.");
        return match!;
    }
}

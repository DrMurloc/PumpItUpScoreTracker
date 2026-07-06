using System.Linq;
using ScoreTracker.Data.Clients;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordMessageSplitterTests
{
    [Fact]
    public void MessagesWithinTheLimitPassThroughUntouched()
    {
        var message = "short message\nwith lines";

        var parts = DiscordMessageSplitter.Split(message);

        Assert.Equal(new[] { message }, parts);
    }

    [Fact]
    public void OversizedMessagesSplitOnLineBoundariesWithNoPartOverTheLimit()
    {
        var lines = Enumerable.Range(0, 60).Select(i => $"line {i:00} " + new string('x', 50)).ToArray();
        var message = string.Join("\n", lines);
        Assert.True(message.Length > DiscordMessageSplitter.MaxContentLength);

        var parts = DiscordMessageSplitter.Split(message);

        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.Length <= DiscordMessageSplitter.MaxContentLength));
        // No line is torn apart and none are lost.
        Assert.Equal(lines, parts.SelectMany(p => p.Split('\n')).ToArray());
    }

    [Fact]
    public void PathologicalSingleLineIsHardWrappedRatherThanDropped()
    {
        var message = new string('x', 4500);

        var parts = DiscordMessageSplitter.Split(message);

        Assert.All(parts, p => Assert.True(p.Length <= DiscordMessageSplitter.MaxContentLength));
        Assert.Equal(message, string.Concat(parts));
    }
}

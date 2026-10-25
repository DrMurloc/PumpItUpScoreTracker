using System;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     A failed run's stored error: the exceptions first, then only this codebase's frames, so the
///     line that names what failed survives the column's 2,000-character cut.
/// </summary>
public sealed class FailureTextTests
{
    private static Exception Caught(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            return e;
        }

        throw new InvalidOperationException("nothing was thrown");
    }

    private static void ParseThenWrap()
    {
        try
        {
            int.Parse("not a number");
        }
        catch (FormatException inner)
        {
            throw new InvalidOperationException("the week could not be worked out", inner);
        }
    }

    [Fact]
    public void EveryExceptionIsNamedBeforeAnyFrame()
    {
        var lines = FailureText.Of(Caught(ParseThenWrap)).Split('\n');

        Assert.Equal("System.InvalidOperationException: the week could not be worked out", lines[0].TrimEnd());
        Assert.StartsWith("System.FormatException: ", lines[1]);
    }

    [Fact]
    public void OnlyThisCodebasesFramesAreKept()
    {
        var frames = FailureText.Of(Caught(ParseThenWrap)).Split('\n').Skip(2).ToArray();

        Assert.Contains(frames, f => f.Contains("FailureTextTests.ParseThenWrap", StringComparison.Ordinal));
        Assert.DoesNotContain(frames, f => f.TrimStart().StartsWith("at System.", StringComparison.Ordinal));
    }
}

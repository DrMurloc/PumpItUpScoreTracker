using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ScoreTracker.CompositionRoot;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     No ambient clock in Application or Domain code (CLAUDE.md, Test data: the clock seam).
///     <c>DateTime.Now</c>, <c>DateTimeOffset.Now</c> and any <c>.UtcNow</c> read a time no test
///     can set, so a window that opens or closes on them can only be tested by waiting for it. The
///     time comes from <c>IDateTimeOffsetAccessor</c>, which a test replaces with
///     <c>FakeDateTime.At(...)</c>.
///     <para>
///         Scope: the production code of <c>ScoreTracker.Application</c> and <c>ScoreTracker.Domain</c>,
///         and the <c>Domain/</c> and <c>Application/</c> folders of every vertical. Infrastructure and
///         Web are not covered. Comment lines (<c>//</c>, <c>///</c>) are skipped. No allowance: the
///         rule landed with nothing to forgive.
///     </para>
/// </summary>
public sealed class NoAmbientClockTests
{
    private static readonly Regex AmbientClock =
        new(@"\b(?:DateTime|DateTimeOffset)\.Now\b|\.UtcNow\b", RegexOptions.Compiled);

    [Fact]
    public void ApplicationAndDomainCodeReadsTheClockThroughItsPort()
    {
        var root = SourceTree.SolutionRoot;
        var violations = ScannedFiles(root)
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(l => IsAmbientClockRead(l.Line))
                .Select(l => $"{Relative(root, file)}:{l.Number}: {l.Line.Trim()}"))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0,
            "These lines read the ambient clock. Inject IDateTimeOffsetAccessor and read its Now " +
            "(or take the time as an argument), so a test can set it with FakeDateTime.At:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>A scan that reaches nothing passes forever; this says the walk still finds the code.</summary>
    [Fact]
    public void TheScanReachesApplicationDomainAndVerticalCode()
    {
        var root = SourceTree.SolutionRoot;
        var scanned = ScannedFiles(root).Select(f => Relative(root, f)).ToHashSet(StringComparer.Ordinal);
        var expected = new[]
        {
            "ScoreTracker.Domain/Models/TournamentConfiguration.cs",
            "ScoreTracker.Application/Handlers/SaveChartToListHandler.cs",
            "ScoreTracker.ScoreLedger/Application/UpdatePhoenixRecordHandler.cs",
            "ScoreTracker.ChartComments/Domain/Comment.cs"
        };

        var missing = expected.Where(e => !scanned.Contains(e)).ToArray();
        Assert.True(missing.Length == 0,
            "The clock scan no longer reaches: " + string.Join(", ", missing) +
            " - repoint the expectation at wherever these moved, or the scan is guarding an empty set");
    }

    [Theory]
    [InlineData("var now = DateTimeOffset.Now;", true)]
    [InlineData("var now = DateTime.Now;", true)]
    [InlineData("if (expires < DateTime.UtcNow) return;", true)]
    [InlineData("var at = DateTimeOffset.UtcNow.AddDays(1);", true)]
    [InlineData("    // DateTimeOffset.Now is what this used to read", false)]
    [InlineData("    /// <summary>Never DateTime.UtcNow here.</summary>", false)]
    [InlineData("public DateTimeOffset Now { get; }", false)]
    [InlineData("DateTimeOffset at = DateTimeOffset.MinValue;", false)]
    [InlineData("var now = _dateTimeOffset.Now;", false)]
    [InlineData("var now = TimeProvider.System.GetUtcNow();", false)]
    public void TheScannerFlagsAmbientClockReadsAndNothingElse(string line, bool expected)
    {
        Assert.Equal(expected, IsAmbientClockRead(line));
    }

    private static bool IsAmbientClockRead(string line)
    {
        return !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && AmbientClock.IsMatch(line);
    }

    private static IEnumerable<string> ScannedFiles(string root)
    {
        var folders = new[] { "ScoreTracker.Application", "ScoreTracker.Domain" }
            .Select(p => Path.Combine(root, p))
            .Concat(VerticalAssemblies.All()
                .Select(a => a.GetName().Name!)
                .SelectMany(v => new[] { Path.Combine(root, v, "Domain"), Path.Combine(root, v, "Application") })
                // Not every vertical has both folders; the reach fact above guards the walk.
                .Where(Directory.Exists));

        return folders
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(f =>
            {
                var segments = Relative(root, f).Split('/');
                return !segments.Contains("bin") && !segments.Contains("obj");
            });
    }

    private static string Relative(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace('\\', '/');
    }
}

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Compares the resx against the compiled satellite. The resx is not evidence: a key can be
///     present in the file and absent from the assembly, which is exactly how a locale silently
///     renders English while the file looks perfect.
/// </summary>
public sealed class QualifierLocalizationTests
{
    private readonly ITestOutputHelper _output;

    public QualifierLocalizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? ResourcesDirectory()
    {
        // Two roots: the assembly location for a normal run, and the working directory for a
        // run whose output was relocated (a locked bin forces -p:BaseOutputPath).
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "ScoreTracker", "ScoreTracker", "Resources");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        return null;
    }

    [Fact]
    public void EveryKeyInTheResxSurvivesIntoTheCompiledResources()
    {
        var resources = ResourcesDirectory();
        if (resources == null) return; // running from a relocated output path

        var resxKeys = Regex.Matches(File.ReadAllText(Path.Combine(resources, "App.en-US.resx")),
                "<data name=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToArray();

        var manager = new ResourceManager("ScoreTracker.Web.Resources.App",
            typeof(ScoreTracker.Web.App).Assembly);
        var set = manager.GetResourceSet(new CultureInfo("en-US"), true, true);
        Assert.NotNull(set);

        var compiled = set!.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet(StringComparer.Ordinal);
        var missing = resxKeys.Where(k => !compiled.Contains(k)).ToArray();

        _output.WriteLine($"resx={resxKeys.Length} compiled={compiled.Count} missing={missing.Length}");
        foreach (var key in missing.Take(40)) _output.WriteLine("MISSING: " + key);

        Assert.Empty(missing);
    }
}

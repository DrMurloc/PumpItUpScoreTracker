using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The eleven-skill rollup stays dead (docs/design/nuke-old-skill-categories.md, owner
///     2026-08-25). It was deleted once before as a data source and grew back as a display
///     vocabulary, which is what a ratchet is for: the site speaks the granular piucenter
///     badges and the five badge families, and nothing may quietly reintroduce a coarse
///     invented layer between them.
///     <para>
///         The Chabala tier list is the one exception, and it is deliberately narrow: it reads
///         the archived hand tags through <c>GetArchivedSkillTagsQuery</c> and renders them
///         uncoloured. That query is allowed to exist; a <c>skillcat-</c> class is not.
///     </para>
/// </summary>
public sealed class RetiredSkillRollupTests
{
    /// <summary>Names the rollup owned. None may return to the source tree.</summary>
    private static readonly IReadOnlyList<string> RetiredNames = new[]
    {
        "skillcat-",
        "SkillCategoryHelpers",
        "PiuCenterSkillMapper",
        "ChartSkillsRecord",
        "GetChartSkillChipsQuery",
        "ChartSkillChipRecord",
        "HasSkillData"
    };

    [Fact]
    public void TheRetiredRollupsVocabularyDoesNotComeBack()
    {
        var root = FindSolutionRoot();
        var violations = SourceFiles(root)
            .SelectMany(file => RetiredNames
                .Where(name => File.ReadAllText(file).Contains(name, StringComparison.Ordinal))
                .Select(name => $"{Path.GetRelativePath(root, file).Replace('\\', '/')}: {name}"))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0,
            "The retired skill rollup reappeared. Use the granular badges (BadgeLabels) and the " +
            "five BadgeCategory families instead:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     The enums themselves. Asserted by name against every loaded assembly rather than by
    ///     source text, so a reintroduction anywhere in the solution trips it.
    /// </summary>
    [Fact]
    public void TheSkillAndSkillCategoryEnumsStayDeleted()
    {
        var resurrected = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("ScoreTracker", StringComparison.Ordinal) == true)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    return e.Types.Where(t => t != null)!;
                }
            })
            .Where(t => t!.IsEnum && t.Namespace == "ScoreTracker.SharedKernel.Enums" &&
                        t.Name is "Skill" or "SkillCategory")
            .Select(t => t!.FullName!)
            .ToArray();

        Assert.True(resurrected.Length == 0,
            "Deleted enums are back: " + string.Join(", ", resurrected));
    }

    private static IEnumerable<string> SourceFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        // Applied migrations are history and are never edited (CLAUDE.md).
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                        // This file names them all on purpose.
                        && !f.EndsWith("RetiredSkillRollupTests.cs", StringComparison.Ordinal));
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("ScoreTracker.sln not found above test bin directory");
    }
}

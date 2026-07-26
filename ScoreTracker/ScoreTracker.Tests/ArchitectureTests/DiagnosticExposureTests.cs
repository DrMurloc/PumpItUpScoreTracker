using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Diagnostic-exposure ratchet (2026-07-26): raw exception text — stack traces, framework
///     error strings, the guts of a failed background job — is for the maintainer, never for a
///     player. The Official Leaderboards header used to hang the whole sweep exception off a
///     tooltip on a public page, so every visitor read a .NET SSL stack trace where a
///     last-updated stamp belonged. Domain exception MESSAGES are a different thing and stay
///     allowed: "File cannot be larger than 10 MB" is copy written for a user.
///     Admin pages are exempt by design — that is where the detail is supposed to live.
/// </summary>
public sealed class DiagnosticExposureTests
{
    // Contracts whose payload carries raw exception text. A page that touches one of these is
    // rendering diagnostics; it belongs under Pages/Admin. Add to this list, never remove.
    private static readonly string[] DiagnosticSurfaces =
    {
        "GetImportRunsQuery",
        "ImportRunRecord"
    };

    private static readonly string[] ScannedFolders = { "Pages", "Components", "Shared" };

    private const string AdminFolder = "Pages/Admin/";

    [Fact]
    public void DiagnosticSurfacesAreReferencedOnlyByAdminPages()
    {
        var violations = UiFiles()
            .Where(f => !f.Path.StartsWith(AdminFolder, StringComparison.Ordinal))
            .SelectMany(f => DiagnosticSurfaces
                .Where(surface => f.Text.Contains(surface, StringComparison.Ordinal))
                .Select(surface =>
                    $"{f.Path}: references {surface}, which carries raw exception text — diagnostics belong under {AdminFolder}"))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void UiCodeNeverRendersAStackTrace()
    {
        var violations = UiFiles()
            .Where(f => !f.Path.StartsWith(AdminFolder, StringComparison.Ordinal))
            .Where(f => f.Text.Contains(".StackTrace", StringComparison.Ordinal)
                        || f.Text.Contains("ToStringDemystified", StringComparison.Ordinal))
            .Select(f => $"{f.Path}: renders a stack trace — show a localized message and log the detail instead")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static IReadOnlyList<(string Path, string Text)> UiFiles()
    {
        var webRoot = Path.Combine(FindSolutionRoot(), "ScoreTracker");
        return ScannedFolders
            .Select(f => Path.Combine(webRoot, f))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (Path: Path.GetRelativePath(webRoot, f).Replace('\\', '/'), Text: File.ReadAllText(f)))
            .ToArray();
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

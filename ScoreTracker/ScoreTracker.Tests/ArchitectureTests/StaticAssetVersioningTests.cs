using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Cache-busting ratchet (2026-07-26): every stylesheet and script the app ships is
///     requested by its content-hashed name, so a release that changes a file changes its URL
///     and a browser holding the previous copy cannot paint last release's styles over this
///     release's markup. The mechanism is MapStaticAssets: markup reads
///     @Assets["css/site.css"] and gets css/site.&lt;hash&gt;.css back. Nothing hand-versioned,
///     nothing raw. Rules here are added, never removed.
/// </summary>
public sealed class StaticAssetVersioningTests
{
    // A whole link/script element whose href/src is a .css/.js path NOT starting with '@' —
    // an @Assets[...] expression is the compliant form, and excluding '@' is what skips it.
    // The element rather than the attribute, so the tag's other attributes are readable here.
    // A trailing query is captured so hand-rolled "?v=3" versions are caught, not excused.
    private static readonly Regex AssetTag =
        new("""<(?:link|script)\b[^>]*?(?:href|src)\s*=\s*"(?<url>[^"@][^"]*\.(?:css|js)(?:\?[^"]*)?)"[^>]*>""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Blazor's boot script is versioned and cached by the framework, and its manifest entry is
    // not ours to redirect — the .NET templates leave it on its plain name for the same reason.
    private static readonly string[] FrameworkOwned = { "_framework/blazor.web.js" };

    [Fact]
    public void ShippedStylesheetsAndScriptsAreContentHashed()
    {
        var web = WebProjectRoot();
        var violations = new List<string>();

        foreach (var file in MarkupFiles(web))
        {
            var relative = Path.GetRelativePath(web, file).Replace('\\', '/');
            // A Razor Page cannot reach @Assets: that collection belongs to the component
            // endpoint and is not in DI, so injecting it throws at render time. The
            // TagHelper's content-hash query is the whole mechanism available there.
            var isRazorPage = file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);
            foreach (Match match in AssetTag.Matches(File.ReadAllText(file)))
            {
                var url = match.Groups["url"].Value;
                if (IsExternal(url) || FrameworkOwned.Any(f =>
                        url.EndsWith(f, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (isRazorPage && match.Value.Contains("asp-append-version=\"true\"", StringComparison.Ordinal))
                    continue;
                violations.Add(isRazorPage
                    ? $"""{relative}: "{url}" is served under its plain name with no version — add asp-append-version="true" so the URL changes when the file does."""
                    : $"""{relative}: "{url}" is served under its plain name — a browser holding the previous release's copy has no way to know it changed. Use @Assets["{url.TrimStart('/').Split('?')[0]}"] instead.""");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     @Assets only hands back hashed names because MapStaticAssets published them. Swap it
    ///     back for UseStaticFiles and every @Assets lookup silently returns the plain path
    ///     again — the markup still compiles, the pages still render, and the caching is gone
    ///     with nothing to notice it. This is that notice.
    /// </summary>
    [Fact]
    public void StaticAssetsAreServedFromTheBuildManifest()
    {
        var program = File.ReadAllText(Path.Combine(WebProjectRoot(), "Program.cs"));

        Assert.True(program.Contains("app.MapStaticAssets()", StringComparison.Ordinal),
            "Program.cs no longer calls app.MapStaticAssets() — @Assets[...] lookups across the app fall back to unhashed paths and every release goes back to serving stale CSS.");
        Assert.False(program.Contains("app.UseStaticFiles()", StringComparison.Ordinal),
            "Program.cs calls app.UseStaticFiles() again. It short-circuits ahead of routing, so it answers /css/site.css before the manifest endpoint can, without the ETag revalidation MapStaticAssets attaches.");
    }

    /// <summary>
    ///     Three scripts are ES modules the circuit imports by name at runtime
    ///     (JSRuntime "import" of ./js/helpers.js and friends), where there is no tag to hang
    ///     @Assets on. The import map is what rewrites those specifiers to the hashed names in
    ///     the browser; without it those three quietly become the only stale-able assets left.
    /// </summary>
    [Fact]
    public void ModuleImportsResolveThroughAnImportMap()
    {
        var app = File.ReadAllText(Path.Combine(WebProjectRoot(), "App.razor"));

        Assert.True(app.Contains("<ImportMap", StringComparison.Ordinal),
            "App.razor has no <ImportMap /> — the JS modules loaded through JSRuntime \"import\" would resolve to their unhashed names and cache across releases.");
    }

    private static IEnumerable<string> MarkupFiles(string web)
    {
        return Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static bool IsExternal(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("//", StringComparison.Ordinal);
    }

    private static string WebProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("ScoreTracker.sln not found above test bin directory");
        return Path.Combine(dir.FullName, "ScoreTracker");
    }
}

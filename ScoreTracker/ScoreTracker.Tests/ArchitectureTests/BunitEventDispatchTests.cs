using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     bUnit event-dispatch ratchet (2026-08-08): a component test that asserts on the
///     result of an event awaits the dispatch.
///     <para>
///         bUnit's synchronous helpers — <c>Click()</c>, <c>Change()</c>, <c>Input()</c> —
///         post the event to the renderer's dispatcher and return without waiting for it.
///         The test thread is not the dispatcher thread, so the handler runs later on a
///         pool thread, and xUnit parks one collection per processor on a synchronous test
///         body — so on a many-core box the pool is at its minimum with every worker
///         blocked, the dispatch waits on the thread-injection throttle, and the assertion
///         on the next line reads the pre-event render. The failure is intermittent, load
///         dependent, and points nowhere near its cause: no exception, a valid
///         <c>blazor:onclick</c> in the markup, and the component's own state untouched.
///         Every helper has an <c>Async</c> twin that completes when the handler is done.
///     </para>
///     <para>
///         The allowlist below is the debt at the time the cause was found. Counts may
///         only go DOWN — convert a file's dispatches to the awaited form and lower (or
///         remove) its entry in the same PR. New files get no allowance. The component
///         suite's own <c>ThreadPoolWarmup</c> raises the pool's worker floor, which makes
///         the remaining debt survivable at ordinary load, but it widens the window rather
///         than closing it and is not a substitute for awaiting.
///     </para>
///     <para>
///         Not covered here: an unawaited <c>cut.InvokeAsync(...)</c> is the same defect in
///         a shape a regex cannot separate from the legitimate nested
///         <c>EventCallback.InvokeAsync</c> inside an awaited outer call.
///     </para>
/// </summary>
public sealed class BunitEventDispatchTests
{
    // Sync dispatch helpers whose names cannot collide with LINQ or a domain method.
    // Deliberately not the full bUnit set: Select/Load/Play/Stop/Reset/Toggle and friends
    // read as ordinary method names, and a false positive here costs a real test.
    // Extend this list when a test reaches for a helper it does not name yet.
    private static readonly Regex SyncDispatch = new(
        @"\.(?:Click|Change|Input|DoubleClick|KeyDown|KeyUp|KeyPress|Blur|MouseOver|MouseDown|MouseUp|ContextMenu|DragStart|DragEnd|Paste|Wheel)\(",
        RegexOptions.Compiled);

    // Baseline captured 2026-08-08. Shrink-only.
    // RandomizerSettingsPanelTests (13) and ByLevelBreakdownConfigPanelTests (2) burned
    // theirs when the cause was found — they hold no entry, and must not gain one.
    private static readonly IReadOnlyDictionary<string, int> Allowance = new Dictionary<string, int>
    {
        ["ArmedActionSelectorTests.cs"] = 1,
        ["ChartDetailsDialogTests.cs"] = 1,
        ["ChartQuickRecordDialogTests.cs"] = 3,
        ["ChartVideoPlayerTests.cs"] = 4,
        ["ChartsExportDialogTests.cs"] = 1,
        ["ChartsPageTests.cs"] = 28,
        ["CommunityInvitePageTests.cs"] = 4,
        ["CommunityToolsReviewPageTests.cs"] = 5,
        ["ConsoleWebhooksPageTests.cs"] = 4,
        ["DrawCardTileTests.cs"] = 2,
        ["FolderGridTests.cs"] = 2,
        ["FolderLevelsConfigPanelTests.cs"] = 2,
        ["FolderPickerTests.cs"] = 2,
        ["LeaderboardDialogTests.cs"] = 1,
        // The three in the InvokeAsync helper are the ApexChart workaround, not debt: a
        // page that parks work on the dispatcher needs the dispatcher pumped, and the
        // click inside that lambda is already running on the right thread.
        ["LifeCalculatorPageTests.cs"] = 4,
        ["MixChangesPageTests.cs"] = 8,
        ["OfficialLeaderboardsHubTests.cs"] = 6,
        ["QualifiersAdminPageTests.cs"] = 5,
        // QuickRecordWidgetTests burned its 8 — it was the loudest file left once the
        // warmup was in (three of its facts chain a grade tap into a save), so it went
        // first — entry removed, ratchet tightened.
        ["RivalsOfMeListTests.cs"] = 1,
        ["ScoreCheckPanelTests.cs"] = 2,
        ["SessionHeroTests.cs"] = 1,
        ["SimilarChartsShelfTests.cs"] = 18,
        ["TitlesPageTests.cs"] = 12,
        ["ToolSetupWizardTests.cs"] = 29,
        ["ToolWebhookPanelTests.cs"] = 4,
        ["UploadPhoenixScoresPageTests.cs"] = 1
    };

    [Fact]
    public void ComponentTestsAwaitTheirEventDispatches()
    {
        var suiteRoot = Path.Combine(FindSolutionRoot(), "ScoreTracker.Tests.Components");
        Assert.True(Directory.Exists(suiteRoot), $"component test project not found at {suiteRoot}");

        var counts = Directory.EnumerateFiles(suiteRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (Path: Path.GetRelativePath(suiteRoot, f).Replace('\\', '/'),
                Count: SyncDispatch.Matches(File.ReadAllText(f)).Count))
            .Where(x => x.Count > 0 || Allowance.ContainsKey(x.Path))
            .ToArray();

        var violations = new List<string>();
        foreach (var (path, count) in counts.OrderBy(c => c.Path, StringComparer.Ordinal))
        {
            var allowed = Allowance.TryGetValue(path, out var a) ? a : 0;
            if (count > allowed)
                violations.Add(
                    $"{path}: {count} synchronous bUnit dispatch(es), allowance {allowed} — make the test async and await ClickAsync/ChangeAsync/InputAsync so the handler has run before the assertion");
            else if (count < allowed)
                violations.Add(
                    $"{path}: down to {count} but allowance is {allowed} — ratchet it: lower this file's entry to {count} (or remove it) in BunitEventDispatchTests");
        }

        var scanned = counts.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        violations.AddRange(Allowance.Keys.Where(k => !scanned.Contains(k))
            .Select(k => $"{k}: no longer exists — remove its allowance entry"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
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

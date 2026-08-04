using System.Text;
using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.ExplorationTests.Translations.Evaluation;

/// <summary>
///     Writes a sweep to markdown, because 23 comments across three arms and four locales is
///     roughly 276 renderings and nobody reads that out of a test runner's console.
///     <para>
///         The layout is deliberately comment-first rather than arm-first: the question is
///         whether a cheaper model got <em>this</em> comment right, which is answerable by
///         reading three renderings side by side and unanswerable by scrolling between three
///         separate sections.
///     </para>
/// </summary>
internal static class SweepReport
{
    public static string Write(IReadOnlyList<SweepResult> results, string stamp)
    {
        var report = new StringBuilder();
        var arms = results.Select(r => r.Arm).Distinct().ToArray();

        report.AppendLine("# Chart-comment translation sweep");
        report.AppendLine();
        report.AppendLine($"Run {stamp}. Pivot through English, register carried as metadata, ");
        report.AppendLine("thinking disabled, synchronous calls, glossary limited to what a model cannot know.");
        report.AppendLine();

        AppendSummary(report, results, arms);
        AppendEntityTable(report, results, arms);
        AppendLanguageDetection(report, results, arms);
        AppendComments(report, results, arms);

        return report.ToString();
    }

    private static void AppendSummary(StringBuilder report, IReadOnlyList<SweepResult> results,
        IReadOnlyList<ModelArm> arms)
    {
        report.AppendLine("## Cost");
        report.AppendLine();
        report.AppendLine("| Arm | Comments | Failed | Total | Per comment | Per 1,000/mo | Batched (50%) |");
        report.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var arm in arms)
        {
            var forArm = results.Where(r => r.Arm == arm).ToArray();
            var ok = forArm.Count(r => r.Outcome != null);
            var total = forArm.Sum(r => r.Cost);
            var each = ok == 0 ? 0m : total / ok;

            report.AppendLine($"| {arm.Name} | {ok} | {forArm.Length - ok} | ${total:F4} | " +
                              $"${each:F4} | ${each * 1000:F2} | ${each * 500:F2} |");
        }

        report.AppendLine();
    }

    private static void AppendEntityTable(StringBuilder report, IReadOnlyList<SweepResult> results,
        IReadOnlyList<ModelArm> arms)
    {
        report.AppendLine("## Entity survival");
        report.AppendLine();
        report.AppendLine("Difficulty codes, scores, timestamps and emoji checked in every locale; ");
        report.AppendLine("names only where the script matches, since Korean transliterates them by design.");
        report.AppendLine();
        report.AppendLine("| Arm | Checked | Survived | Rate | Lost |");
        report.AppendLine("|---|---|---|---|---|");

        foreach (var arm in arms)
        {
            var findings = results.Where(r => r.Arm == arm).SelectMany(r => r.Entities).ToArray();
            var kept = findings.Count(f => f.Survived);
            var lost = findings.Where(f => !f.Survived)
                .Select(f => $"{f.Token} ({f.Locale}, {f.Comment})")
                .Distinct()
                .ToArray();
            var rate = findings.Length == 0 ? 0 : 100.0 * kept / findings.Length;

            report.AppendLine($"| {arm.Name} | {findings.Length} | {kept} | {rate:F1}% | " +
                              $"{(lost.Length == 0 ? "—" : string.Join("; ", lost.Take(8)))} |");
        }

        report.AppendLine();
    }

    private static void AppendLanguageDetection(StringBuilder report, IReadOnlyList<SweepResult> results,
        IReadOnlyList<ModelArm> arms)
    {
        report.AppendLine("## Source-language detection");
        report.AppendLine();
        report.AppendLine("| Arm | Correct | Of | Missed |");
        report.AppendLine("|---|---|---|---|");

        foreach (var arm in arms)
        {
            var forArm = results.Where(r => r.Arm == arm && r.Outcome != null).ToArray();
            var missed = forArm.Where(r => !r.DetectedLanguageCorrectly)
                .Select(r => $"{r.Comment.Id} → {r.Outcome!.Pivot.SourceLanguage} (expected {r.Comment.ExpectedLanguage})")
                .ToArray();

            report.AppendLine($"| {arm.Name} | {forArm.Length - missed.Length} | {forArm.Length} | " +
                              $"{(missed.Length == 0 ? "—" : string.Join("; ", missed))} |");
        }

        report.AppendLine();
    }

    private static void AppendComments(StringBuilder report, IReadOnlyList<SweepResult> results,
        IReadOnlyList<ModelArm> arms)
    {
        report.AppendLine("## Comment by comment");
        report.AppendLine();

        foreach (var comment in TranslationCorpus.All)
        {
            report.AppendLine($"### `{comment.Id}` — {comment.ExpectedLanguage}");
            report.AppendLine();
            report.AppendLine($"> {comment.Text.Replace("\n", "\n> ")}");
            report.AppendLine();
            report.AppendLine($"*{comment.Note}*");
            report.AppendLine();

            foreach (var arm in arms)
            {
                var result = results.FirstOrDefault(r => r.Arm == arm && r.Comment.Id == comment.Id);
                if (result == null) continue;

                report.AppendLine($"**{arm.Name}**");
                report.AppendLine();

                if (result.Outcome == null)
                {
                    report.AppendLine($"Failed: {result.Failure}");
                    report.AppendLine();
                    continue;
                }

                var pivot = result.Outcome.Pivot;
                report.AppendLine($"- **en-US** (pivot) — {Flatten(pivot.English)}");
                report.AppendLine($"- *register* `{pivot.Register}`, *formality marked* " +
                                  $"`{pivot.FormalityMarked}`, *tone* `{pivot.Tone}`");

                if (pivot.Entities.Count > 0)
                    report.AppendLine("- *entities* " + string.Join(", ",
                        pivot.Entities.Select(e => $"`{e.Surface}`→`{e.Canonical}` ({e.Kind})")));

                foreach (var locale in TranslationTarget.All)
                    report.AppendLine(result.Outcome.Translations.TryGetValue(locale, out var text)
                        ? $"- **{locale}** — {Flatten(text)}"
                        : $"- **{locale}** — *missing*");

                report.AppendLine();
            }
        }
    }

    /// <summary>Line breaks survive into the rendering but would break a markdown list item.</summary>
    private static string Flatten(string text)
    {
        return text.Replace("\r\n", " ⏎ ").Replace("\n", " ⏎ ");
    }
}

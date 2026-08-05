using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.ExplorationTests.Translations.Evaluation;

/// <summary>One token that had to appear in one rendering, and whether it did.</summary>
internal sealed record EntityFinding(string Comment, string Locale, string Token, bool Survived);

/// <summary>
///     The free half of the evaluation. Every model tier gets asked to keep the same difficulty
///     codes, scores, timestamps, emoji, and names, and a string search says whether it did.
///     <para>
///         No model judges this and no human reads it, which is the point: it produces a number
///         that separates the tiers before anyone spends money on an opinion. It is also the
///         failure mode most likely to matter in production — a comment whose tone drifts is
///         merely worse, while a comment that says D28 when the player wrote D29 is wrong.
///     </para>
/// </summary>
internal static class EntityPreservationCheck
{
    /// <summary>
    ///     Forms that count as the same token. A Korean rendering writing 피펨즈 for Fefemz has
    ///     preserved the name, not lost it, and marking that a failure would punish the one
    ///     locale doing the harder thing correctly.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> Aliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fefemz"] = ["Fefemz", "피펨즈"],
            ["fefemz"] = ["Fefemz", "피펨즈"],
            ["Big One"] = ["Big One", "B1G", "빅원"],
            ["B1G"] = ["B1G", "Big One", "빅원"],
            // The Korean source writes the company and the game in Hangul; every Latin-script
            // locale should reach the romanized names, and Korean may keep either.
            ["Andamiro"] = ["Andamiro", "안다미로"],
            ["AM"] = ["AM", "Andamiro", "안다미로"],
            ["Phoenix 2"] = ["Phoenix 2", "피닉스 2", "PHX 2"],
            ["PHX"] = ["PHX", "Phoenix", "피닉스"]
        };

    public static IReadOnlyList<EntityFinding> Check(CorpusComment comment, TranslationOutcome outcome)
    {
        var findings = new List<EntityFinding>();

        foreach (var (locale, text) in outcome.Translations)
        {
            foreach (var token in comment.MustSurviveEverywhere)
                findings.Add(new EntityFinding(comment.Id, locale, token, Contains(text, token)));

            // Korean transliterates names into Hangul, which is correct, so the Latin-script
            // demand is only made of the locales that share the script.
            if (locale == "ko-KR") continue;

            foreach (var token in comment.NamesInLatinScript)
                findings.Add(new EntityFinding(comment.Id, locale, token, Contains(text, token)));
        }

        return findings;
    }

    private static bool Contains(string text, string token)
    {
        var accepted = Aliases.TryGetValue(token, out var forms) ? forms : [token];

        return accepted.Any(form => text.Contains(form, StringComparison.OrdinalIgnoreCase));
    }
}

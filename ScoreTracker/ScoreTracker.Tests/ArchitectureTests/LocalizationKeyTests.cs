using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     The resx files are the localization contract, and MSBuild's GenerateResource enforces two
///     rules on them that are invisible at runtime until a player switches language.
///     <para>
///         First: duplicate names are deduplicated <em>case-insensitively</em>, first-wins, and the
///         loser is dropped from the compiled satellite entirely (MSB3568, "ignored"). Two keys
///         differing only by case therefore cannot coexist — the second one resolves to nothing, the
///         localizer falls back to the key text, and every non-English locale silently renders
///         English. English looks perfect the whole time, because the key text <em>is</em> the
///         English copy, which is why this survived a full QC pass.
///     </para>
///     <para>
///         Second: a key present in en-US but missing from a translated locale has the same
///         effect for that locale alone.
///     </para>
///     Both are ratchets: a new key lands in every locale in the same pass (CLAUDE.md), and it
///     never differs from an existing key by case alone.
/// </summary>
public sealed class LocalizationKeyTests
{
    private const string BaseLocale = "en-US";

    /// <summary>The Murloc joke locale. At full key parity since 2026-07-28 — see MurlocLocale below.</summary>
    private const string MurlocLocale = "en-ZW";

    [Fact]
    public void NoResxKeysCollideOnlyByCase()
    {
        var violations = new List<string>();
        foreach (var (locale, keys) in ReadAllLocales())
            foreach (var group in keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1))
            {
                var casings = string.Join(", ", group.Select(k => $"'{k}'"));
                violations.Add(
                    $"App.{locale}.resx: {casings} differ only by case — GenerateResource keeps the first and drops the rest, so the dropped call sites render untranslated English. Pick one canonical key and update the call sites.");
            }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryLocaleCoversTheSameKeysAsEnUs()
    {
        var locales = ReadAllLocales().ToDictionary(x => x.Locale, x => x.Keys);
        var baseKeys = new HashSet<string>(locales[BaseLocale], StringComparer.Ordinal);

        var violations = new List<string>();
        foreach (var (locale, keys) in locales.Select(kv => (kv.Key, kv.Value)))
        {
            if (locale == BaseLocale) continue;

            var present = new HashSet<string>(keys, StringComparer.Ordinal);
            var missing = baseKeys.Except(present).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var extra = present.Except(baseKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();

            if (missing.Count > 0)
                violations.Add(
                    $"App.{locale}.resx is missing {missing.Count} key(s) present in {BaseLocale} — these render English for {locale}: {Preview(missing)}");
            if (extra.Count > 0)
                violations.Add(
                    $"App.{locale}.resx has {extra.Count} key(s) absent from {BaseLocale} — either the key is dead and should go, or {BaseLocale} needs it: {Preview(extra)}");
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     Murloc is a joke locale, and the joke only works if it is committed to. Every batch that
    ///     wrote en-ZW without a rule reached for the same shortcut — mangle the vowels out of the
    ///     English ("Search" to "Srglrch", "Back" to "Blrrgk") or just leave the English alone — which
    ///     reads as a speech impediment rather than a language and left 297 values in plain English.
    ///     <para>
    ///         The alphabet below is what the locale's original hand-written entries used, plus 'b'
    ///         and 'a'. Letters outside it are exactly where English creeps back in, so the letter set
    ///         is the ratchet. Acronyms, bracketed placeholders and the protected proper nouns are
    ///         excluded — a Murloc still has to be able to find piugame.com.
    ///     </para>
    ///     See docs/LOCALIZATION-en-ZW.md for the syllable inventory and term mappings.
    /// </summary>
    [Fact]
    public void MurlocValuesUseOnlyTheMurlocAlphabet()
    {
        const string alphabet = "abglmopru";
        string[] protectedNouns =
        [
            "Pump It Up", "PIUGame.com", "PIUGAME.com", "piugame.com", "PIU Center", "Iolite Sky",
            "Start.GG", "SkillAttack", "DrMurloc", "PUMBILITY", "Pumbility", "piuscores", "PIUGAME",
            "PIUGame", "YouTube", "Youtube", "Discord", "Phoenix", "Murloc", "BITE"
        ];

        var violations = new List<string>();
        foreach (var (key, value) in ReadValues(MurlocLocale))
        {
            var stripped = value;
            foreach (var noun in protectedNouns.OrderByDescending(n => n.Length))
                stripped = stripped.Replace(noun, " ", StringComparison.Ordinal);
            stripped = Regex.Replace(stripped, @"\{[^}]*\}", " ");   // {0}, {0:N0}
            stripped = Regex.Replace(stripped, @"\b[A-Z][A-Z0-9]+\b", " "); // BPM, NPS, SSS

            var offending = Regex.Matches(stripped, "[A-Za-z]+")
                .Select(m => m.Value)
                .FirstOrDefault(w => w.ToLowerInvariant().Any(c => !alphabet.Contains(c)));

            if (offending != null)
                violations.Add(
                    $"'{key}' = '{value}' — '{offending}' uses letters outside the Murloc alphabet '{alphabet}'. Build the word from the syllable inventory in docs/LOCALIZATION-en-ZW.md instead of mangling the English.");
        }

        Assert.True(violations.Count == 0,
            $"{violations.Count} Murloc value(s) leak English:{Environment.NewLine}{string.Join(Environment.NewLine, violations.Take(20))}");
    }

    private static IEnumerable<(string Key, string Value)> ReadValues(string locale)
    {
        var file = Path.Combine(FindSolutionRoot(), "ScoreTracker", "Resources", $"App.{locale}.resx");
        return XDocument.Load(file).Root!
            .Elements("data")
            .Select(d => ((string?)d.Attribute("name"), d.Element("value")?.Value))
            .Where(x => x.Item1 != null && x.Item2 != null)
            .Select(x => (x.Item1!, x.Item2!))
            .ToList();
    }

    private static string Preview(IReadOnlyCollection<string> keys) =>
        string.Join(", ", keys.Take(10).Select(k => $"'{k}'")) + (keys.Count > 10 ? ", …" : string.Empty);

    private static IEnumerable<(string Locale, List<string> Keys)> ReadAllLocales()
    {
        var dir = Path.Combine(FindSolutionRoot(), "ScoreTracker", "Resources");
        foreach (var file in Directory.EnumerateFiles(dir, "App.*.resx").OrderBy(f => f, StringComparer.Ordinal))
        {
            var locale = Path.GetFileNameWithoutExtension(file)["App.".Length..];
            var keys = XDocument.Load(file).Root!
                .Elements("data")
                .Select(d => (string?)d.Attribute("name"))
                .Where(n => n != null)
                .Select(n => n!)
                .ToList();
            yield return (locale, keys);
        }
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

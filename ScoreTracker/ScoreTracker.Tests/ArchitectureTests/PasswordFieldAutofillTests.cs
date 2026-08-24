using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Password-manager posture ratchet (import-fields fix, 2026-08-24): every masked
///     MudTextField declares what a password manager should do with it, via one of the
///     <c>PasswordManagerHints</c> bundles in its <c>UserAttributes</c>. MudBlazor emits no
///     <c>name</c> or <c>autocomplete</c> on its own, and a bare masked input fails in one of
///     two directions: a credential field managers cannot find (so nothing fills), or a
///     secret field they guess at (offering to fill a piugame password into a webhook
///     header). The shared bundles also pin the attribute values identical across surfaces,
///     so the entry a manager saves from the login page is offered on every credential
///     surface — managers key on our domain, not the field.
/// </summary>
public sealed class PasswordFieldAutofillTests
{
    private static readonly string[] ScannedFolders = { "Pages", "Components", "Shared" };

    [Fact]
    public void MaskedFieldsDeclareTheirPasswordManagerPosture()
    {
        var webRoot = Path.Combine(FindSolutionRoot(), "ScoreTracker");
        var violations = new List<string>();

        foreach (var file in ScannedFolders
                     .Select(f => Path.Combine(webRoot, f))
                     .Where(Directory.Exists)
                     .SelectMany(dir => Directory.EnumerateFiles(dir, "*.razor", SearchOption.AllDirectories)))
        {
            var text = File.ReadAllText(file);
            foreach (var (tag, line) in MudTextFieldTags(text))
            {
                if (!tag.Contains("InputType.Password", StringComparison.Ordinal)) continue;
                if (tag.Contains("PasswordManagerHints.", StringComparison.Ordinal)) continue;
                violations.Add(
                    $"{Path.GetRelativePath(webRoot, file).Replace('\\', '/')}:{line}: " +
                    "a password-type MudTextField says what a password manager should do with it — " +
                    "UserAttributes=\"@PasswordManagerHints.PiuGamePassword\" for a real piugame credential " +
                    "(its username field takes PiuGameUsername), or \"@PasswordManagerHints.NotALogin\" for a " +
                    "masked secret or token that managers must leave alone");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     Each MudTextField open tag with the line it starts on. The tag ends at the first
    ///     <c>&gt;</c> outside a quoted attribute value — attribute values legitimately carry
    ///     <c>&gt;</c> inside quotes (lambdas), so a bare IndexOf would cut tags short.
    /// </summary>
    private static IEnumerable<(string Tag, int Line)> MudTextFieldTags(string text)
    {
        const string open = "<MudTextField";
        var searchFrom = 0;
        while (true)
        {
            var start = text.IndexOf(open, searchFrom, StringComparison.Ordinal);
            if (start < 0) yield break;

            var inQuotes = false;
            var end = start + open.Length;
            while (end < text.Length && (inQuotes || text[end] != '>'))
            {
                if (text[end] == '"') inQuotes = !inQuotes;
                end++;
            }

            var line = text.AsSpan(0, start).Count('\n') + 1;
            yield return (text[start..(end < text.Length ? end + 1 : end)], line);
            searchFrom = end;
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

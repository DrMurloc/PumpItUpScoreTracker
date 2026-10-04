using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     How every api/v2 parameter that takes several values reads them: a comma list or the
///     parameter repeated, read as a union — a row matches any value named. An empty value picks
///     nothing, and no pick includes everything.
/// </summary>
internal static class Picks
{
    /// <summary>The values a list parameter names, split and trimmed; empty when it names none.</summary>
    public static string[] Split(string[]? values)
    {
        // An empty query value binds as a null element.
        return values?
            .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    ///     The members a list parameter names, matched case-insensitively by name — never
    ///     <c>Enum.TryParse</c>, which merges a comma list into one bitwise value and takes any
    ///     number. Null when the parameter names none; a 400 of <paramref name="problemType" /> on
    ///     the first name that is not a member.
    /// </summary>
    public static (IReadOnlySet<TEnum>? Picked, ObjectResult? Problem) Resolve<TEnum>(string[]? values,
        string problemType, string noun, Func<string, string, string?, ObjectResult> problem)
        where TEnum : struct, Enum
    {
        var tokens = Split(values);
        if (tokens.Length == 0) return (null, null);

        var picked = new HashSet<TEnum>();
        foreach (var token in tokens)
        {
            var member = Enum.GetValues<TEnum>()
                .Where(m => m.ToString().Equals(token, StringComparison.OrdinalIgnoreCase))
                .Select(m => (TEnum?)m)
                .FirstOrDefault();
            if (member is null)
                return (null, problem(problemType, $"'{token}' is not a {noun}.",
                    $"Valid values: {string.Join(", ", Enum.GetNames<TEnum>())}."));
            picked.Add(member.Value);
        }

        return (picked, null);
    }

    public static bool Includes<T>(IReadOnlySet<T>? picked, T value)
    {
        return picked is null || picked.Contains(value);
    }

    /// <summary>The parameter as written, ordered, so the cursor fingerprint carries it.</summary>
    public static string Fingerprint(string[]? values)
    {
        return values is null ? string.Empty : string.Join(",", values.OrderBy(v => v, StringComparer.Ordinal));
    }
}

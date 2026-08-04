using System.Text.Json;
using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.Translations.Domain;

/// <summary>
///     Turns the two model responses into records.
///     <para>
///         Both calls are made under a JSON schema, so a malformed response is not a case to
///         recover from — it means the request went out without its schema, or the adapter
///         returned something other than the model's text. Either is a bug in the calling code,
///         and throwing names it immediately instead of producing a translation with silently
///         empty fields.
///     </para>
/// </summary>
internal static class TranslationResponseReader
{
    public static PivotResult ReadPivot(string json)
    {
        using var document = Parse(json, "pivot");
        var root = document.RootElement;

        var entities = Require(root, "entities", "pivot").EnumerateArray()
            .Select(e => new TranslationEntity(
                Text(e, "surface", "pivot entity"),
                Text(e, "canonical", "pivot entity"),
                Text(e, "kind", "pivot entity")))
            .ToArray();

        return new PivotResult(
            Text(root, "source_language", "pivot"),
            Text(root, "english", "pivot"),
            Text(root, "register", "pivot"),
            Require(root, "formality_marked", "pivot").GetBoolean(),
            Text(root, "tone", "pivot"),
            entities);
    }

    public static IReadOnlyDictionary<string, string> ReadTranslations(string json)
    {
        using var document = Parse(json, "fan-out");

        return Require(document.RootElement, "translations", "fan-out").EnumerateArray()
            .ToDictionary(
                t => Text(t, "locale", "fan-out translation"),
                t => Text(t, "text", "fan-out translation"));
    }

    private static JsonDocument Parse(string json, string stage)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The {stage} response was not JSON. It was requested under a schema, so this " +
                "means the schema never reached the API or the adapter returned the wrong field.",
                exception);
        }
    }

    private static JsonElement Require(JsonElement element, string property, string stage)
    {
        if (!element.TryGetProperty(property, out var value))
            throw new InvalidOperationException(
                $"The {stage} response has no '{property}'. The schema marks it required, so the " +
                "request was made without one.");

        return value;
    }

    private static string Text(JsonElement element, string property, string stage)
    {
        return Require(element, property, stage).GetString() ?? string.Empty;
    }
}

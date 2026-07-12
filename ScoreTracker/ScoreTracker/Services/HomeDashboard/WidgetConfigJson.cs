using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     THE serializer for widget config blobs: camelCase, enum names as strings,
///     null-skipping, case-insensitive reads. Config blobs are public API via
///     export/import (D19) — this shape is contract, and old or garbled blobs are
///     tolerated forever (§2.3: reads fall back to defaults, never throw).
/// </summary>
public static class WidgetConfigJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Read<T>(string? configJson) where T : new()
    {
        if (string.IsNullOrWhiteSpace(configJson)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(configJson, Options) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    public static string Write<T>(T config)
    {
        return JsonSerializer.Serialize(config, Options);
    }
}

using System.Collections;
using System.Text.Json;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     The "what's available" document (D19, §2.6): limits, size tokens, mixes, and one
///     entry per widget type with a JSON schema reflected from its config record — so
///     people can hand the file to an AI and get a valid page-import document back.
///     PUBLIC API, pinned by the Tests.Api golden: config record changes surface here
///     as reviewed diffs, which is the point.
/// </summary>
public static class CapabilitySchemaService
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Document = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Build()
    {
        var schema = new
        {
            Version = CurrentVersion,
            Limits = new
            {
                WidgetsPerPage = HomePageRecord.MaxWidgetsPerPage,
                PagesPerUser = HomePageRecord.MaxPagesPerUser
            },
            Mixes = Enum.GetNames<MixEnum>(),
            Widgets = WidgetRegistry.All.Select(d => new
            {
                Type = d.TypeId,
                Name = d.NameKey,
                Description = d.DescriptionKey,
                Category = d.Category.ToString(),
                SupportedSizes = d.SupportedSizes.Select(s => s.Token).ToArray(),
                DefaultSize = d.DefaultSize.Token,
                SupportedMixes = d.SupportedMixes.Select(m => m.ToString()).ToArray(),
                ConfigSchema = d.ConfigType == null ? null : SchemaFor(d.ConfigType, new HashSet<Type>())
            }).ToArray()
        };
        return JsonSerializer.Serialize(schema, Document);
    }

    private static Dictionary<string, object?> SchemaFor(Type type, HashSet<Type> seen)
    {
        seen.Add(type);
        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = type.GetProperties()
                .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name),
                    p => PropertySchema(p.PropertyType, seen))
        };
    }

    private static object PropertySchema(Type type, HashSet<Type> seen)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            var inner = PropertySchema(underlying, seen) as Dictionary<string, object?>
                        ?? new Dictionary<string, object?>();
            inner["nullable"] = true;
            return inner;
        }

        if (type.IsEnum)
            return new Dictionary<string, object?> { ["enum"] = Enum.GetNames(type) };
        if (type == typeof(bool))
            return new Dictionary<string, object?> { ["type"] = "boolean" };
        if (type == typeof(int) || type == typeof(long) || type == typeof(byte))
            return new Dictionary<string, object?> { ["type"] = "integer" };
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return new Dictionary<string, object?> { ["type"] = "number" };
        if (type == typeof(string))
            return new Dictionary<string, object?> { ["type"] = "string" };
        if (type == typeof(Guid))
            return new Dictionary<string, object?> { ["type"] = "string", ["format"] = "uuid" };
        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
            return new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["items"] = PropertySchema(type.GetGenericArguments()[0], seen),
                ["nullable"] = true
            };
        // A nested record we own (e.g. the completion threshold's {kind, value}) → recurse
        // so its shape is explicit for AI-built configs; framework objects stay opaque, and
        // the seen-set breaks any (currently nonexistent) config self-reference.
        if (type.Namespace?.StartsWith("ScoreTracker", StringComparison.Ordinal) == true && !seen.Contains(type))
            return SchemaFor(type, seen);
        return new Dictionary<string, object?> { ["type"] = "object" };
    }
}

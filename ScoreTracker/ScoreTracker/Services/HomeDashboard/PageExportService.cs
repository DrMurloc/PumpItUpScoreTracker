using System.Text.Json;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.HomePage.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.HomeDashboard;

// The page export/import document (D19, §2.6). PUBLIC API: camelCase, string enums,
// versioned envelope — breaking-change review discipline like api/*, pinned by the
// Tests.Api goldens. Validation errors are deliberately English and precise: they get
// pasted back into whatever AI generated the document.

public sealed record PageExportDocument(int Version, PageExport Page);

public sealed record PageExport(string Name, MixEnum? DefaultMix, IReadOnlyList<WidgetExport> Widgets);

public sealed record WidgetExport(string Type, string? Title, string Size, JsonElement? Config);

public sealed record PageImportResult(IReadOnlyList<string> Errors, string Name, MixEnum? DefaultMix,
    IReadOnlyList<HomePageWidgetSpec> Widgets)
{
    public bool IsValid => !Errors.Any();
}

public static class PageExportService
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Document = new(WidgetConfigJson.Options)
    {
        WriteIndented = true
    };

    public static string Export(HomePageRecord page)
    {
        var widgets = page.Widgets.OrderBy(w => w.Ordinal)
            .Select(w => new WidgetExport(w.WidgetType, w.Title, w.SizePreset, ParseConfig(w.ConfigJson)))
            .ToArray();
        return JsonSerializer.Serialize(
            new PageExportDocument(CurrentVersion, new PageExport(page.Name, page.DefaultMix, widgets)),
            Document);
    }

    public static PageImportResult Parse(string json)
    {
        var errors = new List<string>();
        PageExportDocument? document = null;
        try
        {
            document = JsonSerializer.Deserialize<PageExportDocument>(json, Document);
        }
        catch (JsonException exception)
        {
            errors.Add($"Invalid JSON: {exception.Message}");
        }

        if (document == null)
        {
            if (!errors.Any()) errors.Add("The document is empty.");
            return new PageImportResult(errors, string.Empty, null, Array.Empty<HomePageWidgetSpec>());
        }

        if (document.Version != CurrentVersion)
            errors.Add($"Unsupported version {document.Version} — this build speaks version {CurrentVersion}.");
        if (document.Page == null!)
        {
            errors.Add("Missing 'page' object.");
            return new PageImportResult(errors, string.Empty, null, Array.Empty<HomePageWidgetSpec>());
        }

        var widgets = document.Page.Widgets ?? Array.Empty<WidgetExport>();
        if (widgets.Count > HomePageRecord.MaxWidgetsPerPage)
            errors.Add(
                $"A page holds at most {HomePageRecord.MaxWidgetsPerPage} widgets — this document has {widgets.Count}.");

        var specs = new List<HomePageWidgetSpec>();
        for (var i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            var descriptor = widget.Type == null ? null : WidgetRegistry.TryGet(widget.Type);
            if (descriptor == null)
            {
                errors.Add($"widgets[{i}]: unknown type '{widget.Type}'. Valid types: " +
                           string.Join(", ", WidgetRegistry.All.Select(d => d.TypeId)) + ".");
                continue;
            }

            var size = SizePreset.TryParse(widget.Size);
            if (size == null || !descriptor.SupportedSizes.Contains(size.Value))
            {
                errors.Add($"widgets[{i}]: size '{widget.Size}' isn't supported by '{widget.Type}' — " +
                           "supported: " + string.Join(", ", descriptor.SupportedSizes.Select(s => s.Token)) +
                           ".");
                continue;
            }

            if (widget.Title is { Length: > HomePageWidgetRecord.MaxTitleLength })
            {
                errors.Add($"widgets[{i}]: titles cap at {HomePageWidgetRecord.MaxTitleLength} characters.");
                continue;
            }

            var configJson = widget.Config?.GetRawText() ?? "{}";
            if (configJson.Length > HomePageWidgetRecord.MaxConfigLength)
            {
                errors.Add(
                    $"widgets[{i}]: config caps at {HomePageWidgetRecord.MaxConfigLength} characters of JSON.");
                continue;
            }

            specs.Add(new HomePageWidgetSpec(descriptor.TypeId, widget.Title, size.Value.Token, configJson, 1));
        }

        return new PageImportResult(errors, document.Page.Name ?? string.Empty, document.Page.DefaultMix,
            specs);
    }

    private static JsonElement? ParseConfig(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson) || configJson.Trim() == "{}") return null;
        try
        {
            using var parsed = JsonDocument.Parse(configJson);
            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

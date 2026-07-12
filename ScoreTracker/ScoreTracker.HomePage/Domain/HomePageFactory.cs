using ScoreTracker.HomePage.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.HomePage.Domain;

/// <summary>
///     Identity creation is a domain concern — handlers never call Guid.NewGuid()
///     directly (CLAUDE.md ID seam convention).
/// </summary>
internal static class HomePageFactory
{
    public static HomePageRecord NewPage(Name name, int ordinal, bool isDefault)
    {
        return new HomePageRecord(Guid.NewGuid(), name, ordinal, isDefault, null,
            Array.Empty<HomePageWidgetRecord>());
    }

    public static HomePageWidgetRecord NewWidget(string widgetType, string sizePreset, string? title,
        string configJson, int configVersion, int ordinal)
    {
        return new HomePageWidgetRecord(Guid.NewGuid(), widgetType, title, ordinal, sizePreset, configJson,
            configVersion);
    }
}

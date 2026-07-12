using ScoreTracker.HomePage.Contracts;

namespace ScoreTracker.HomePage.Domain;

internal interface IHomePageRepository
{
    /// <summary>All of a user's pages with widgets, pages by tab order, widgets by auto-flow order.</summary>
    Task<IReadOnlyList<HomePageRecord>> GetPages(Guid userId, CancellationToken cancellationToken);

    Task CreatePage(Guid userId, HomePageRecord page, CancellationToken cancellationToken);

    /// <summary>Updates page fields (name, ordinal, default flag, mix context) — never widgets.</summary>
    Task UpdatePage(HomePageRecord page, CancellationToken cancellationToken);

    /// <summary>Multi-page ordinal/default rewrite in one transaction (reorder, default promotion).</summary>
    Task UpdatePageOrder(IReadOnlyList<(Guid PageId, int Ordinal, bool IsDefault)> pages,
        CancellationToken cancellationToken);

    /// <summary>Deletes the page and its widgets.</summary>
    Task DeletePage(Guid pageId, CancellationToken cancellationToken);

    Task AddWidget(Guid pageId, HomePageWidgetRecord widget, CancellationToken cancellationToken);

    Task UpdateWidget(HomePageWidgetRecord widget, CancellationToken cancellationToken);

    Task RemoveWidget(Guid widgetId, CancellationToken cancellationToken);

    /// <summary>Ordinal rewrite for one page's widgets in one transaction (move, post-remove compaction).</summary>
    Task SetWidgetOrdinals(IReadOnlyList<(Guid WidgetId, int Ordinal)> ordinals,
        CancellationToken cancellationToken);

    /// <summary>Deletes the page's widgets and inserts the given set in one transaction (import, D19).</summary>
    Task ReplaceWidgets(Guid pageId, IReadOnlyList<HomePageWidgetRecord> widgets,
        CancellationToken cancellationToken);
}

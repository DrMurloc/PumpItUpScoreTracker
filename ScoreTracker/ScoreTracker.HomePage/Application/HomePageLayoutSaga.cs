using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.HomePage.Contracts.Commands;
using ScoreTracker.HomePage.Contracts.Exceptions;
using ScoreTracker.HomePage.Contracts.Queries;
using ScoreTracker.HomePage.Domain;

namespace ScoreTracker.HomePage.Application;

/// <summary>
///     The dashboard layout use cases (feature-grouped handlers, "Saga" in this
///     codebase's sense). Everything is scoped to the CURRENT user (D2: private in v1) —
///     a foreign or stale page/widget id reads as not-found. Caps (D4) are enforced
///     here, not just in the UI. The full working set is at most 8 pages × 8 widgets,
///     so handlers load the user's pages once and reason in memory.
/// </summary>
internal sealed class HomePageLayoutSaga(IHomePageRepository repository, ICurrentUserAccessor currentUser) :
    IRequestHandler<GetMyHomePagesQuery, IReadOnlyList<HomePageRecord>>,
    IRequestHandler<CreateHomePageCommand, HomePageRecord>,
    IRequestHandler<RenameHomePageCommand>,
    IRequestHandler<DeleteHomePageCommand>,
    IRequestHandler<ReorderHomePageCommand>,
    IRequestHandler<SetDefaultHomePageCommand>,
    IRequestHandler<SetHomePageMixCommand>,
    IRequestHandler<AddHomePageWidgetCommand, HomePageWidgetRecord>,
    IRequestHandler<RemoveHomePageWidgetCommand>,
    IRequestHandler<MoveHomePageWidgetCommand>,
    IRequestHandler<ResizeHomePageWidgetCommand>,
    IRequestHandler<RenameHomePageWidgetCommand>,
    IRequestHandler<UpdateHomePageWidgetConfigCommand>,
    IRequestHandler<ReplaceHomePageWidgetsCommand>
{
    public async Task<IReadOnlyList<HomePageRecord>> Handle(GetMyHomePagesQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetPages(currentUser.User.Id, cancellationToken);
    }

    public async Task<HomePageRecord> Handle(CreateHomePageCommand request, CancellationToken cancellationToken)
    {
        ValidateName(request.Name);
        var pages = await repository.GetPages(currentUser.User.Id, cancellationToken);
        if (pages.Count >= HomePageRecord.MaxPagesPerUser)
            throw new HomePageCapReachedException(
                $"You already have {HomePageRecord.MaxPagesPerUser} pages — the current cap.");

        var page = HomePageFactory.NewPage(request.Name, pages.Count, !pages.Any());
        await repository.CreatePage(currentUser.User.Id, page, cancellationToken);
        return page;
    }

    public async Task Handle(RenameHomePageCommand request, CancellationToken cancellationToken)
    {
        ValidateName(request.Name);
        var page = await GetMyPage(request.PageId, cancellationToken);
        await repository.UpdatePage(page with { Name = request.Name }, cancellationToken);
    }

    public async Task Handle(DeleteHomePageCommand request, CancellationToken cancellationToken)
    {
        var pages = await repository.GetPages(currentUser.User.Id, cancellationToken);
        var page = pages.FirstOrDefault(p => p.Id == request.PageId)
                   ?? throw new HomePageNotFoundException("That page no longer exists.");

        await repository.DeletePage(page.Id, cancellationToken);

        // Compact tab order; the first remaining page inherits default if the default died.
        var remaining = pages.Where(p => p.Id != page.Id).OrderBy(p => p.Ordinal).ToArray();
        if (!remaining.Any()) return;
        var promoteDefault = page.IsDefault;
        await repository.UpdatePageOrder(remaining
            .Select((p, i) => (p.Id, i, p.IsDefault || (promoteDefault && i == 0)))
            .ToArray(), cancellationToken);
    }

    public async Task Handle(ReorderHomePageCommand request, CancellationToken cancellationToken)
    {
        var pages = (await repository.GetPages(currentUser.User.Id, cancellationToken))
            .OrderBy(p => p.Ordinal).ToList();
        var page = pages.FirstOrDefault(p => p.Id == request.PageId)
                   ?? throw new HomePageNotFoundException("That page no longer exists.");

        pages.Remove(page);
        pages.Insert(Math.Clamp(request.NewOrdinal, 0, pages.Count), page);
        await repository.UpdatePageOrder(pages.Select((p, i) => (p.Id, i, p.IsDefault)).ToArray(),
            cancellationToken);
    }

    public async Task Handle(SetDefaultHomePageCommand request, CancellationToken cancellationToken)
    {
        var pages = (await repository.GetPages(currentUser.User.Id, cancellationToken))
            .OrderBy(p => p.Ordinal).ToArray();
        if (pages.All(p => p.Id != request.PageId))
            throw new HomePageNotFoundException("That page no longer exists.");

        await repository.UpdatePageOrder(pages
            .Select((p, i) => (p.Id, i, p.Id == request.PageId))
            .ToArray(), cancellationToken);
    }

    public async Task Handle(SetHomePageMixCommand request, CancellationToken cancellationToken)
    {
        var page = await GetMyPage(request.PageId, cancellationToken);
        await repository.UpdatePage(page with { DefaultMix = request.DefaultMix }, cancellationToken);
    }

    public async Task<HomePageWidgetRecord> Handle(AddHomePageWidgetCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WidgetType) ||
            request.WidgetType.Length > HomePageWidgetRecord.MaxTypeLength)
            throw new HomePageValidationException("Widget type is required (max 64 characters).");
        ValidateSize(request.SizePreset);
        ValidateTitle(request.Title);
        ValidateConfig(request.ConfigJson);

        var page = await GetMyPage(request.PageId, cancellationToken);
        if (page.Widgets.Count >= HomePageRecord.MaxWidgetsPerPage)
            throw new HomePageCapReachedException(
                $"This page already has {HomePageRecord.MaxWidgetsPerPage} widgets — the current cap.");

        var widget = HomePageFactory.NewWidget(request.WidgetType, request.SizePreset, request.Title,
            request.ConfigJson, request.ConfigVersion, page.Widgets.Count);
        await repository.AddWidget(page.Id, widget, cancellationToken);
        return widget;
    }

    public async Task Handle(RemoveHomePageWidgetCommand request, CancellationToken cancellationToken)
    {
        var (page, widget) = await GetMyWidget(request.WidgetId, cancellationToken);
        await repository.RemoveWidget(widget.Id, cancellationToken);
        await repository.SetWidgetOrdinals(page.Widgets
            .Where(w => w.Id != widget.Id)
            .OrderBy(w => w.Ordinal)
            .Select((w, i) => (w.Id, i))
            .ToArray(), cancellationToken);
    }

    public async Task Handle(MoveHomePageWidgetCommand request, CancellationToken cancellationToken)
    {
        var (page, widget) = await GetMyWidget(request.WidgetId, cancellationToken);
        var ordered = page.Widgets.OrderBy(w => w.Ordinal).ToList();
        ordered.RemoveAll(w => w.Id == widget.Id);
        ordered.Insert(Math.Clamp(request.NewOrdinal, 0, ordered.Count), widget);
        await repository.SetWidgetOrdinals(ordered.Select((w, i) => (w.Id, i)).ToArray(),
            cancellationToken);
    }

    public async Task Handle(ResizeHomePageWidgetCommand request, CancellationToken cancellationToken)
    {
        ValidateSize(request.SizePreset);
        var (_, widget) = await GetMyWidget(request.WidgetId, cancellationToken);
        await repository.UpdateWidget(widget with { SizePreset = request.SizePreset }, cancellationToken);
    }

    public async Task Handle(RenameHomePageWidgetCommand request, CancellationToken cancellationToken)
    {
        ValidateTitle(request.Title);
        var (_, widget) = await GetMyWidget(request.WidgetId, cancellationToken);
        await repository.UpdateWidget(widget with { Title = request.Title }, cancellationToken);
    }

    public async Task Handle(UpdateHomePageWidgetConfigCommand request, CancellationToken cancellationToken)
    {
        ValidateConfig(request.ConfigJson);
        var (_, widget) = await GetMyWidget(request.WidgetId, cancellationToken);
        await repository.UpdateWidget(
            widget with { ConfigJson = request.ConfigJson, ConfigVersion = request.ConfigVersion },
            cancellationToken);
    }

    public async Task Handle(ReplaceHomePageWidgetsCommand request, CancellationToken cancellationToken)
    {
        if (request.Widgets.Count > HomePageRecord.MaxWidgetsPerPage)
            throw new HomePageCapReachedException(
                $"A page holds at most {HomePageRecord.MaxWidgetsPerPage} widgets — this import has {request.Widgets.Count}.");
        foreach (var spec in request.Widgets)
        {
            if (string.IsNullOrWhiteSpace(spec.WidgetType) ||
                spec.WidgetType.Length > HomePageWidgetRecord.MaxTypeLength)
                throw new HomePageValidationException("Widget type is required (max 64 characters).");
            ValidateSize(spec.SizePreset);
            ValidateTitle(spec.Title);
            ValidateConfig(spec.ConfigJson);
        }

        var page = await GetMyPage(request.PageId, cancellationToken);
        await repository.ReplaceWidgets(page.Id, request.Widgets
            .Select((spec, i) => HomePageFactory.NewWidget(spec.WidgetType, spec.SizePreset, spec.Title,
                spec.ConfigJson, spec.ConfigVersion, i))
            .ToArray(), cancellationToken);
    }

    private async Task<HomePageRecord> GetMyPage(Guid pageId, CancellationToken cancellationToken)
    {
        var pages = await repository.GetPages(currentUser.User.Id, cancellationToken);
        return pages.FirstOrDefault(p => p.Id == pageId)
               ?? throw new HomePageNotFoundException("That page no longer exists.");
    }

    private async Task<(HomePageRecord Page, HomePageWidgetRecord Widget)> GetMyWidget(Guid widgetId,
        CancellationToken cancellationToken)
    {
        var pages = await repository.GetPages(currentUser.User.Id, cancellationToken);
        foreach (var page in pages)
        {
            var widget = page.Widgets.FirstOrDefault(w => w.Id == widgetId);
            if (widget != null) return (page, widget);
        }

        throw new HomePageNotFoundException("That widget no longer exists.");
    }

    private static void ValidateName(string name)
    {
        if (name.Length > HomePageRecord.MaxNameLength)
            throw new HomePageValidationException("Page names cap at 64 characters.");
    }

    private static void ValidateSize(string sizePreset)
    {
        if (string.IsNullOrWhiteSpace(sizePreset) || sizePreset.Length > HomePageWidgetRecord.MaxSizeLength)
            throw new HomePageValidationException("Size preset is required (max 8 characters).");
    }

    private static void ValidateTitle(string? title)
    {
        if (title != null && title.Length > HomePageWidgetRecord.MaxTitleLength)
            throw new HomePageValidationException("Widget titles cap at 64 characters.");
    }

    private static void ValidateConfig(string configJson)
    {
        if (configJson == null || configJson.Length > HomePageWidgetRecord.MaxConfigLength)
            throw new HomePageValidationException("Widget configuration caps at 2,000 characters.");
    }
}

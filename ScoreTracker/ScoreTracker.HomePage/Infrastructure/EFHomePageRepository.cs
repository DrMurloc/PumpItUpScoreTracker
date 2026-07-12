using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.HomePage.Domain;
using ScoreTracker.HomePage.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.HomePage.Infrastructure;

internal sealed class EFHomePageRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    : IHomePageRepository
{
    public async Task<IReadOnlyList<HomePageRecord>> GetPages(Guid userId, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var pages = await database.Set<HomePageEntity>()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Ordinal)
            .ToArrayAsync(cancellationToken);
        var pageIds = pages.Select(p => p.Id).ToArray();
        var widgets = (await database.Set<HomePageWidgetEntity>()
                .Where(w => pageIds.Contains(w.PageId))
                .ToArrayAsync(cancellationToken))
            .ToLookup(w => w.PageId);
        return pages.Select(p => ToRecord(p, widgets[p.Id])).ToArray();
    }

    public async Task CreatePage(Guid userId, HomePageRecord page, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Set<HomePageEntity>().AddAsync(new HomePageEntity
        {
            Id = page.Id,
            UserId = userId,
            Name = page.Name,
            Ordinal = (byte)page.Ordinal,
            IsDefault = page.IsDefault,
            DefaultMixId = page.DefaultMix == null ? null : MixIds.For(page.DefaultMix.Value)
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePage(HomePageRecord page, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<HomePageEntity>().SingleAsync(p => p.Id == page.Id, cancellationToken);
        entity.Name = page.Name;
        entity.Ordinal = (byte)page.Ordinal;
        entity.IsDefault = page.IsDefault;
        entity.DefaultMixId = page.DefaultMix == null ? null : MixIds.For(page.DefaultMix.Value);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePageOrder(IReadOnlyList<(Guid PageId, int Ordinal, bool IsDefault)> pages,
        CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var ids = pages.Select(p => p.PageId).ToArray();
        var entities = await database.Set<HomePageEntity>()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        foreach (var (pageId, ordinal, isDefault) in pages)
        {
            entities[pageId].Ordinal = (byte)ordinal;
            entities[pageId].IsDefault = isDefault;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePage(Guid pageId, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Set<HomePageWidgetEntity>().Where(w => w.PageId == pageId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<HomePageEntity>().Where(p => p.Id == pageId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddWidget(Guid pageId, HomePageWidgetRecord widget, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Set<HomePageWidgetEntity>().AddAsync(new HomePageWidgetEntity
        {
            Id = widget.Id,
            PageId = pageId,
            WidgetType = widget.WidgetType,
            Title = widget.Title,
            Ordinal = (byte)widget.Ordinal,
            SizePreset = widget.SizePreset,
            ConfigJson = widget.ConfigJson,
            ConfigVersion = widget.ConfigVersion
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWidget(HomePageWidgetRecord widget, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<HomePageWidgetEntity>()
            .SingleAsync(w => w.Id == widget.Id, cancellationToken);
        entity.Title = widget.Title;
        entity.SizePreset = widget.SizePreset;
        entity.ConfigJson = widget.ConfigJson;
        entity.ConfigVersion = widget.ConfigVersion;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveWidget(Guid widgetId, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Set<HomePageWidgetEntity>().Where(w => w.Id == widgetId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SetWidgetOrdinals(IReadOnlyList<(Guid WidgetId, int Ordinal)> ordinals,
        CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var ids = ordinals.Select(o => o.WidgetId).ToArray();
        var entities = await database.Set<HomePageWidgetEntity>()
            .Where(w => ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);
        foreach (var (widgetId, ordinal) in ordinals) entities[widgetId].Ordinal = (byte)ordinal;
        await database.SaveChangesAsync(cancellationToken);
    }

    private static HomePageRecord ToRecord(HomePageEntity page, IEnumerable<HomePageWidgetEntity> widgets)
    {
        return new HomePageRecord(page.Id, page.Name, page.Ordinal, page.IsDefault,
            page.DefaultMixId == null ? null : MixIds.ToEnum(page.DefaultMixId.Value),
            widgets.OrderBy(w => w.Ordinal)
                .Select(w => new HomePageWidgetRecord(w.Id, w.WidgetType, w.Title, w.Ordinal, w.SizePreset,
                    w.ConfigJson, w.ConfigVersion))
                .ToArray());
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFChartPresenceRepository : IChartPresenceRepository
{
    private const char DotSeparator = ';';

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartPresenceRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Replace(MixEnum mix, ChartPresenceCensusResult census, DateTimeOffset computedAt,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // One transaction: the deletes run immediately rather than riding SaveChanges, so outside one a
        // page read mid-insert would find no census, and a failed insert would leave none until the next run.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Set<ChartPumbilityPresenceEntity>().Where(e => e.MixId == mixId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ChartPumbilityPresenceColumnEntity>().Where(e => e.MixId == mixId)
            .ExecuteDeleteAsync(cancellationToken);

        await database.Set<ChartPumbilityPresenceColumnEntity>().AddRangeAsync(census.Columns.Select(c =>
            new ChartPumbilityPresenceColumnEntity
            {
                MixId = mixId,
                ColumnOrder = c.Order,
                Band = c.Band.ToString(),
                Players = c.Players,
                SitePlayers = c.SitePlayers,
                ComputedAt = computedAt
            }), cancellationToken);
        await database.Set<ChartPumbilityPresenceEntity>().AddRangeAsync(census.Rows.Select(r =>
            new ChartPumbilityPresenceEntity
            {
                MixId = mixId,
                ChartId = r.ChartId,
                ColumnOrder = r.Column,
                CountsBoardPlayers = r.CountsBoardPlayers,
                Holders = r.Holders,
                SpotMin = r.Spots?.Min,
                SpotP25 = r.Spots?.P25,
                SpotMedian = r.Spots?.Median,
                SpotP75 = r.Spots?.P75,
                SpotMax = r.Spots?.Max,
                Dots = r.Dots.Count == 0
                    ? null
                    : string.Join(DotSeparator, r.Dots.Select(d => d.ToString(CultureInfo.InvariantCulture))),
                FolderSpots = r.FolderSpots,
                FolderP25 = r.Folder?.P25,
                FolderMedian = r.Folder?.Median,
                FolderP75 = r.Folder?.P75
            }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ChartPresenceColumns> GetColumns(MixEnum mix, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<ChartPumbilityPresenceColumnEntity>()
            .Where(e => e.MixId == mixId)
            .OrderBy(e => e.ColumnOrder)
            .ToArrayAsync(cancellationToken);
        return new ChartPresenceColumns(
            rows.Select(e => new ChartPresenceColumnRow(e.ColumnOrder, Name.From(e.Band), e.Players, e.SitePlayers))
                .ToArray(),
            rows.Length == 0 ? null : rows[0].ComputedAt);
    }

    public async Task<IReadOnlyList<ChartPresenceRow>> GetRows(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<ChartPumbilityPresenceEntity>()
            .Where(e => e.MixId == mixId && e.ChartId == chartId)
            .OrderBy(e => e.ColumnOrder)
            .ToArrayAsync(cancellationToken);
        return rows.Select(e => new ChartPresenceRow(e.ChartId, e.ColumnOrder, e.CountsBoardPlayers, e.Holders,
                e.SpotMin is { } min && e.SpotP25 is { } p25 && e.SpotMedian is { } median && e.SpotP75 is { } p75 &&
                e.SpotMax is { } max
                    ? new PumbilitySpots(min, p25, median, p75, max)
                    : null,
                e.Dots == null
                    ? Array.Empty<double>()
                    : e.Dots.Split(DotSeparator).Select(d => double.Parse(d, CultureInfo.InvariantCulture)).ToArray(),
                e.FolderSpots,
                e.FolderP25 is { } folderP25 && e.FolderMedian is { } folderMedian && e.FolderP75 is { } folderP75
                    ? new PumbilitySpotBox(folderP25, folderMedian, folderP75)
                    : null))
            .ToArray();
    }
}

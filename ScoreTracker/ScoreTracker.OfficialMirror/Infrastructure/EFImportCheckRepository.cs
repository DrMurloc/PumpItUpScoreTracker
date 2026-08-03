using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFImportCheckRepository : IImportCheckRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFImportCheckRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(ImportCheckRun run, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ImportCheckRunEntity>().AddAsync(new ImportCheckRunEntity
        {
            Id = run.Id,
            UserId = run.UserId,
            MixId = MixIds.For(run.Mix),
            RanAt = run.RanAt,
            Kind = KindToken(run.Kind),
            OfficialPumbility = run.OfficialPumbility,
            LocalPumbility = run.LocalPumbility,
            OfficialPasses = run.OfficialPasses,
            LocalPasses = run.LocalPasses,
            Findings = JsonSerializer.Serialize(run.Findings)
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<ImportCheckRun?> GetLatest(Guid userId, MixEnum mix, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<ImportCheckRunEntity>().AsNoTracking()
            .Where(r => r.UserId == userId && r.MixId == mixId)
            .OrderByDescending(r => r.RanAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null
            ? null
            : new ImportCheckRun(entity.Id, entity.UserId, mix, entity.RanAt, ParseKind(entity.Kind),
                entity.OfficialPumbility, entity.LocalPumbility, entity.OfficialPasses, entity.LocalPasses,
                JsonSerializer.Deserialize<CensusFinding[]>(entity.Findings) ?? Array.Empty<CensusFinding>());
    }

    public async Task<int> CountDeepScansInMonth(Guid userId, DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // A calendar month, not a rolling window: "3 a month, resets on the 1st" is a rule a
        // player can hold in their head, and the panel can name the date the next one unlocks.
        var start = new DateTimeOffset(asOf.Year, asOf.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        var deep = KindToken(ImportCheckKind.Deep);
        // Not per mix — the limit protects piugame from repeated full walks, and both mixes are
        // the same site.
        return await database.Set<ImportCheckRunEntity>().AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.Kind == deep && r.RanAt >= start && r.RanAt < end,
                cancellationToken);
    }

    private static string KindToken(ImportCheckKind kind)
    {
        return kind == ImportCheckKind.Deep ? "deep" : "census";
    }

    private static ImportCheckKind ParseKind(string token)
    {
        return token == "deep" ? ImportCheckKind.Deep : ImportCheckKind.Census;
    }
}

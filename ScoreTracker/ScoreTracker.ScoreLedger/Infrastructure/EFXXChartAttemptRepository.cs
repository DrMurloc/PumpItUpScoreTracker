using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFXXChartAttemptRepository : IXXChartAttemptRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFXXChartAttemptRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<XXChartAttempt?> GetBestAttempt(Guid userId, Chart chart,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var chartId = chart.Id;
        var mixId = MixIds.For(chart.Mix);
        return await (
            from ba in database.Set<BestAttemptEntity>()
            where ba.ChartId == chartId && ba.UserId == userId && ba.MixId == mixId
            select new XXChartAttempt(Enum.Parse<XXLetterGrade>(ba.LetterGrade), ba.IsBroken, ba.Score, ba.RecordedDate)
        ).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RemoveBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var chartId = chart.Id;
        var mixId = MixIds.For(chart.Mix);

        var previousBest = await database.Set<BestAttemptEntity>()
            .Where(ba => ba.ChartId == chartId && ba.UserId == userId && ba.MixId == mixId)
            .SingleOrDefaultAsync(cancellationToken);

        if (previousBest != null)
        {
            database.Set<BestAttemptEntity>().Remove(previousBest);
            await database.SaveChangesAsync(cancellationToken);
        }
    }


    public async Task SetBestAttempt(Guid userId, Chart chart, XXChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var chartId = chart.Id;
        var mixId = MixIds.For(chart.Mix);

        var previousBest = await database.Set<BestAttemptEntity>()
            .Where(ba => ba.ChartId == chartId && ba.UserId == userId && ba.MixId == mixId)
            .SingleOrDefaultAsync(cancellationToken);
        if (previousBest == null)
        {
            var entity = new BestAttemptEntity
            {
                ChartId = chartId,
                Id = Guid.NewGuid(),
                IsBroken = attempt.IsBroken,
                LetterGrade = attempt.LetterGrade.ToString(),
                RecordedDate = recordedOn,
                UserId = userId,
                MixId = mixId,
                Score = attempt.Score
            };
            await database.Set<BestAttemptEntity>().AddAsync(entity, cancellationToken);
        }
        else
        {
            previousBest.LetterGrade = attempt.LetterGrade.ToString();
            previousBest.RecordedDate = recordedOn;
            previousBest.IsBroken = attempt.IsBroken;
            previousBest.Score = attempt.Score;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, IEnumerable<Chart> charts,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var result = (from ce in charts
                join s in database.Song on (string)ce.Song.Name equals s.Name
                join c in database.Chart on new
                        { SongId = s.Id, Level = (int)ce.Level, ChartType = ce.Type.ToString() } equals
                    new { c.SongId, c.Level, ChartType = c.Type }
                join _ in database.Set<BestAttemptEntity>() on
                    new { UserId = userId, ChartId = c.Id, MixId = MixIds.For(ce.Mix) } equals new
                        { _.UserId, _.ChartId, _.MixId } into gi
                from ba in gi.DefaultIfEmpty()
                select new BestXXChartAttempt(
                    new Chart(c.Id, ce.Mix,
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration,
                            s.Artist ?? "Unknown",
                            Bpm.From(s.MinBpm, s.MaxBpm)),
                        Enum.Parse<ChartType>(c.Type), c.Level, MixEnum.XX, c.StepArtist, null,
                        new HashSet<Skill>()),
                    ba == null
                        ? null
                        : new XXChartAttempt(Enum.Parse<XXLetterGrade>(ba.LetterGrade), ba.IsBroken, ba.Score,
                            ba.RecordedDate)))
            .ToArray();
        return result;
    }

    public async Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, MixEnum mix,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Whole-catalog view with the user's attempts for ONE mix left-joined in.
        // OriginalMix maps through MixIds, not Enum.Parse(Mix.Name): legacy mix names
        // ("Prex 3", "OBG SE") are display strings, not enum identifiers.
        var mixId = MixIds.For(mix);
        return await (
                from s in database.Song
                join c in database.Chart on s.Id equals c.SongId
                join _ in database.Set<BestAttemptEntity>() on
                    new { UserId = userId, ChartId = c.Id, MixId = mixId } equals new
                        { _.UserId, _.ChartId, _.MixId } into gi
                from ba in gi.DefaultIfEmpty()
                select new BestXXChartAttempt(
                    new Chart(c.Id, MixIds.ToEnum(c.OriginalMixId),
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration,
                            s.Artist ?? "Unknown",
                            Bpm.From(s.MinBpm, s.MaxBpm)),
                        Enum.Parse<ChartType>(c.Type), c.Level, mix, c.StepArtist, null,
                        new HashSet<Skill>()),
                    ba == null
                        ? null
                        : new XXChartAttempt(Enum.Parse<XXLetterGrade>(ba.LetterGrade), ba.IsBroken, ba.Score,
                            ba.RecordedDate)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var attempts = await database.Set<BestAttemptEntity>().Where(b => b.UserId == userId).ToArrayAsync(cancellationToken);
        database.Set<BestAttemptEntity>().RemoveRange(attempts);
        await database.SaveChangesAsync(cancellationToken);
    }
}

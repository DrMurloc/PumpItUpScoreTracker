﻿using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFXXChartAttemptRepository : IXXChartAttemptRepository
{
    private readonly ChartAttemptDbContext _database;

    public EFXXChartAttemptRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _database = factory.CreateDbContext();
    }

    public async Task<XXChartAttempt?> GetBestAttempt(Guid userId, Chart chart,
        CancellationToken cancellationToken = default)
    {
        var chartId = chart.Id;
        return await (
            from ba in _database.BestAttempt
            where ba.ChartId == chartId && ba.UserId == userId
            select new XXChartAttempt(Enum.Parse<XXLetterGrade>(ba.LetterGrade), ba.IsBroken, ba.Score, ba.RecordedDate)
        ).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RemoveBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default)
    {
        var chartId = chart.Id;

        var previousBest = await _database.BestAttempt.Where(ba => ba.ChartId == chartId && ba.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (previousBest != null)
        {
            _database.BestAttempt.Remove(previousBest);
            await _database.SaveChangesAsync(cancellationToken);
        }
    }


    public async Task SetBestAttempt(Guid userId, Chart chart, XXChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default)
    {
        var chartId = chart.Id;

        var previousBest = await _database.BestAttempt.Where(ba => ba.ChartId == chartId && ba.UserId == userId)
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
                Score = attempt.Score
            };
            await _database.BestAttempt.AddAsync(entity, cancellationToken);
        }
        else
        {
            previousBest.LetterGrade = attempt.LetterGrade.ToString();
            previousBest.RecordedDate = recordedOn;
            previousBest.IsBroken = attempt.IsBroken;
            previousBest.Score = attempt.Score;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    public Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId, IEnumerable<Chart> charts,
        CancellationToken cancellationToken)
    {
        var result = (from ce in charts
                join s in _database.Song on (string)ce.Song.Name equals s.Name
                join c in _database.Chart on new
                        { SongId = s.Id, Level = (int)ce.Level, ChartType = ce.Type.ToString() } equals
                    new { c.SongId, c.Level, ChartType = c.Type }
                join _ in _database.BestAttempt on new { UserId = userId, ChartId = c.Id } equals new
                    { _.UserId, _.ChartId } into gi
                from ba in gi.DefaultIfEmpty()
                select new BestXXChartAttempt(
                    new Chart(c.Id, ce.Mix,
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration,
                            s.Artist ?? "Unknown",
                            Bpm.From(s.MinBpm, s.MaxBpm)),
                        Enum.Parse<ChartType>(c.Type), c.Level, MixEnum.XX, c.StepArtist, c.Level, null,
                        new HashSet<Skill>()),
                    ba == null
                        ? null
                        : new XXChartAttempt(Enum.Parse<XXLetterGrade>(ba.LetterGrade), ba.IsBroken, ba.Score,
                            ba.RecordedDate)))
            .ToArray();
        return Task.FromResult<IEnumerable<BestXXChartAttempt>>(result);
    }

    public async Task<IEnumerable<BestXXChartAttempt>> GetBestAttempts(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await (
                from s in _database.Song
                join c in _database.Chart on s.Id equals c.SongId
                join m in _database.Mix on c.OriginalMixId equals m.Id
                join _ in _database.BestAttempt on new { UserId = userId, ChartId = c.Id } equals new
                    { _.UserId, _.ChartId } into gi
                from ba in gi.DefaultIfEmpty()
                select new BestXXChartAttempt(
                    new Chart(c.Id, Enum.Parse<MixEnum>(m.Name),
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration,
                            s.Artist ?? "Unknown",
                            Bpm.From(s.MinBpm, s.MaxBpm)),
                        Enum.Parse<ChartType>(c.Type), c.Level, MixEnum.XX, c.StepArtist, c.Level, null,
                        new HashSet<Skill>()),
                    ba == null
                        ? null
                        : new XXChartAttempt(Enum.Parse<XXLetterGrade>(ba.LetterGrade), ba.IsBroken, ba.Score,
                            ba.RecordedDate)))
            .ToArrayAsync(cancellationToken);
    }
}
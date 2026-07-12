using MediatR;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.Randomizer.Contracts.Queries;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.Randomizer.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Infrastructure
{
    internal sealed class EFDrawRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IDateTimeOffsetAccessor clock) : IDrawRepository,
        IRequestHandler<GetDrawBySlugQuery, DrawDto?>,
        IRequestHandler<GetTournamentDrawsQuery, IEnumerable<DrawDto>>
    {
        public async Task<DrawDto> ReplacePersonalDraw(Guid userId, MixEnum mix, IReadOnlyList<Guid> chartIds,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
            if (draw == null)
            {
                draw = new RandomizerDrawEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Slug = Guid.NewGuid(),
                    CreatedAt = clock.Now
                };
                await database.Set<RandomizerDrawEntity>().AddAsync(draw, cancellationToken);
            }
            else
            {
                var oldCards = await database.Set<RandomizerDrawCardEntity>()
                    .Where(c => c.DrawId == draw.Id).ToArrayAsync(cancellationToken);
                database.Set<RandomizerDrawCardEntity>().RemoveRange(oldCards);
            }

            draw.Mix = mix.ToString();
            draw.UpdatedAt = clock.Now;
            await AddCards(database, draw.Id, chartIds, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return await Load(database, draw.Id, cancellationToken)
                   ?? throw new InvalidOperationException("Draw vanished during creation.");
        }

        public async Task<DrawDto> CreateTournamentDraw(Guid tournamentId, string name, MixEnum mix,
            IReadOnlyList<Guid> chartIds, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draw = new RandomizerDrawEntity
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Name = name,
                Slug = Guid.NewGuid(),
                Mix = mix.ToString(),
                CreatedAt = clock.Now,
                UpdatedAt = clock.Now
            };
            await database.Set<RandomizerDrawEntity>().AddAsync(draw, cancellationToken);
            await AddCards(database, draw.Id, chartIds, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return await Load(database, draw.Id, cancellationToken)
                   ?? throw new InvalidOperationException("Draw vanished during creation.");
        }

        public async Task<DrawDto> RedrawCards(Guid drawId, MixEnum mix, IReadOnlyList<Guid> chartIds,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstAsync(d => d.Id == drawId, cancellationToken);
            var oldCards = await database.Set<RandomizerDrawCardEntity>()
                .Where(c => c.DrawId == drawId).ToArrayAsync(cancellationToken);
            database.Set<RandomizerDrawCardEntity>().RemoveRange(oldCards);
            draw.Mix = mix.ToString();
            draw.UpdatedAt = clock.Now;
            await AddCards(database, drawId, chartIds, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return await Load(database, drawId, cancellationToken)
                   ?? throw new InvalidOperationException("Draw vanished during redraw.");
        }

        public async Task RenameDraw(Guid drawId, string name, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstOrDefaultAsync(d => d.Id == drawId, cancellationToken);
            if (draw == null) return;

            // Deliberately no UpdatedAt touch: the match list is recency-ordered and a
            // rename shouldn't shuffle it.
            draw.Name = name;
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task SetCardState(Guid drawId, Guid pullId, DrawCardState state,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var card = await database.Set<RandomizerDrawCardEntity>()
                .FirstOrDefaultAsync(c => c.DrawId == drawId && c.PullId == pullId, cancellationToken);
            if (card == null) return;

            card.State = state.ToString();
            await Touch(database, drawId, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<DrawDto> ClearVetoed(Guid drawId, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var cards = await database.Set<RandomizerDrawCardEntity>()
                .Where(c => c.DrawId == drawId).ToArrayAsync(cancellationToken);
            var vetoed = cards.Where(c => c.State == nameof(DrawCardState.Vetoed)).ToArray();
            database.Set<RandomizerDrawCardEntity>().RemoveRange(vetoed);
            var order = 1;
            foreach (var card in cards.Except(vetoed).OrderBy(c => c.Order)) card.Order = order++;

            await Touch(database, drawId, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return await Load(database, drawId, cancellationToken)
                   ?? throw new InvalidOperationException("Draw vanished during compaction.");
        }

        public async Task<DrawDto> AddChart(Guid drawId, Guid chartId, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var maxOrder = await database.Set<RandomizerDrawCardEntity>()
                .Where(c => c.DrawId == drawId)
                .Select(c => (int?)c.Order).MaxAsync(cancellationToken) ?? 0;
            await database.Set<RandomizerDrawCardEntity>().AddAsync(new RandomizerDrawCardEntity
            {
                PullId = Guid.NewGuid(),
                DrawId = drawId,
                ChartId = chartId,
                Order = maxOrder + 1,
                State = nameof(DrawCardState.None)
            }, cancellationToken);
            await Touch(database, drawId, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return await Load(database, drawId, cancellationToken)
                   ?? throw new InvalidOperationException("Draw vanished while adding a chart.");
        }

        public async Task<DrawDto?> GetDraw(Guid drawId, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            return await Load(database, drawId, cancellationToken);
        }

        public async Task<DrawDto?> GetActiveDraw(Guid? userId, Guid? tournamentId,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            // Personal: the one rolling draw. Tournament: the most recently touched match.
            var draw = tournamentId != null
                ? await database.Set<RandomizerDrawEntity>()
                    .Where(d => d.TournamentId == tournamentId)
                    .OrderByDescending(d => d.UpdatedAt)
                    .FirstOrDefaultAsync(cancellationToken)
                : await database.Set<RandomizerDrawEntity>()
                    .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
            return draw == null ? null : await Load(database, draw.Id, cancellationToken);
        }

        public async Task<DrawDto?> GetBySlug(Guid slug, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstOrDefaultAsync(d => d.Slug == slug, cancellationToken);
            return draw == null ? null : await Load(database, draw.Id, cancellationToken);
        }

        public async Task<IEnumerable<DrawDto>> GetTournamentDraws(Guid tournamentId,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draws = await database.Set<RandomizerDrawEntity>()
                .Where(d => d.TournamentId == tournamentId)
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => d.Id)
                .ToArrayAsync(cancellationToken);
            var results = new List<DrawDto>();
            foreach (var id in draws)
            {
                var dto = await Load(database, id, cancellationToken);
                if (dto != null) results.Add(dto);
            }

            return results;
        }

        public async Task DeleteDraw(Guid drawId, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstOrDefaultAsync(d => d.Id == drawId, cancellationToken);
            if (draw == null) return;

            database.Set<RandomizerDrawEntity>().Remove(draw);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<DrawDto?> Handle(GetDrawBySlugQuery request, CancellationToken cancellationToken)
        {
            return await GetBySlug(request.Slug, cancellationToken);
        }

        public async Task<IEnumerable<DrawDto>> Handle(GetTournamentDrawsQuery request,
            CancellationToken cancellationToken)
        {
            return await GetTournamentDraws(request.TournamentId, cancellationToken);
        }

        private static async Task AddCards(ChartAttemptDbContext database, Guid drawId,
            IReadOnlyList<Guid> chartIds, CancellationToken cancellationToken)
        {
            await database.Set<RandomizerDrawCardEntity>().AddRangeAsync(chartIds.Select((chartId, i) =>
                new RandomizerDrawCardEntity
                {
                    PullId = Guid.NewGuid(),
                    DrawId = drawId,
                    ChartId = chartId,
                    Order = i + 1,
                    State = nameof(DrawCardState.None)
                }), cancellationToken);
        }

        private async Task Touch(ChartAttemptDbContext database, Guid drawId, CancellationToken cancellationToken)
        {
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstOrDefaultAsync(d => d.Id == drawId, cancellationToken);
            if (draw != null) draw.UpdatedAt = clock.Now;
        }

        private static async Task<DrawDto?> Load(ChartAttemptDbContext database, Guid drawId,
            CancellationToken cancellationToken)
        {
            var draw = await database.Set<RandomizerDrawEntity>()
                .FirstOrDefaultAsync(d => d.Id == drawId, cancellationToken);
            if (draw == null) return null;

            var cards = await database.Set<RandomizerDrawCardEntity>()
                .Where(c => c.DrawId == drawId)
                .OrderBy(c => c.Order)
                .ToArrayAsync(cancellationToken);
            return new DrawDto(draw.Id, draw.Slug,
                Enum.TryParse<MixEnum>(draw.Mix, out var mix) ? mix : MixEnum.Phoenix,
                draw.TournamentId,
                cards.Select(c => new DrawCardDto(c.PullId, c.ChartId, c.Order,
                    Enum.TryParse<DrawCardState>(c.State, out var state) ? state : DrawCardState.None)).ToArray(),
                draw.Name);
        }
    }
}

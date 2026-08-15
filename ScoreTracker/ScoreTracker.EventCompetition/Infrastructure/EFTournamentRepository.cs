using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.EventCompetition.Infrastructure
{
    internal sealed class EFTournamentRepository : ITournamentRepository,
        IRequestHandler<GetTournamentRolesQuery, IEnumerable<UserTournamentRole>>,
        IRequestHandler<GetMyTournamentsQuery, IEnumerable<TournamentRoleListing>>
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IChartRepository _charts;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IDateTimeOffsetAccessor _dateTime;

        public EFTournamentRepository(IMemoryCache memoryCache, IChartRepository charts,
            IDbContextFactory<ChartAttemptDbContext> factory, ICurrentUserAccessor currentUser,
            IDateTimeOffsetAccessor dateTime)
        {
            _factory = factory;
            _memoryCache = memoryCache;
            _charts = charts;
            _currentUser = currentUser;
            _dateTime = dateTime;
        }

        // Internal: EFMoMRepository busts the listing cache when the cycle creates or prunes
        // a season.
        internal static readonly string TourneyCacheKey = $@"{nameof(EFTournamentRepository)}_Tournies";
        private static string TourneyIdCacheKey(Guid id) => $@"{nameof(EFTournamentRepository)}_Tourney_{id}";

        /// <summary>
        ///     Reconstructs the display name a legacy tournament row carried: the franchise
        ///     prefix unless the season name already starts with it, and the chart-type suffix
        ///     for quarterly or multi-board seasons — the two off-grid single-board seasons
        ///     ("March of Murlocs Practice", "March of Murlocs") never had one.
        /// </summary>
        private static string BoardDisplayName(MoMSeasonEntity season, MoMBoardEntity board, int seasonBoardCount)
        {
            var baseName = season.Name.StartsWith("March of Murlocs", StringComparison.OrdinalIgnoreCase)
                ? season.Name
                : $"March of Murlocs {season.Name}";
            return season.Quarter == null && seasonBoardCount == 1
                ? baseName
                : $"{baseName} - {(ChartType)board.ChartType}s";
        }

        private bool IsCurrent(MoMSeasonEntity season)
        {
            var now = _dateTime.Now;
            return season.StartsAt <= now && season.EndsAt > now;
        }

        private static Uri? ParseUri(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
        }

        public async Task<IEnumerable<TournamentRecord>> GetAllTournaments(CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(TourneyCacheKey, async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);

                // Unlisted micro-tournaments are invisible here by design — every existing
                // consumer (nav, /Tournaments, the API) wants the curated listing. Role
                // holders reach them through GetMyTournamentsQuery instead. MoM rows on the
                // legacy table are excluded: they were copied onto the MoM* tables (Slice 2)
                // and the boards below are their living successors. No live non-MoM
                // tournament carries sessions, so the count column is MoM-only.
                var tournaments = (await database.Set<TournamentEntity>()
                        .Where(t => !t.IsUnlisted && !t.IsMoM)
                        .ToArrayAsync(cancellationToken)).Select(t =>
                    new TournamentRecord(t.Id,
                        t.Name,
                        0,
                        Enum.Parse<TournamentType>(t.Type),
                        t.Location, t.IsHighlighted,
                        ParseUri(t.LinkOverride),
                        t.StartDate,
                        t.EndDate,
                        false));

                var seasons = await database.Set<MoMSeasonEntity>().ToArrayAsync(cancellationToken);
                var boards = await database.Set<MoMBoardEntity>().ToArrayAsync(cancellationToken);
                var counts = await database.Set<MoMSessionEntity>()
                    .Where(s => s.PublishedAt != null)
                    .GroupBy(s => s.BoardId)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
                var boardsPerSeason = boards.GroupBy(b => b.SeasonId).ToDictionary(g => g.Key, g => g.Count());

                var boardRecords = from b in boards
                    join s in seasons on b.SeasonId equals s.Id
                    select new TournamentRecord(b.Id,
                        BoardDisplayName(s, b, boardsPerSeason[s.Id]),
                        counts.TryGetValue(b.Id, out var count) ? count : 0,
                        TournamentType.Stamina,
                        "Remote",
                        IsCurrent(s),
                        null,
                        s.StartsAt,
                        s.EndsAt,
                        true);

                return tournaments.Concat(boardRecords).ToArray();
            });
        }

        public async Task<TournamentConfiguration> GetTournament(Guid id, CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(TourneyIdCacheKey(id), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);

                var board = await database.Set<MoMBoardEntity>()
                    .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
                if (board != null) return await BoardConfiguration(database, board, cancellationToken);

                var result = await database.Set<TournamentEntity>().Where(t => t.Id == id).SingleAsync(cancellationToken);
                var snapshots = await GetScoringLevelSnapshot(id, cancellationToken);
                return JsonSerializer.Deserialize<TournamentConfigurationJsonEntity>(result.Configuration)
                           ?.To(snapshots) ??
                       throw new Exception($"Tournament {id} was not configured properly");
            });
        }

        private async Task<TournamentConfiguration> BoardConfiguration(ChartAttemptDbContext database,
            MoMBoardEntity board, CancellationToken cancellationToken)
        {
            var season = await database.Set<MoMSeasonEntity>()
                .SingleAsync(s => s.Id == board.SeasonId, cancellationToken);
            var boardCount = await database.Set<MoMBoardEntity>()
                .CountAsync(b => b.SeasonId == board.SeasonId, cancellationToken);
            // Delta rows only (§9.3): a chart with no row scores at folder level + 0.5, which
            // is exactly what the scoring fallback does for a missing key — sparse is exact.
            var snapshot = await database.Set<MoMChartLevelEntity>()
                .Where(l => l.SeasonId == board.SeasonId && l.MixId == board.MixId)
                .ToDictionaryAsync(l => l.ChartId, l => l.Level, cancellationToken);
            var json = JsonSerializer.Deserialize<TournamentConfigurationJsonEntity>(board.ScoringConfig)
                       ?? throw new InvalidOperationException(
                           $"MoM board {board.Id} has no scoring configuration");
            var frozen = json.To(snapshot);
            // The board pins the mix, so grading follows the board rather than defaulting to
            // Phoenix (§2.3) — a no-op for every Phoenix board, load-bearing once P2 boards
            // exist (Slice 5).
            frozen.Scoring.Mix = MixIds.ToEnum(board.MixId);

            // MaxTime and AllowRepeats come from the frozen config itself — every stored board
            // carries 1h45m / no-repeats today, and reading them back means a session always
            // replays under exactly the rules it was recorded under.
            return new TournamentConfiguration(board.Id,
                BoardDisplayName(season, board, boardCount),
                frozen.Scoring,
                IsCurrent(season),
                true)
            {
                StartDate = season.StartsAt,
                EndDate = season.EndsAt,
                MaxTime = frozen.MaxTime,
                AllowRepeats = frozen.AllowRepeats
            };
        }

        public async Task CreateOrSaveTournament(TournamentConfiguration tournament,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.Set<TournamentEntity>().FirstOrDefaultAsync(t => t.Id == tournament.Id, cancellationToken);
            if (entity == null)
            {
                await database.Set<TournamentEntity>().AddAsync(new TournamentEntity
                {
                    Id = tournament.Id,
                    Configuration = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(tournament)),
                    EndDate = tournament.EndDate,
                    StartDate = tournament.StartDate,
                    IsHighlighted = tournament.IsHighlighted,
                    IsMoM = tournament.IsMom,
                    Name = tournament.Name
                }, cancellationToken);
            }
            else
            {
                entity.Name = tournament.Name;
                entity.EndDate = tournament.EndDate;
                entity.IsHighlighted = tournament.IsHighlighted;
                entity.StartDate = tournament.StartDate;
                entity.IsMoM = tournament.IsMom;
                entity.Configuration = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(tournament));
            }

            await database.SaveChangesAsync(cancellationToken);
            _memoryCache.Remove(TourneyCacheKey);
            _memoryCache.Remove(TourneyIdCacheKey(tournament.Id));
        }

        public async Task CreateOrSaveTournament(TournamentRecord tournament, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.Set<TournamentEntity>().Where(t => t.Id == tournament.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await database.Set<TournamentEntity>().AddAsync(new TournamentEntity
                {
                    Configuration = "{}",
                    EndDate = tournament.EndDate,
                    Id = tournament.Id,
                    IsHighlighted = tournament.IsHighlighted,
                    IsMoM = tournament.IsMoM,
                    LinkOverride = tournament.LinkOverride?.ToString(),
                    Location = tournament.Location,
                    Name = tournament.Name,
                    StartDate = tournament.StartDate,
                    Type = tournament.Type.ToString()
                }, cancellationToken);
            }
            else
            {
                entity.EndDate = tournament.EndDate;
                entity.Id = tournament.Id;
                entity.IsHighlighted = tournament.IsHighlighted;
                entity.IsMoM = tournament.IsMoM;
                entity.LinkOverride = tournament.LinkOverride?.ToString();
                entity.Location = tournament.Location;
                entity.Name = tournament.Name;
                entity.StartDate = tournament.StartDate;
                entity.Type = tournament.Type.ToString();
                entity.IsMoM = tournament.IsMoM;
            }

            await database.SaveChangesAsync(cancellationToken);
            _memoryCache.Remove(TourneyCacheKey);
            _memoryCache.Remove(TourneyIdCacheKey(tournament.Id));
        }

        public async Task SaveSession(TournamentSession session, CancellationToken cancellationToken)
        {
            _memoryCache.Remove(TourneyCacheKey);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);

            // The submit flow edits "the" session — until Slice 4b's draft/publish lifecycle,
            // that is the user's latest on the board (D16 allows many; this UI makes one).
            var entity = await database.Set<MoMSessionEntity>()
                .Where(s => s.BoardId == session.TournamentId && s.UserId == session.UsersId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (!session.Entries.Any())
            {
                // Saving an emptied session is the delete (D17) — chart rows cascade.
                if (entity != null)
                {
                    database.Set<MoMSessionEntity>().Remove(entity);
                    await database.SaveChangesAsync(cancellationToken);
                }

                _memoryCache.Remove(CacheKey(session.TournamentId, session.UsersId));
                return;
            }

            var board = await database.Set<MoMBoardEntity>()
                .SingleAsync(b => b.Id == session.TournamentId, cancellationToken);
            var mix = MixIds.ToEnum(board.MixId);
            var snapshot = await database.Set<MoMChartLevelEntity>()
                .Where(l => l.SeasonId == board.SeasonId && l.MixId == board.MixId)
                .ToDictionaryAsync(l => l.ChartId, l => l.Level, cancellationToken);

            var now = _dateTime.Now;
            if (entity == null)
            {
                entity = new MoMSessionEntity
                {
                    Id = Guid.NewGuid(),
                    BoardId = session.TournamentId,
                    UserId = session.UsersId,
                    CreatedAt = now,
                    // Publish-on-save: this flow has no draft concept and today's board ranks
                    // a session the moment it is saved. NULL starts meaning draft in 4b.
                    PublishedAt = now
                };
                await database.Set<MoMSessionEntity>().AddAsync(entity, cancellationToken);
            }

            // Everything below PublishedAt is a derived cache of the chart rows (§6),
            // recomputed wholesale on every save. Balanced level is the season snapshot's
            // override where one exists, folder level + 0.5 where none does (§9.3).
            entity.UpdatedAt = now;
            entity.TotalScore = session.TotalScore;
            entity.ChartsPlayed = session.Entries.Count;
            entity.RestTime = session.CurrentRestTime.Ticks;
            entity.AverageDifficulty = session.Entries.Average(e =>
                snapshot.TryGetValue(e.Chart.Id, out var balanced) ? balanced : (int)e.Chart.Level + 0.5);
            entity.AverageGrade = session.Entries.Average(e => (int)e.Score.LetterGradeFor(mix));
            entity.LowestLevel = (byte)session.Entries.Min(e => (int)e.Chart.Level);
            entity.HighestLevel = (byte)session.Entries.Max(e => (int)e.Chart.Level);
            entity.VideoUrl = session.VideoUrl?.ToString();

            var existingCharts = await database.Set<MoMSessionChartEntity>()
                .Where(c => c.SessionId == entity.Id).ToArrayAsync(cancellationToken);
            database.Set<MoMSessionChartEntity>().RemoveRange(existingCharts);
            await database.Set<MoMSessionChartEntity>().AddRangeAsync(session.Entries.Select(
                (e, ordinal) => new MoMSessionChartEntity
                {
                    SessionId = entity.Id,
                    Ordinal = ordinal,
                    ChartId = e.Chart.Id,
                    Score = e.Score,
                    Plate = e.Plate.ToString(),
                    IsBroken = e.IsBroken,
                    SessionScore = e.SessionScore,
                    BonusPoints = e.BonusPoints
                }), cancellationToken);

            await database.SaveChangesAsync(cancellationToken);
            _memoryCache.Remove(CacheKey(session.TournamentId, session.UsersId));
        }

        private string CacheKey(Guid tournamentId, Guid userId)
        {
            return $"{nameof(EFTournamentRepository)}__Tournament:{tournamentId}__User:{userId}";
        }

        public async Task<TournamentSession> GetSession(Guid tournamentId, Guid userId,
            CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(CacheKey(tournamentId, userId), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(1);

                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                var tournamentConfig = await GetTournament(tournamentId, cancellationToken);
                // The board pins the mix (set in BoardConfiguration), so the session follows it.
                var mix = tournamentConfig.Scoring.Mix;
                var entity = await database.Set<MoMSessionEntity>()
                    .Where(s => s.BoardId == tournamentId && s.UserId == userId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (entity == null) return new TournamentSession(userId, tournamentConfig, mix);

                var chartRows = await database.Set<MoMSessionChartEntity>()
                    .Where(c => c.SessionId == entity.Id)
                    .OrderBy(c => c.Ordinal)
                    .ToArrayAsync(cancellationToken);
                var charts = (await _charts.GetCharts(mix,
                    chartIds: chartRows.Select(c => c.ChartId).Distinct().ToArray(),
                    cancellationToken: cancellationToken)).ToDictionary(c => c.Id);

                var session = new TournamentSession(userId, tournamentConfig, mix);
                foreach (var row in chartRows)
                    session.Add(charts[row.ChartId], row.Score, Enum.Parse<PhoenixPlate>(row.Plate), row.IsBroken);
                session.VideoUrl = ParseUri(entity.VideoUrl);

                return session;
            });
        }

        public async Task<IEnumerable<LeaderboardRecord>> GetLeaderboardRecords(Guid tournamentId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var config = await GetTournament(tournamentId, cancellationToken);
            var mix = config.Scoring.Mix;

            // Boards rank published sessions, not players (D16); the earliest publication
            // wins a tie (§1). Drafts are never on a board.
            var sessions = await (from s in database.Set<MoMSessionEntity>()
                    join u in database.User on s.UserId equals u.Id
                    where s.BoardId == tournamentId && s.PublishedAt != null
                    select new { Entity = s, u.Name })
                .ToArrayAsync(cancellationToken);
            if (!sessions.Any()) return Array.Empty<LeaderboardRecord>();

            // One query for every session's chart rows and one catalog read for every chart —
            // the old shape re-read the config and charts per player, an N+1 on each render.
            var sessionIds = sessions.Select(x => x.Entity.Id).ToArray();
            var chartRows = (await database.Set<MoMSessionChartEntity>()
                    .Where(c => sessionIds.Contains(c.SessionId))
                    .ToArrayAsync(cancellationToken))
                .GroupBy(c => c.SessionId)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Ordinal).ToArray());
            var charts = (await _charts.GetCharts(mix,
                    chartIds: chartRows.Values.SelectMany(rows => rows.Select(c => c.ChartId)).Distinct().ToArray(),
                    cancellationToken: cancellationToken)).ToDictionary(c => c.Id);

            return sessions
                .OrderByDescending(x => x.Entity.TotalScore)
                .ThenBy(x => x.Entity.PublishedAt)
                .Select((x, index) =>
                {
                    var session = new TournamentSession(x.Entity.UserId, config, mix);
                    foreach (var row in chartRows[x.Entity.Id])
                        session.Add(charts[row.ChartId], row.Score, Enum.Parse<PhoenixPlate>(row.Plate),
                            row.IsBroken);

                    return new LeaderboardRecord(index + 1, x.Entity.UserId, x.Name, x.Entity.TotalScore,
                        TimeSpan.FromTicks(x.Entity.RestTime), x.Entity.AverageDifficulty, x.Entity.ChartsPlayed,
                        ParseUri(x.Entity.VideoUrl))
                    {
                        Session = session
                    };
                }).ToArray();
        }

        private string SnapshotCacheKey(Guid tournamentId)
        {
            return $"{nameof(EFTournamentRepository)}__{tournamentId}__LevelSnapshot";
        }

        public async Task<IDictionary<Guid, double>?> GetScoringLevelSnapshot(Guid tournamentId,
            CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(SnapshotCacheKey(tournamentId), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                var board = await database.Set<MoMBoardEntity>()
                    .FirstOrDefaultAsync(b => b.Id == tournamentId, cancellationToken);
                // Snapshots were always a MoM concern; a non-board id has none.
                if (board == null) return (IDictionary<Guid, double>?)null;

                // Delta rows only (§9.3): a missing chart means folder level + 0.5, which the
                // scoring fallback already produces — sparse is exact, not approximate.
                return await database.Set<MoMChartLevelEntity>()
                    .Where(l => l.SeasonId == board.SeasonId && l.MixId == board.MixId)
                    .ToDictionaryAsync(l => l.ChartId, l => l.Level, cancellationToken);
            });
        }

        public async Task SetRole(Guid tournamentId, Guid userId, TournamentRole role,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity =
                await database.Set<TournamentRoleEntity>().FirstOrDefaultAsync(
                    e => e.TournamentId == tournamentId && e.UserId == userId, cancellationToken);
            if (entity == null)
                await database.Set<TournamentRoleEntity>().AddAsync(new TournamentRoleEntity
                {
                    Role = role.ToString(),
                    TournamentId = tournamentId,
                    UserId = userId
                }, cancellationToken);
            else
                entity.Role = role.ToString();
            await database.SaveChangesAsync(cancellationToken);
            _memoryCache.Remove(TourneyRolesCacheKey(tournamentId));
        }

        public async Task RevokeRole(Guid tournamentId, Guid userId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var removingEntities = await
                database.Set<TournamentRoleEntity>().Where(e => e.TournamentId == tournamentId && e.UserId == userId)
                    .ToArrayAsync(cancellationToken);
            database.Set<TournamentRoleEntity>().RemoveRange(removingEntities);
            await database.SaveChangesAsync(cancellationToken);
            _memoryCache.Remove(TourneyRolesCacheKey(tournamentId));
        }

        public async Task SetDiscordChannel(Guid tournamentId, ulong? channelId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.Set<TournamentEntity>()
                .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
            if (entity == null) return;

            entity.DiscordChannelId = channelId;
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<ulong?> GetDiscordChannel(Guid tournamentId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.Set<TournamentEntity>().Where(t => t.Id == tournamentId)
                .Select(t => t.DiscordChannelId)
                .FirstOrDefaultAsync(cancellationToken);
        }


        private static string TourneyRolesCacheKey(Guid tournamentId)
        {
            return $@"{nameof(EFTournamentRepository)}_TourneyRoles_{tournamentId}";
        }

        public async Task<IEnumerable<UserTournamentRole>> Handle(GetTournamentRolesQuery request,
            CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(TourneyRolesCacheKey(request.TournamentId), async o =>
            {
                o.AbsoluteExpiration = DateTime.Now + TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                return await database.Set<TournamentRoleEntity>().Where(t => t.TournamentId == request.TournamentId)
                    .Select(t => new UserTournamentRole(t.TournamentId, t.UserId, Enum.Parse<TournamentRole>(t.Role)))
                    .ToArrayAsync(cancellationToken);
            });
        }

        public async Task<IEnumerable<TournamentRoleListing>> Handle(GetMyTournamentsQuery request,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) return Array.Empty<TournamentRoleListing>();

            var userId = _currentUser.User.Id;
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await (from r in database.Set<TournamentRoleEntity>()
                        join t in database.Set<TournamentEntity>() on r.TournamentId equals t.Id
                        where r.UserId == userId
                        select new { t.Id, t.Name, r.Role, t.IsUnlisted })
                    .ToArrayAsync(cancellationToken))
                .Select(x => new TournamentRoleListing(x.Id, x.Name, Enum.Parse<TournamentRole>(x.Role),
                    x.IsUnlisted))
                .ToArray();
        }

        public async Task CreateUnlistedTournament(TournamentRecord tournament, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            await database.Set<TournamentEntity>().AddAsync(new TournamentEntity
            {
                Id = tournament.Id,
                Configuration = "{}",
                Name = tournament.Name,
                Type = tournament.Type.ToString(),
                Location = tournament.Location,
                IsHighlighted = false,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                IsMoM = false,
                IsUnlisted = true
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            // Not in the listed cache, but clear it anyway in case the flag is ever flipped by hand.
            _memoryCache.Remove(TourneyCacheKey);
        }

        public async Task<Guid> CreateRoleInvite(Guid tournamentId, TournamentRole role, DateTimeOffset? expiresAt,
            Guid createdBy, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var token = Guid.NewGuid();
            await database.Set<TournamentRoleInviteEntity>().AddAsync(new TournamentRoleInviteEntity
            {
                Token = token,
                TournamentId = tournamentId,
                Role = role.ToString(),
                ExpiresAt = expiresAt,
                CreatedBy = createdBy
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            return token;
        }

        public async Task<TournamentRoleInviteRecord?> GetRoleInvite(Guid token, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.Set<TournamentRoleInviteEntity>()
                .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
            return entity == null
                ? null
                : new TournamentRoleInviteRecord(entity.Token, entity.TournamentId,
                    Enum.Parse<TournamentRole>(entity.Role), entity.ExpiresAt);
        }

        public async Task<IEnumerable<TournamentRoleInviteRecord>> GetRoleInvites(Guid tournamentId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await database.Set<TournamentRoleInviteEntity>()
                    .Where(i => i.TournamentId == tournamentId)
                    .ToArrayAsync(cancellationToken))
                .Select(i => new TournamentRoleInviteRecord(i.Token, i.TournamentId,
                    Enum.Parse<TournamentRole>(i.Role), i.ExpiresAt))
                .ToArray();
        }

        public async Task DeleteRoleInvite(Guid token, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entities = await database.Set<TournamentRoleInviteEntity>()
                .Where(i => i.Token == token).ToArrayAsync(cancellationToken);
            database.Set<TournamentRoleInviteEntity>().RemoveRange(entities);
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}

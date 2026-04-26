using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFQualifiersRepository : IQualifiersRepository
    {
        private readonly IChartRepository _charts;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        private static readonly ISet<Guid> ChartIds = new HashSet<Guid>(new[]
        {
            new Guid("1D1606A0-BC43-417D-8867-B574D6F3E92C"),
            new Guid("E2D622A3-ED44-456E-8572-29DA5AA90F92"),
            new Guid("0FD50D96-1F0C-4CB0-A179-9282132EF9BB"),
            new Guid("41DCE283-0C6B-4899-96DD-50CE10DC49B9"),
            new Guid("99E9BED2-3C4A-47E3-A058-ACCAE532F117"),
            new Guid("8501B01A-8D67-4CAF-AEA2-5AD0206A6255")
        });

        private static readonly IDictionary<Guid, int> Modifiers = new Dictionary<Guid, int>()
        {
            { new Guid("41DCE283-0C6B-4899-96DD-50CE10DC49B9"), 106 },
        };

        public EFQualifiersRepository(IChartRepository charts, IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _charts = charts;
            _factory = factory;
        }

        public async Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Name userName,
            QualifiersConfiguration config,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var nameString = userName.ToString();
            var entity =
                await database.UserQualifier.FirstOrDefaultAsync(
                    u => u.TournamentId == tournamentId && u.Name == nameString, cancellationToken);
            return entity == null ? null : From(entity, config);
        }

        public async Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Guid userId, QualifiersConfiguration config,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity =
                await database.UserQualifier.FirstOrDefaultAsync(
                    u => u.TournamentId == tournamentId && u.UserId == userId, cancellationToken);
            return entity == null ? null : From(entity, config);
        }

        private static UserQualifiers From(UserQualifierEntity entity, QualifiersConfiguration config)
        {
            var entries = JsonSerializer.Deserialize<QualifierSubmissionDto[]>(entity.Entries);
            return new UserQualifiers(config, entity.IsApproved, entity.Name, entity.UserId, entries!.ToDictionary(
                e => e.ChartId, e =>
                    new UserQualifiers.Submission
                    {
                        ChartId = e.ChartId,
                        PhotoUrl = e.PhotoUrl == null ? null : new Uri(e.PhotoUrl),
                        Score = e.Score
                    }));
        }

        public async Task SaveQualifiers(Guid tournamentId, UserQualifiers qualifiers,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var nameString = qualifiers.UserName.ToString();
            var userIdEntity = qualifiers.UserId == null
                ? null
                : await database.UserQualifier.FirstOrDefaultAsync(
                    u => u.TournamentId == tournamentId && u.UserId == qualifiers.UserId, cancellationToken);

            var entity = userIdEntity ??
                         await database.UserQualifier.FirstOrDefaultAsync(
                             u => u.TournamentId == tournamentId && u.Name == nameString, cancellationToken);
            var entryJson = JsonSerializer.Serialize(qualifiers.Submissions.Select(kv => new QualifierSubmissionDto
            {
                ChartId = kv.Value.ChartId,
                PhotoUrl = kv.Value.PhotoUrl?.ToString(),
                Score = kv.Value.Score
            }));
            if (entity == null)
            {
                await database.AddAsync(new UserQualifierEntity
                {
                    TournamentId = tournamentId,
                    Id = Guid.NewGuid(),
                    UserId = qualifiers.UserId,
                    Entries = entryJson,
                    IsApproved = qualifiers.IsApproved,
                    Name = nameString
                }, cancellationToken);
            }
            else
            {
                entity.IsApproved = qualifiers.IsApproved;
                entity.Entries = entryJson;
                if (entity.UserId == null && qualifiers.UserId != null) entity.UserId = qualifiers.UserId;
                entity.Name = nameString;
            }

            await database.UserQualifierHistory.AddAsync(new UserQualifierHistoryEntity
            {
                TournamentId = tournamentId,
                Id = Guid.NewGuid(),
                Entries = entryJson,
                IsApproved = qualifiers.IsApproved,
                Name = nameString,
                RecordedDate = DateTimeOffset.Now
            }, cancellationToken);

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<string>> GetMissing(Guid tournamentId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var history = await database.UserQualifierHistory.Where(uqh => uqh.TournamentId == tournamentId)
                .ToArrayAsync(cancellationToken);
            var existing = (await database.UserQualifier.Where(uq => uq.TournamentId == tournamentId)
                    .Select(u => u.Name).Distinct().ToArrayAsync(cancellationToken))
                .ToHashSet();
            return history.Select(h => h.Name).Distinct().Where(h => !existing.Contains(h)).ToArray();
        }

        public async Task Restore(Guid tournamentId, string name, CancellationToken cancellationToken)
        {
            var config = await GetQualifiersConfiguration(tournamentId, cancellationToken);
            var charts = config.Charts.Select(c => c.Id).ToHashSet();
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var history = await database.UserQualifierHistory
                .Where(uqh => uqh.TournamentId == tournamentId && uqh.Name == name)
                .ToArrayAsync(cancellationToken);

            var user = new UserQualifiers(config, true, name, null, new Dictionary<Guid, UserQualifiers.Submission>());
            var entries = history.SelectMany(g =>
                    JsonSerializer.Deserialize<QualifierSubmissionDto[]>(g.Entries) ??
                    throw new Exception("You done goofed"))
                .ToArray();
            var maxes = entries.Where(e => charts.Contains(e.ChartId)).GroupBy(e => e.ChartId)
                .Select(g => g.MaxBy(e => e.Score));


            foreach (var max in maxes)

                user.AddPhoenixScore(max.ChartId, max.Score,
                    max.PhotoUrl == null ? null : new Uri(max.PhotoUrl, UriKind.RelativeOrAbsolute));

            await SaveQualifiers(tournamentId, user, cancellationToken);
        }

        public async Task FixLeaderboard(Guid tournamentId, CancellationToken cancellationToken = default)
        {
            var config = await GetQualifiersConfiguration(tournamentId, cancellationToken);
            var charts = config.Charts.Select(c => c.Id).ToHashSet();
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var history = await database.UserQualifierHistory.Where(uqh => uqh.TournamentId == tournamentId)
                .ToArrayAsync(cancellationToken);
            foreach (var userGroup in history.GroupBy(u => u.Name))
            {
                var existing = await GetQualifiers(tournamentId, userGroup.Key, config, cancellationToken);
                if (existing == null) continue;
                var entries = userGroup.SelectMany(g =>
                        JsonSerializer.Deserialize<QualifierSubmissionDto[]>(g.Entries) ??
                        throw new Exception("You done goofed"))
                    .ToArray();
                var maxes = entries.Where(e => charts.Contains(e.ChartId)).GroupBy(e => e.ChartId)
                    .Select(g => g.MaxBy(e => e.Score));

                var needsSaved = false;

                foreach (var max in maxes)
                    if (!existing.Submissions.ContainsKey(max.ChartId) ||
                        existing.Submissions[max.ChartId].Score < max.Score)
                    {
                        existing.AddPhoenixScore(max.ChartId, max.Score,
                            max.PhotoUrl == null ? null : new Uri(max.PhotoUrl, UriKind.RelativeOrAbsolute));
                        needsSaved = true;
                    }

                if (needsSaved) await SaveQualifiers(tournamentId, existing, cancellationToken);
            }
        }

        public async Task<IEnumerable<UserQualifiers>> GetAllUserQualifiers(Guid tournamentId,
            QualifiersConfiguration config,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entities = await database.UserQualifier.Where(e => e.TournamentId == tournamentId)
                .ToArrayAsync(cancellationToken);
            return entities.Select(e => From(e, config)).ToArray();
        }

        public async Task<QualifiersConfiguration> GetQualifiersConfiguration(Guid tournamentId,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var config =
                await database.QualifiersConfiguration.FirstOrDefaultAsync(e => e.TournamentId == tournamentId,
                    cancellationToken);
            if (config == null)
            {
                var charts = await _charts.GetCharts(MixEnum.Phoenix, chartIds: ChartIds,
                    cancellationToken: cancellationToken);
                return new QualifiersConfiguration(charts, Modifiers, "Phoenix", 1164337603034759278, 2, null, false);
            }

            var chartIds = config.Charts.Split(",").Select(c => new Guid(c));
            var charts2 =
                config.AllCharts
                    ? await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken)
                    : await _charts.GetCharts(MixEnum.Phoenix, chartIds: chartIds,
                        cancellationToken: cancellationToken);
            return new QualifiersConfiguration(charts2, Modifiers, config.ScoringType, config.NotificationChannel,
                config.ChartPlayCount, config.CutoffTime, config.AllCharts);
        }

        public async Task SaveTeam(Guid tournamentId, CoOpTeam team, CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var playerNames = new[] { team.Player2.Tag.ToString(), team.Player1.Tag.ToString() };
            var individualPlayers =
                await database.CoOpPlayers.Where(p => playerNames.Contains(p.PlayerName)).ToArrayAsync(
                    cancellationToken);
            database.CoOpPlayers.RemoveRange(individualPlayers);
            await database.CoOpPlayers.AddRangeAsync(new[]
            {
                new CoOpPlayerEntity
                {
                    PlayerName = team.Player1.Tag,
                    CoOpTitle = team.Player1.HighestCoOpTitle,
                    DifficultyTitle = team.Player1.HighestStandardTitle,
                    IsInTeam = true,
                    TournamentId = tournamentId
                },
                new CoOpPlayerEntity
                {
                    PlayerName = team.Player2.Tag,
                    CoOpTitle = team.Player2.HighestCoOpTitle,
                    DifficultyTitle = team.Player2.HighestStandardTitle,
                    IsInTeam = true,
                    TournamentId = tournamentId
                }
            }, cancellationToken);
            var teamName = team.TeamName.ToString();
            var existingTeam =
                await database.CoOpTeam.FirstOrDefaultAsync(
                    t => t.TournamentId == tournamentId && t.TeamName == teamName, cancellationToken);
            if (existingTeam == null)
            {
                await database.CoOpTeam.AddAsync(new CoOpTeamEntity
                {
                    Player1Name = team.Player1.Tag,
                    Player2Name = team.Player2.Tag,
                    Seed = team.Seed,
                    TeamName = team.TeamName,
                    TournamentId = tournamentId
                }, cancellationToken);
            }
            else
            {
                existingTeam.Seed = team.Seed;
                existingTeam.Player1Name = team.Player1.Tag;
                existingTeam.Player2Name = team.Player2.Tag;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveIndividualPlayer(Guid tournamentId, CoOpPlayer player,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var playerName = player.Tag.ToString();
            var entity =
                await database.CoOpPlayers.FirstOrDefaultAsync(
                    p => p.TournamentId == tournamentId && p.PlayerName == playerName, cancellationToken);
            if (entity == null)
            {
                await database.CoOpPlayers.AddAsync(new CoOpPlayerEntity
                {
                    TournamentId = tournamentId,
                    CoOpTitle = player.HighestCoOpTitle,
                    DifficultyTitle = player.HighestStandardTitle,
                    IsInTeam = false,
                    PlayerName = playerName
                }, cancellationToken);
            }
            else
            {
                entity.IsInTeam = false;
                entity.CoOpTitle = player.HighestCoOpTitle;
                entity.DifficultyTitle = player.HighestStandardTitle;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<CoOpPlayer>> GetIndividualCoopPlayers(Guid tournamentId,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await database.CoOpPlayers.Where(c => !c.IsInTeam)
                    .ToArrayAsync(cancellationToken))
                .Select(e => new CoOpPlayer(e.PlayerName, e.CoOpTitle, e.DifficultyTitle))
                .ToArray();
        }

        public async Task<IEnumerable<CoOpTeam>> GetCoOpTeams(Guid tournamentId,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await (from t in database.CoOpTeam
                    join p1 in database.CoOpPlayers on new { t.TournamentId, Name = t.Player1Name } equals new
                        { p1.TournamentId, Name = p1.PlayerName }
                    join p2 in database.CoOpPlayers on new { t.TournamentId, Name = t.Player2Name } equals new
                        { p2.TournamentId, Name = p2.PlayerName }
                    where t.TournamentId == tournamentId
                    select new CoOpTeam(t.TeamName, new CoOpPlayer(p1.PlayerName, p1.CoOpTitle, p1.DifficultyTitle),
                        new CoOpPlayer(p2.PlayerName, p2.CoOpTitle, p2.DifficultyTitle), t.Seed)
                ).ToArrayAsync(cancellationToken);
        }

        public async Task RegisterUserToTournament(Guid tournamentId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);

            var existing =
                await database.UserTournamentRegistration.FirstOrDefaultAsync(
                    t => t.TournamentId == tournamentId && t.UserId == userId, cancellationToken);

            if (existing == null)
            {
                await database.UserTournamentRegistration.AddAsync(new UserTournamentRegistrationEntity
                {
                    TournamentId = tournamentId,
                    UserId = userId
                }, cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<Guid>> GetRegisteredUsers(Guid tournamentId,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);

            return await database.UserTournamentRegistration.Where(t => t.TournamentId == tournamentId)
                .Select(e => e.UserId)
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetRegisteredTournaments(Guid userId,
            CancellationToken cancellationToken = default)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);

            return await database.UserTournamentRegistration.Where(t => t.UserId == userId).Select(e => e.TournamentId)
                .ToArrayAsync(cancellationToken);
        }
    }
}

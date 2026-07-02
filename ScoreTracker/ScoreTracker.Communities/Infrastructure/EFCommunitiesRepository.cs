using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Infrastructure
{
    internal sealed class EFCommunitiesRepository : ICommunityRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly IPlayerStatsReader _playerStats;

        public EFCommunitiesRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IPlayerStatsReader playerStats)
        {
            _factory = factory;
            _playerStats = playerStats;
        }

        public async Task<Name?> GetCommunityByInviteCode(Guid inviteCode, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await (from ci in database.Set<CommunityInviteCodeEntity>()
                where ci.InviteCode == inviteCode
                join c in database.Set<CommunityEntity>() on ci.CommunityId equals c.Id
                select c.Name).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task SaveCommunity(Community community, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var communityString = community.Name.ToString();
            var entity =
                await database.Set<CommunityEntity>().FirstOrDefaultAsync(c => c.Name == communityString, cancellationToken);
            var communityId = entity?.Id;
            if (communityId == null || entity == null)
            {
                communityId = Guid.NewGuid();
                await database.Set<CommunityEntity>().AddAsync(new CommunityEntity
                {
                    Id = communityId.Value,
                    Name = communityString,
                    OwningUserId = community.OwnerId,
                    IsRegional = community.IsRegional,
                    PrivacyType = community.PrivacyType.ToString()
                }, cancellationToken);
            }
            else
            {
                entity.PrivacyType = community.PrivacyType.ToString();
                entity.OwningUserId = community.OwnerId;
                entity.IsRegional = community.IsRegional;
            }

            //Members
            var memberEntities = await database.Set<CommunityMembershipEntity>().Where(cm => cm.CommunityId == communityId.Value)
                .ToArrayAsync(cancellationToken);
            var existingSet = memberEntities.Select(m => m.UserId).Distinct().ToHashSet();
            var toDelete = memberEntities.Where(e => !community.MemberIds.Contains(e.UserId)).ToArray();
            var toCreate = community.MemberIds.Where(m => !existingSet.Contains(m)).Select(m =>
                new CommunityMembershipEntity
                {
                    Id = Guid.NewGuid(),
                    CommunityId = communityId.Value,
                    UserId = m
                }).ToArray();
            database.Set<CommunityMembershipEntity>().RemoveRange(toDelete);
            await database.Set<CommunityMembershipEntity>().AddRangeAsync(toCreate, cancellationToken);

            //Invite Codes
            var codeEntities = await database.Set<CommunityInviteCodeEntity>().Where(ci => ci.CommunityId == communityId.Value)
                .ToArrayAsync(cancellationToken);
            var existingCodeSet = codeEntities.Select(c => c.InviteCode).Distinct().ToHashSet();
            var deleteCodes = codeEntities.Where(e => !community.InviteCodes.ContainsKey(e.InviteCode)).ToArray();
            var createCodes = community.InviteCodes.Where(kv => !existingCodeSet.Contains(kv.Key)).Select(m =>
                new CommunityInviteCodeEntity
                {
                    InviteCode = m.Key,
                    CommunityId = communityId.Value,
                    ExpirationDate = m.Value?.ToDateTime(TimeOnly.MinValue)
                }).ToArray();
            database.Set<CommunityInviteCodeEntity>().RemoveRange(deleteCodes);
            await database.Set<CommunityInviteCodeEntity>().AddRangeAsync(createCodes, cancellationToken);

            //Channels
            var channelEntities = await database.Set<CommunityChannelEntity>().Where(cc => cc.CommunityId == communityId.Value)
                .ToArrayAsync(cancellationToken);
            var channelIdSet = channelEntities.Select(e => e.ChannelId).ToHashSet();
            var newStats = community.Channels.ToDictionary(c => c.ChannelId);
            foreach (var channelEntity in channelEntities)
            {
                if (!newStats.ContainsKey(channelEntity.ChannelId))
                {
                    database.Set<CommunityChannelEntity>().Remove(channelEntity);
                    continue;
                }

                channelEntity.SendNewMembers = newStats[channelEntity.ChannelId].SendNewMembers;
                channelEntity.SendNewScores = newStats[channelEntity.ChannelId].SendNewScores;
                channelEntity.SendTitles = newStats[channelEntity.ChannelId].SendTitles;
            }

            var toCreateChannels = newStats.Values.Where(s => !channelIdSet.Contains(s.ChannelId))
                .Select(s => new CommunityChannelEntity
                {
                    Id = Guid.NewGuid(),
                    ChannelId = s.ChannelId,
                    SendNewMembers = s.SendNewMembers,
                    SendNewScores = s.SendNewScores,
                    SendTitles = s.SendTitles,
                    CommunityId = communityId.Value
                }).ToArray();

            await database.Set<CommunityChannelEntity>().AddRangeAsync(toCreateChannels, cancellationToken);


            //Save
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<CommunityOverviewRecord>> GetCommunities(Guid userId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await (from cm in database.Set<CommunityMembershipEntity>()
                where cm.UserId == userId
                join c in database.Set<CommunityEntity>() on cm.CommunityId equals c.Id
                join members in database.Set<CommunityMembershipEntity>() on c.Id equals members.CommunityId
                group c by new { c.Name, c.IsRegional, c.PrivacyType, cm.UserId }
                into g
                select new
                {
                    g.Key.Name,
                    g.Key.IsRegional,
                    g.Key.PrivacyType,
                    Count = g.Count()
                }).ToArrayAsync(cancellationToken)).Select(g =>
                new CommunityOverviewRecord(g.Name, Enum.Parse<CommunityPrivacyType>(g.PrivacyType),
                    g.Count, g.IsRegional));
        }

        public async Task<IEnumerable<CommunityOverviewRecord>> GetPublicCommunities(
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var publicTypes = new[]
                { CommunityPrivacyType.Public.ToString(), CommunityPrivacyType.PublicWithCode.ToString() };
            return (await (from c in database.Set<CommunityEntity>()
                where publicTypes.Contains(c.PrivacyType)
                join members in database.Set<CommunityMembershipEntity>() on c.Id equals members.CommunityId
                group c by new { c.Name, c.PrivacyType, c.IsRegional }
                into g
                select new
                {
                    g.Key.Name,
                    g.Key.IsRegional,
                    g.Key.PrivacyType,
                    Count = g.Count()
                }).ToArrayAsync(cancellationToken)).Select(g =>
                new CommunityOverviewRecord(g.Name, Enum.Parse<CommunityPrivacyType>(g.PrivacyType),
                    g.Count, g.IsRegional));
        }

        public async Task<IEnumerable<CommunityLeaderboardRecord>> GetLeaderboard(Name communityName,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var nameString = communityName.ToString();
            var members = await (from c in database.Set<CommunityEntity>()
                    where c.Name == nameString
                    join cm in database.Set<CommunityMembershipEntity>() on c.Id equals cm.CommunityId
                    join u in database.User on cm.UserId equals u.Id
                    select new { u.Id, u.Name, u.IsPublic, u.ProfileImage })
                .ToArrayAsync(cancellationToken);

            // Stats come from PlayerProgress's published reader — its PlayerStats table is
            // vertical-internal, so no SQL join onto it from here. The reader returns rows
            // only for users that have stats, preserving the old inner-join semantics.
            var stats = (await _playerStats.GetStats(members.Select(m => m.Id).Distinct(), cancellationToken))
                .ToDictionary(s => s.UserId);

            return members.Where(m => stats.ContainsKey(m.Id))
                .Select(m =>
                {
                    var ps = stats[m.Id];
                    return new CommunityLeaderboardRecord(m.Name, m.IsPublic,
                        new Uri(m.ProfileImage, UriKind.Absolute), m.Id,
                        ps.TotalRating, ps.HighestLevel, ps.ClearCount,
                        ps.CoOpRating, ps.CoOpScore, ps.SkillRating, ps.SkillScore, ps.SkillLevel,
                        ps.SinglesRating, ps.SinglesScore, ps.SinglesLevel, ps.DoublesRating,
                        ps.DoublesScore, ps.DoublesLevel, ps.CompetitiveLevel, ps.SinglesCompetitiveLevel,
                        ps.DoublesCompetitiveLevel);
                }).ToArray();
        }

        public async Task<Community?> GetCommunityByName(Name communityName, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var nameString = communityName.ToString();
            var entity = await database.Set<CommunityEntity>().FirstOrDefaultAsync(c => c.Name == nameString, cancellationToken);
            if (entity == null) return null;

            var members = await database.Set<CommunityMembershipEntity>().Where(cm => cm.CommunityId == entity.Id)
                .ToArrayAsync(cancellationToken);
            var invites = await database.Set<CommunityInviteCodeEntity>().Where(cm => cm.CommunityId == entity.Id)
                .ToArrayAsync(cancellationToken);

            var channels = await database.Set<CommunityChannelEntity>().Where(cm => cm.CommunityId == entity.Id)
                .ToArrayAsync(cancellationToken);

            return new Community(entity.Name, entity.OwningUserId, Enum.Parse<CommunityPrivacyType>(entity.PrivacyType),
                members.Select(c => c.UserId),
                channels.Select(c =>
                    new Community.ChannelConfiguration(c.ChannelId, c.SendNewScores, c.SendTitles, c.SendNewMembers)),
                invites.ToDictionary(i => i.InviteCode,
                    i => i.ExpirationDate == null
                        ? null
                        : (DateOnly?)new DateOnly(i.ExpirationDate.Value.Year, i.ExpirationDate.Value.Month,
                            i.ExpirationDate.Value.Day)), entity.IsRegional);
        }
    }
}

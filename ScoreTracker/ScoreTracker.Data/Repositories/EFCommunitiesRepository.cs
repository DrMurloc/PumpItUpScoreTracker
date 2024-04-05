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
    public sealed class EFCommunitiesRepository : ICommunityRepository
    {
        private readonly ChartAttemptDbContext _dbContext;

        public EFCommunitiesRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _dbContext = factory.CreateDbContext();
        }

        public async Task<Name?> GetCommunityByInviteCode(Guid inviteCode, CancellationToken cancellationToken)
        {
            return await (from ci in _dbContext.CommunityInviteCode
                where ci.InviteCode == inviteCode
                join c in _dbContext.Community on ci.CommunityId equals c.Id
                select c.Name).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task SaveCommunity(Community community, CancellationToken cancellationToken)
        {
            var communityString = community.Name.ToString();
            var entity =
                await _dbContext.Community.FirstOrDefaultAsync(c => c.Name == communityString, cancellationToken);
            var communityId = entity?.Id;
            if (communityId == null || entity == null)
            {
                communityId = Guid.NewGuid();
                await _dbContext.Community.AddAsync(new CommunityEntity
                {
                    Id = communityId.Value,
                    Name = communityString,
                    OwningUserId = community.OwnerId,
                    PrivacyType = community.PrivacyType.ToString()
                }, cancellationToken);
            }
            else
            {
                entity.PrivacyType = community.PrivacyType.ToString();
                entity.OwningUserId = community.OwnerId;
            }

            //Members
            var memberEntities = await _dbContext.CommunityMembership.Where(cm => cm.CommunityId == communityId.Value)
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
            _dbContext.CommunityMembership.RemoveRange(toDelete);
            await _dbContext.CommunityMembership.AddRangeAsync(toCreate, cancellationToken);

            //Invite Codes
            var codeEntities = await _dbContext.CommunityInviteCode.Where(ci => ci.CommunityId == communityId.Value)
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
            _dbContext.CommunityInviteCode.RemoveRange(deleteCodes);
            await _dbContext.CommunityInviteCode.AddRangeAsync(createCodes, cancellationToken);

            //Channels
            var channelEntities = await _dbContext.CommunityChannel.Where(cc => cc.CommunityId == communityId.Value)
                .ToArrayAsync(cancellationToken);
            var channelIdSet = channelEntities.Select(e => e.ChannelId).ToHashSet();
            var newStats = community.Channels.ToDictionary(c => c.ChannelId);
            foreach (var channelEntity in channelEntities)
            {
                if (!newStats.ContainsKey(channelEntity.ChannelId))
                {
                    _dbContext.CommunityChannel.Remove(channelEntity);
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

            await _dbContext.CommunityChannel.AddRangeAsync(toCreateChannels, cancellationToken);


            //Save
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<CommunityOverviewRecord>> GetCommunities(Guid userId,
            CancellationToken cancellationToken)
        {
            return (await (from cm in _dbContext.CommunityMembership
                where cm.UserId == userId
                join c in _dbContext.Community on cm.CommunityId equals c.Id
                join members in _dbContext.CommunityMembership on c.Id equals members.CommunityId
                group c by new { c.Name, c.PrivacyType, cm.UserId }
                into g
                select new
                {
                    g.Key.Name,
                    g.Key.PrivacyType,
                    Count = g.Count()
                }).ToArrayAsync(cancellationToken)).Select(g =>
                new CommunityOverviewRecord(g.Name, Enum.Parse<CommunityPrivacyType>(g.PrivacyType),
                    g.Count));
        }

        public async Task<IEnumerable<CommunityOverviewRecord>> GetPublicCommunities(
            CancellationToken cancellationToken)
        {
            var publicTypes = new[]
                { CommunityPrivacyType.Public.ToString(), CommunityPrivacyType.PublicWithCode.ToString() };
            return (await (from c in _dbContext.Community
                where publicTypes.Contains(c.PrivacyType)
                join members in _dbContext.CommunityMembership on c.Id equals members.CommunityId
                group c by new { c.Name, c.PrivacyType }
                into g
                select new
                {
                    g.Key.Name,
                    g.Key.PrivacyType,
                    Count = g.Count()
                }).ToArrayAsync(cancellationToken)).Select(g =>
                new CommunityOverviewRecord(g.Name, Enum.Parse<CommunityPrivacyType>(g.PrivacyType),
                    g.Count));
        }

        public async Task<IEnumerable<CommunityLeaderboardRecord>> GetLeaderboard(Name communityName,
            CancellationToken cancellationToken)
        {
            var nameString = communityName.ToString();
            return await (from c in _dbContext.Community
                    where c.Name == nameString
                    join cm in _dbContext.CommunityMembership on c.Id equals cm.CommunityId
                    join ps in _dbContext.PlayerStats on cm.UserId equals ps.UserId
                    join u in _dbContext.User on ps.UserId equals u.Id
                    select new CommunityLeaderboardRecord(u.Name, new Uri(u.ProfileImage, UriKind.Absolute), u.Id,
                        ps.TotalRating, ps.HighestLevel, ps.ClearCount,
                        ps.CoOpRating, ps.AverageCoOpScore, ps.SkillRating, ps.AverageSkillScore, ps.AverageSkillLevel,
                        ps.SinglesRating, ps.AverageSinglesScore, ps.AverageSinglesLevel, ps.DoublesRating,
                        ps.AverageDoublesScore, ps.AverageDoublesLevel))
                .ToArrayAsync(cancellationToken);
        }

        public async Task<Community?> GetCommunityByName(Name communityName, CancellationToken cancellationToken)
        {
            var nameString = communityName.ToString();
            var entity = await _dbContext.Community.FirstOrDefaultAsync(c => c.Name == nameString, cancellationToken);
            if (entity == null) return null;

            var members = await _dbContext.CommunityMembership.Where(cm => cm.CommunityId == entity.Id)
                .ToArrayAsync(cancellationToken);
            var invites = await _dbContext.CommunityInviteCode.Where(cm => cm.CommunityId == entity.Id)
                .ToArrayAsync(cancellationToken);

            var channels = await _dbContext.CommunityChannel.Where(cm => cm.CommunityId == entity.Id)
                .ToArrayAsync(cancellationToken);

            return new Community(entity.Name, entity.OwningUserId, Enum.Parse<CommunityPrivacyType>(entity.PrivacyType),
                members.Select(c => c.UserId),
                channels.Select(c =>
                    new Community.ChannelConfiguration(c.ChannelId, c.SendNewScores, c.SendTitles, c.SendNewMembers)),
                invites.ToDictionary(i => i.InviteCode,
                    i => i.ExpirationDate == null
                        ? null
                        : (DateOnly?)new DateOnly(i.ExpirationDate.Value.Year, i.ExpirationDate.Value.Month,
                            i.ExpirationDate.Value.Day)));
        }
    }
}

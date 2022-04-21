using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFUserRepository : IUserRepository
{
    private readonly ChartAttemptDbContext _database;

    public EFUserRepository(ChartAttemptDbContext database)
    {
        _database = database;
    }

    public async Task SaveUser(User user, CancellationToken cancellationToken = default)
    {
        var existingUser = await _database.User.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
        if (existingUser == null)
            await _database.User.AddAsync(new UserEntity
            {
                Name = user.Name,
                Id = user.Id
            }, cancellationToken);
        else
            existingUser.Name = user.Name;

        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateDiscordLogin(Guid userId, ulong discordId, CancellationToken cancellationToken = default)
    {
        await _database.DiscordLogin.AddAsync(new DiscordLoginEntity
        {
            DiscordId = discordId,
            UserId = userId
        }, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task<User> GetUser(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _database.User.Where(u => u.Id == userId).Select(u => new User(u.Id, u.Name))
            .SingleAsync(cancellationToken);
    }

    public async Task<User?> GetUserByDiscordLogin(ulong discordId, CancellationToken cancellationToken = default)
    {
        return await (from dl in _database.DiscordLogin
            join u in _database.User on dl.UserId equals u.Id
            where dl.DiscordId == discordId
            select new User(u.Id, u.Name)).FirstOrDefaultAsync(cancellationToken);
    }
}
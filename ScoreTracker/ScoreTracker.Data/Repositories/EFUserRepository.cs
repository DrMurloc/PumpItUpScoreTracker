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
        {
            await _database.User.AddAsync(new UserEntity
            {
                Name = user.Name,
                Id = user.Id,
                IsPublic = user.IsPublic
            }, cancellationToken);
        }
        else
        {
            existingUser.Name = user.Name;
            existingUser.IsPublic = user.IsPublic;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateExternalLogin(Guid userId, string loginProviderName, string externalId,
        CancellationToken cancellationToken = default)
    {
        var entity = new ExternalLoginEntity
        {
            ExternalId = externalId,
            LoginProvider = loginProviderName,
            UserId = userId
        };
        await _database.ExternalLogin.AddAsync(entity, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> SearchForUsersByName(string searchText,
        CancellationToken cancellationToken = default)
    {
        return await _database.User.Where(u => u.Name.Contains(searchText))
            .Select(u => new User(u.Id, u.Name, u.IsPublic))
            .ToArrayAsync(cancellationToken);
    }


    public async Task<User> GetUser(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _database.User.Where(u => u.Id == userId).Select(u => new User(u.Id, u.Name, u.IsPublic))
            .SingleAsync(cancellationToken);
    }

    public async Task<User?> GetUserByExternalLogin(string loginProviderName, string externalId,
        CancellationToken cancellationToken = default)
    {
        return await (from e in _database.ExternalLogin
            join u in _database.User on e.UserId equals u.Id
            where e.LoginProvider == loginProviderName
                  && e.ExternalId == externalId
            select new User(u.Id, u.Name, u.IsPublic)).SingleOrDefaultAsync(cancellationToken);
    }
}
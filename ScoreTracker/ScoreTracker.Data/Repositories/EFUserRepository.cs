using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFUserRepository : IUserRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFUserRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IMemoryCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    public async Task SaveUser(User user, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existingUser = await database.User.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
        if (existingUser == null)
        {
            await database.User.AddAsync(new UserEntity
            {
                Name = user.Name,
                Id = user.Id,
                IsPublic = user.IsPublic,
                GameTag = user.GameTag,
                ProfileImage = user.ProfileImage.ToString(),
                CountryName = user.Country
            }, cancellationToken);
        }
        else
        {
            existingUser.Name = user.Name;
            existingUser.IsPublic = user.IsPublic;
            existingUser.GameTag = user.GameTag;
            existingUser.CountryName = user.Country;
            existingUser.ProfileImage = user.ProfileImage.ToString();
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private string FeedbackCache(Guid userId)
    {
        return $"{nameof(EFUserRepository)}_Feedback_{userId}";
    }

    public async Task SaveFeedback(Guid userId, SuggestionFeedbackRecord feedback,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.SuggestionFeedback.AddAsync(new SuggestionFeedbackEntity
        {
            Id = Guid.NewGuid(),
            ChartId = feedback.ChartId,
            FeedbackCategory = feedback.FeedbackCategory,
            IsPositive = feedback.IsPositive,
            Notes = feedback.Notes,
            ShouldHide = feedback.ShouldHide,
            SuggestionCategory = feedback.SuggestionCategory,
            UserId = userId
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(FeedbackCache(userId));
    }

    public async Task<IEnumerable<SuggestionFeedbackRecord>> GetFeedback(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(FeedbackCache(userId), async cache =>
        {
            cache.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.SuggestionFeedback.Where(f => f.UserId == userId)
                .Select(e => new SuggestionFeedbackRecord(e.SuggestionCategory, e.FeedbackCategory, e.Notes,
                    e.ShouldHide, e.IsPositive, e.ChartId))
                .ToArrayAsync(cancellationToken);
        });
    }

    public async Task CreateExternalLogin(Guid userId, string loginProviderName, string externalId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = new ExternalLoginEntity
        {
            ExternalId = externalId,
            LoginProvider = loginProviderName,
            UserId = userId
        };
        await database.ExternalLogin.AddAsync(entity, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> SearchForUsersByName(string searchText,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.User.Where(u => u.Name.Contains(searchText))
            .OrderBy(u => u.Name)
            .Select(u => new User(u.Id, u.Name, u.IsPublic, u.GameTag, new Uri(u.ProfileImage), u.CountryName))
            .ToArrayAsync(cancellationToken);
    }


    public async Task<User?> GetUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.User.Where(u => u.Id == userId)
            .Select(u => new User(u.Id, u.Name, u.IsPublic, u.GameTag, new Uri(u.ProfileImage), u.CountryName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetUsers(IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.User.Where(u => userIds.Contains(u.Id))
            .Select(u => new User(u.Id, u.Name, u.IsPublic, u.GameTag, new Uri(u.ProfileImage), u.CountryName)
            ).ToArrayAsync(cancellationToken);
    }

    public async Task<User?> GetUserByExternalLogin(string loginProviderName, string externalId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from e in database.ExternalLogin
                join u in database.User on e.UserId equals u.Id
                where e.LoginProvider == loginProviderName
                      && e.ExternalId == externalId
                select new User(u.Id, u.Name, u.IsPublic, u.GameTag, new Uri(u.ProfileImage), u.CountryName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IDictionary<string, string>> GetUserUiSettings(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var settings = await database.UserSettings.FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        if (settings == null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(settings.UiSettings) ??
               new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveUserUiSettings(Guid userId, IDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var userSettings =
            await database.UserSettings.FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        if (userSettings == null)
            await database.UserSettings.AddAsync(new UserSettingsEntity
            {
                UserId = userId,
                UiSettings = JsonSerializer.Serialize(settings)
            }, cancellationToken);
        else
            userSettings.UiSettings = JsonSerializer.Serialize(settings);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> GetUserApiToken(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.UserApiToken.FirstOrDefaultAsync(upt => upt.UserId == userId, cancellationToken))
            ?.Token;
    }

    public async Task<User?> GetUserByApiToken(Guid apiToken, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var apiEntity =
            await database.UserApiToken.FirstOrDefaultAsync(api => api.Token == apiToken, cancellationToken);
        if (apiEntity == null) return null;

        apiEntity.CurrentTokenUsageCount++;
        apiEntity.TotalUsageCount++;
        await database.SaveChangesAsync(cancellationToken);
        return await GetUser(apiEntity.UserId, cancellationToken);
    }

    public async Task SetUserApiToken(Guid userId, Guid apiToken, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.UserApiToken.FirstOrDefaultAsync(api => api.Token == apiToken, cancellationToken);
        if (entity == null)
        {
            await database.UserApiToken.AddAsync(new UserApiTokenEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = apiToken,
                CurrentTokenUsageCount = 0,
                TotalUsageCount = 0
            }, cancellationToken);
        }
        else
        {
            entity.CurrentTokenUsageCount = 0;
            entity.Token = apiToken;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateCountry(CountryRecord country, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Country.AddAsync(new CountryEntity
        {
            ImagePath = country.ImagePath.ToString(),
            Name = country.Name
        }, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Uri?> GetCountryImage(Name countryName, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"{nameof(EFUserRepository)}__Country__{countryName}__Image", async o =>
        {
            o.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var nameString = countryName.ToString();
            var uri = await database.Country.Where(d => d.Name == nameString).FirstOrDefaultAsync(cancellationToken);
            if (uri == null) return null;
            return new Uri(uri.ImagePath,
                UriKind.Absolute);
        });
    }

    public async Task<IEnumerable<CountryRecord>> GetCountries(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Country.Select(c => new CountryRecord(c.Name, new Uri(c.ImagePath, UriKind.Absolute)))
            .ToArrayAsync(cancellationToken);
    }
}

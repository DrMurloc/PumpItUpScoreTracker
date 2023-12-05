using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFMatchRepository : IMatchRepository
    {
        private readonly ChartAttemptDbContext _dbContext;
        private readonly JsonSerializerOptions _jsonOptions;

        public EFMatchRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IOptions<JsonSerializerOptions> jsonOptions)
        {
            _dbContext = factory.CreateDbContext();
            _jsonOptions = jsonOptions.Value;
        }

        public async Task<MatchView> GetMatch(Name matchName, CancellationToken cancellationToken)
        {
            var nameString = matchName.ToString();
            var entity = await _dbContext.Match.Where(m => m.Name == nameString).FirstAsync(cancellationToken);
            return JsonSerializer.Deserialize<MatchView>(entity.Json, _jsonOptions) ??
                   throw new JsonException($"Couldn't parse json for match {matchName} {entity.Id}");
        }

        public async Task<IEnumerable<MatchView>> GetAllMatches(CancellationToken cancellationToken)
        {
            var entities = await _dbContext.Match.ToArrayAsync(cancellationToken);
            return entities.Select(e =>
                JsonSerializer.Deserialize<MatchView>(e.Json, _jsonOptions) ??
                throw new JsonException($"Couldn't parse json for match {e.Name} {e.Id}"));
        }

        public async Task SaveMatch(MatchView matchView, CancellationToken cancellationToken)
        {
            var nameString = matchView.MatchName.ToString();
            var entity = await _dbContext.Match.Where(m => m.Name == nameString).FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
                await _dbContext.Match.AddAsync(new MatchEntity
                {
                    Id = Guid.NewGuid(),
                    Name = nameString,
                    Json = JsonSerializer.Serialize(matchView, _jsonOptions)
                }, cancellationToken);
            else
                entity.Json = JsonSerializer.Serialize(matchView, _jsonOptions);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveRandomSettings(Name settingsName, RandomSettings settings,
            CancellationToken cancellationToken)
        {
            var nameString = settingsName.ToString();
            var entity = await _dbContext.RandomSettings.Where(m => m.Name == nameString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
                await _dbContext.RandomSettings.AddAsync(new RandomSettingsEntity
                {
                    Id = Guid.NewGuid(),
                    Name = nameString,
                    Json = JsonSerializer.Serialize(settings, _jsonOptions)
                }, cancellationToken);
            else
                entity.Json = JsonSerializer.Serialize(settings, _jsonOptions);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<RandomSettings> GetRandomSettings(Name settingsName, CancellationToken cancellationToken)
        {
            var nameString = settingsName.ToString();
            var entity = await _dbContext.RandomSettings.Where(r => r.Name == nameString).FirstAsync(cancellationToken);
            return JsonSerializer.Deserialize<RandomSettings>(entity.Json, _jsonOptions) ??
                   throw new JsonException($"Couldn't deserialize random settings {entity.Name} {entity.Id}");
        }

        public async Task<IEnumerable<(Name name, RandomSettings settings)>> GetAllRandomSettings(
            CancellationToken cancellationToken)
        {
            var entities = await _dbContext.RandomSettings.ToArrayAsync(cancellationToken);
            return entities.Select(e => (Name.From(e.Name),
                    JsonSerializer.Deserialize<RandomSettings>(e.Json, _jsonOptions) ??
                    throw new JsonException($"Error deserializing random settings {e.Name} {e.Id}")))
                .ToArray();
        }

        public async Task<IEnumerable<MatchLink>> GetMatchLinksByFromMatchName(Name fromMatchName,
            CancellationToken cancellationToken)
        {
            var nameString = fromMatchName.ToString();
            return await _dbContext.MatchLink.Where(m => m.FromMatch == nameString)
                .Select(e => new MatchLink(e.FromMatch, e.ToMatch, e.IsWinners, e.PlayerCount))
                .ToArrayAsync(cancellationToken);
        }

        public async Task SaveMatchLink(MatchLink matchLink, CancellationToken cancellationToken)
        {
            var fromString = matchLink.FromMatch.ToString();
            var toString = matchLink.ToMatch.ToString();
            var entity = await _dbContext.MatchLink.Where(m => m.FromMatch == fromString && m.ToMatch == toString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await _dbContext.MatchLink.AddAsync(new MatchLinkEntity
                {
                    Id = Guid.NewGuid(),
                    FromMatch = fromString,
                    ToMatch = toString,
                    IsWinners = matchLink.IsWinners,
                    PlayerCount = matchLink.PlayerCount
                }, cancellationToken);
            }
            else
            {
                entity.IsWinners = matchLink.IsWinners;
                entity.PlayerCount = matchLink.PlayerCount;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteMatchLink(Name fromName, Name toName, CancellationToken cancellationToken)
        {
            var fromString = fromName.ToString();
            var toString = toName.ToString();
            var entity = await _dbContext.MatchLink.Where(m => m.FromMatch == fromString && m.ToMatch == toString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity != null)
            {
                _dbContext.MatchLink.Remove(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<MatchLink>> GetAllMatchLinks(CancellationToken cancellationToken)
        {
            return await _dbContext.MatchLink
                .Select(ml => new MatchLink(ml.FromMatch, ml.ToMatch, ml.IsWinners, ml.PlayerCount))
                .ToArrayAsync(cancellationToken);
        }

        public Task<IEnumerable<MatchPlayer>> GetMatchPlayers(CancellationToken cancellationToken)
        {
            return Task.FromResult(PlayerOrders);
        }

        public static readonly IEnumerable<MatchPlayer> RealPlayerOrders = new MatchPlayer[]
        {
            new("QED", 56, 221807174853066753),
            new("Snowstorm", 55, 453727464959770657, "Potential Conflict with DDR and SMX", true),
            new("Tommy Doesn't Miss", 54, 309858089740271617, "Potential Conflict with DDR, SMX, and ITG", true),
            new("Sneezle", 53, 337580556214599700, "Potential Conflict with DDR and SMX", true),
            new("Kwuarter", 52, 516285239518167060),
            new("PrimoVictorian", 51, 104708794755366912),
            new("Ulsi", 50, 128982706145984520), //
            new("Frac", 49, 189917099714412545),
            new("Houseplant", 48, 698584589223985202),
            new("Nyroom", 47, 457690467232907265),
            new("DefaultK", 46, 684175993107120215),
            new("EMCAT", 45, 218159187652247552),
            new("Smallboy", 44, 338157160112586752, "AKA Nagasaki"),
            new("Slowpoke", 43, 361362903691034625), //
            new("ancient_grainz", 42, 291371859105284096, "AKA Thomas Grover, Potential Conflicts with ITG", true),
            new("PacRob", 41, 477504512207093841, "Potential Conflict with SMX and DDR", true), //? 
            new("Crafty The Fox", 40, 384007242837524481),
            new("NESSQUICK", 39, 385296090422837248),
            new("Songbird", 38, 309540048674750466),
            new("StrawHatGabe", 37, 245641474409103361),
            new("Tink", 36, 477504512207093841), //?
            new("Shinobee", 35, 478794055711457282, "Potential Conflict with DDR, SMX, and ITG", true),
            new("ligma", 34, 189142827391778816, "AKA Ivan"),
            new("Surikato", 33, 477504512207093841), //?
            new("SEBAA", 32, 603768563890913282),
            new("Waffle", 31, 146003802527367169),
            new("litenang", 30, 83038699318677504, "Potential Conflict with SMX and ITG", true),
            new("Bedrock", 29, 109982291832389632),
            new("jonathan", 28, 193816242782470154),
            new("HSPuppets", 27, 95558504353374208),
            new("ABENHAIM", 26, 741750282001842276, "AKA IOnlyPlayForTitles"), //-IonlyPlayForTitles
            new("s0 lost", 25, 150066133003665408),
            new("Lulu_uwu", 24, 807880209721982996),
            new("sixxofsixx", 23, 277996088693096450),
            new("Chives", 22, 257353930688692224),
            new("Valex", 21, 72451105463738368),
            new("Flashy flash", 20, 889357618810855445, "AKA Another, no not THAT Another, another Another"),
            new("Ermagerd", 19, 557002938690699278),
            new("Tieny", 18, 478388650510647317), //
            new("Blankman", 17, 208851134805180417),
            new("ZIGGURATH8", 16, 689667017070215215),
            new("Jaekim", 15, 133906426681622528, "AKA Beans on Start.gg"),
            new("esi", 14, 81897683911966720),
            new("Yimmythe42", 13, 534030186677534731),
            new("PureWasian", 12, 325047531576754186, "AKA Tusa"),
            new("Redviper", 11, 184466247767818241),
            new("imDrake", 10, 491462877308256269),
            new("comboscoring", 9, 584861861711970349, "AKA LayZDog"),
            new("JellySlosh", 8, 478958379428085760),
            new("GODDISH", 7, 125806983591755776),
            new("AwesomoBird", 6, 335769888692109316, "Bird is the Word"),
            new("Jboy", 5, 638507320850251777, "AKA KittyCatastrophe in Discord"),
            new("jqtran", 4, 160123260393095169, "AKA IMSORRY, Potential Conflicts with DDR", true),
            new("ParanoiaBoi", 3, 666481245261135884, "Potential Conflicts with ITG, SMX, DDR, DRS", true),
            new("HDS", 2, 931745583629238303, "AKA Edison"),
            new("mattmiller", 1, 131264515248488449, "Egg")
        };

        public static readonly IEnumerable<MatchPlayer> PlayerOrders = new MatchPlayer[]
        {
            new("QED", 56, 477504512207093841),
            new("Snowstorm", 55, 477504512207093841, "Potential Conflict with DDR and SMX", true),
            new("Tommy Doesn't Miss", 54, 477504512207093841, "Potential Conflict with DDR, SMX, and ITG", true),
            new("Sneezle", 53, 477504512207093841, "Potential Conflict with DDR and SMX", true),
            new("Kwuarter", 52, 477504512207093841),
            new("PrimoVictorian", 51, 477504512207093841),
            new("Ulsi", 50, 477504512207093841), //
            new("Frac", 49, 477504512207093841),
            new("Houseplant", 48, 477504512207093841),
            new("Nyroom", 47, 477504512207093841),
            new("DefaultK", 46, 477504512207093841),
            new("EMCAT", 45, 477504512207093841),
            new("Smallboy", 44, 477504512207093841, "AKA Nagasaki"),
            new("Slowpoke", 43, 477504512207093841), //
            new("ancient_grainz", 42, 477504512207093841, "AKA Thomas Grover, Potential Conflicts with ITG", true),
            new("PacRob", 41, 477504512207093841, "Potential Conflict with SMX and DDR", true), //? 
            new("Crafty The Fox", 40, 477504512207093841),
            new("NESSQUICK", 39, 477504512207093841),
            new("Songbird", 38, 477504512207093841),
            new("StrawHatGabe", 37, 477504512207093841),
            new("Tink", 36, 477504512207093841), //?
            new("Shinobee", 35, 477504512207093841, "Potential Conflict with DDR, SMX, and ITG", true),
            new("ligma", 34, 477504512207093841, "AKA Ivan"),
            new("Surikato", 33, 477504512207093841), //?
            new("SEBAA", 32, 477504512207093841),
            new("Waffle", 31, 477504512207093841),
            new("litenang", 30, 477504512207093841, "Potential Conflict with SMX and ITG", true),
            new("Bedrock", 29, 477504512207093841),
            new("jonathan", 28, 477504512207093841),
            new("HSPuppets", 27, 477504512207093841),
            new("ABENHAIM", 26, 477504512207093841, "AKA IOnlyPlayForTitles"), //-IonlyPlayForTitles
            new("s0 lost", 25, 477504512207093841),
            new("Lulu_uwu", 24, 477504512207093841),
            new("sixxofsixx", 23, 477504512207093841),
            new("Chives", 22, 477504512207093841),
            new("Valex", 21, 477504512207093841),
            new("Flashy flash", 20, 477504512207093841, "AKA Another, no not THAT Another, another Another"),
            new("Ermagerd", 19, 477504512207093841),
            new("Tieny", 18, 477504512207093841), //
            new("Blankman", 17, 477504512207093841),
            new("ZIGGURATH8", 16, 477504512207093841),
            new("Jaekim", 15, 477504512207093841, "AKA Beans on Start.gg"),
            new("esi", 14, 477504512207093841),
            new("Yimmythe42", 13, 477504512207093841),
            new("PureWasian", 12, 477504512207093841, "AKA Tusa"),
            new("Redviper", 11, 477504512207093841),
            new("imDrake", 10, 477504512207093841),
            new("comboscoring", 9, 477504512207093841, "AKA LayZDog"),
            new("JellySlosh", 8, 477504512207093841),
            new("GODDISH", 7, 477504512207093841),
            new("AwesomoBird", 6, 477504512207093841, "Bird is the Word"),
            new("Jboy", 5, 477504512207093841, "AKA KittyCatastrophe in Discord"),
            new("jqtran", 4, 477504512207093841, "AKA IMSORRY, Potential Conflicts with DDR", true),
            new("ParanoiaBoi", 3, 477504512207093841, "Potential Conflicts with ITG, SMX, DDR, DRS", true),
            new("HDS", 2, 477504512207093841, "AKA Edison"),
            new("mattmiller", 1, 477504512207093841, "Egg")
        };
    }
}

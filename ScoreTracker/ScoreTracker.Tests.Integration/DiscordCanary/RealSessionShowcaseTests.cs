using Discord;
using Discord.Rest;
using MassTransit;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.Integration.DiscordCanary;

/// <summary>
///     Replays a real play session from the LOCAL development database through the
///     production announcement pipeline: picks the best-looking historical day out of
///     the player's records, computes the honest highlight flags and folder lamps
///     against today's data (Score Quality is skipped — a local database has no
///     comparable-player cohort, and an empty cohort would flag everything), stamps the
///     journal/highlight/milestone tables so the card's deep link resolves on the
///     Sessions page, and publishes <see cref="ScoreHighlightsCapturedEvent" /> on the
///     real in-memory bus — the real CommunitySaga builds the cards and the TESTING bot
///     posts them to the lab channel.
///     Channel safety: the lab channel is attached to the self-creating World community
///     for the duration of the run, a hard gate asserts it is the ONLY Discord channel
///     in the database before anything is published, and the attachment is removed
///     afterwards.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RealSessionShowcaseTests
{
    private const int MinSessionSize = 5;
    private const int MaxSessionSize = 25;

    private static readonly Lazy<IConfigurationRoot> Configuration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets<RealSessionShowcaseTests>(optional: true)
            .Build());

    private static string? Token =>
        Environment.GetEnvironmentVariable("DISCORD_CANARY_TOKEN") ?? Configuration.Value["Discord:BotToken"];

    private static ulong? ChannelId =>
        ulong.TryParse(
            Environment.GetEnvironmentVariable("DISCORD_CANARY_CHANNEL") ??
            Configuration.Value["DiscordTest:CanaryChannelId"], out var id)
            ? id
            : null;

    private static string? DatabaseConnection =>
        Environment.GetEnvironmentVariable("DISCORD_EXAMPLE_CONNECTION") ??
        Configuration.Value["DiscordTest:ExampleConnectionString"];

    public static bool Configured => !string.IsNullOrWhiteSpace(Token) && ChannelId != null &&
                                     !string.IsNullOrWhiteSpace(DatabaseConnection);

    [SessionShowcaseFact]
    public async Task ReplaysARealSessionThroughTheProductionPipelineToTheLabChannel()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var picked = await PickSession();

        await using var provider = BuildRealPipeline();
        var bus = provider.GetRequiredService<IBusControl>();
        await bus.StartAsync();
        var bot = provider.GetRequiredService<IBotClient>();
        try
        {
            await bot.Start();
            var ready = new TaskCompletionSource();
            bot.WhenReady(() =>
            {
                ready.TrySetResult();
                return Task.CompletedTask;
            });
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));

            var mediator = provider.GetRequiredService<IMediator>();
            await AttachLabChannelToWorld(bus, mediator, picked.UserId);
            await AssertLabChannelIsTheOnlyChannelAnywhere();

            var (changes, lamps, scoringLevels, charts) =
                await ComputeHonestHighlights(provider, picked.UserId, picked.Rows);
            var sessionId = await StampDatabaseForDeepLink(picked, changes, lamps, scoringLevels, charts);

            await bus.Publish(ScoreHighlightsCapturedEvent.Create(
                picked.Rows.Max(r => r.RecordedAt), picked.UserId, MixEnum.Phoenix, sessionId, changes, lamps));

            var cardTexts = await AwaitCardsInLabChannel(picked.UserName, startedAt);
            Assert.Contains(cardTexts, t => t.Contains("passed", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DetachLabChannel();
            await bot.Stop();
            await bus.StopAsync();
        }
    }

    // ── Pipeline composition ────────────────────────────────────────────────────

    /// <summary>
    ///     The production graph, minus the web host: AddInfrastructure (every port +
    ///     every vertical + the DbContext factory) pointed at the local database, the
    ///     MediatR scan for the verticals this path dispatches into, and MassTransit's
    ///     in-memory transport with ONLY the Communities consumers registered — the
    ///     captured event goes straight to the card saga; capture/rating/title
    ///     consumers stay out so the replay computes its own honest flags.
    /// </summary>
    private static ServiceProvider BuildRealPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.Configure<DiscordConfiguration>(o =>
        {
            o.BotToken = Token!;
            o.RichScoreMessages = true;
        });
        services.AddSingleton<ICurrentUserAccessor>(new NoCurrentUser());
        services.AddSingleton<IDateTimeOffsetAccessor>(new SystemClock());
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(CommunitiesRegistrationExtensions).Assembly,
            typeof(PlayerProgressRegistrationExtensions).Assembly,
            typeof(CatalogRegistrationExtensions).Assembly,
            typeof(ChartIntelligenceRegistrationExtensions).Assembly,
            typeof(ScoreLedgerRegistrationExtensions).Assembly,
            typeof(IdentityRegistrationExtensions).Assembly));
        services.AddInfrastructure(new AzureBlobConfiguration(),
            new SqlConfiguration { ConnectionString = DatabaseConnection! },
            new SendGridConfiguration());
        services.AddMassTransit(o =>
        {
            o.AddCommunitiesConsumers();
            o.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
        });
        return services.BuildServiceProvider();
    }

    // ── Session selection (real history) ────────────────────────────────────────

    private sealed record SessionRow(Guid ChartId, int Score, string Plate, bool IsBroken, DateTimeOffset RecordedAt);

    private sealed record PickedSession(Guid UserId, string UserName, DateTime Day, Guid PhoenixMixId,
        IReadOnlyList<SessionRow> Rows);

    /// <summary>
    ///     "A good session": the player with the most records (the owner on a dev
    ///     database), then their best day — 5–25 changed records, highest max difficulty
    ///     first, then the fullest, then the most recent.
    /// </summary>
    private static async Task<PickedSession> PickSession()
    {
        await using var connection = new SqlConnection(DatabaseConnection);
        await connection.OpenAsync();

        var userId = Configuration.Value["DiscordTest:ExampleUserId"] is { Length: > 0 } configured
            ? Guid.Parse(configured)
            : await ScalarGuid(connection,
                """
                SELECT TOP 1 p.UserId FROM scores.PhoenixRecord p
                GROUP BY p.UserId ORDER BY COUNT(*) DESC
                """);
        var userName = (string)(await Scalar(connection,
            "SELECT Name FROM scores.[User] WHERE Id = @UserId", ("UserId", userId)))!;
        var phoenixMixId = await ScalarGuid(connection, "SELECT Id FROM scores.Mix WHERE Name = 'Phoenix'");

        var day = (DateTime)(await Scalar(connection,
            $"""
             SELECT TOP 1 CAST(p.RecordedDate AS date)
             FROM scores.PhoenixRecord p
             JOIN scores.Chart c ON c.Id = p.ChartId
             WHERE p.UserId = @UserId
             GROUP BY CAST(p.RecordedDate AS date)
             HAVING COUNT(*) BETWEEN {MinSessionSize} AND {MaxSessionSize}
             ORDER BY MAX(c.Level) DESC, COUNT(*) DESC, CAST(p.RecordedDate AS date) DESC
             """, ("UserId", userId)) ?? throw new InvalidOperationException(
            "No day with a session-sized cluster of records — nothing to showcase."));

        var rows = new List<SessionRow>();
        await using (var command = new SqlCommand(
                         """
                         SELECT p.ChartId, p.Score, p.Plate, p.IsBroken, p.RecordedDate
                         FROM scores.PhoenixRecord p
                         WHERE p.UserId = @UserId AND CAST(p.RecordedDate AS date) = @Day
                         """, connection))
        {
            command.Parameters.AddWithValue("UserId", userId);
            command.Parameters.AddWithValue("Day", day);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add(new SessionRow(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2),
                    reader.GetBoolean(3), reader.GetDateTimeOffset(4)));
        }

        return new PickedSession(userId, userName, day, phoenixMixId, rows);
    }

    // ── Channel wiring + the hard safety gate ───────────────────────────────────

    private async Task AttachLabChannelToWorld(IBusControl bus, IMediator mediator, Guid userId)
    {
        // The self-creating system-community path (C0): a profile-updated fact makes
        // World exist and joins the player — the same consumer production uses.
        await bus.Publish(new UserUpdatedEvent(userId, Country: null, IsPublic: true));
        await WaitFor(async () => await CountAsync(
            """
            SELECT COUNT(*) FROM scores.CommunityMembership m
            JOIN scores.Community c ON c.Id = m.CommunityId
            WHERE c.Name = 'World' AND m.UserId = @UserId
            """, ("UserId", userId)) > 0, TimeSpan.FromSeconds(15), "World membership");

        await mediator.Send(new AddDiscordChannelToCommunityCommand(Name.From("World"), null, ChannelId!.Value,
            SendScores: true, SendTitles: true, SendNewMembers: true));
    }

    /// <summary>
    ///     Nothing publishes until the ONLY Discord channel attached to ANY community in
    ///     this database is the lab channel. A dev database restored from anywhere else
    ///     could carry real channel ids — this is the line that keeps a showcase run
    ///     inside the lab.
    /// </summary>
    private async Task AssertLabChannelIsTheOnlyChannelAnywhere()
    {
        var foreignChannels = await CountAsync(
            "SELECT COUNT(*) FROM scores.CommunityChannel WHERE ChannelId <> @Lab",
            ("Lab", (decimal)ChannelId!.Value));
        var labChannels = await CountAsync(
            "SELECT COUNT(*) FROM scores.CommunityChannel WHERE ChannelId = @Lab",
            ("Lab", (decimal)ChannelId!.Value));
        Assert.True(foreignChannels == 0 && labChannels > 0,
            $"Aborting before publish: expected the lab channel to be the only Discord channel in the " +
            $"database, found {foreignChannels} foreign channel row(s).");
    }

    private static async Task DetachLabChannel()
    {
        await using var connection = new SqlConnection(DatabaseConnection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "DELETE FROM scores.CommunityChannel WHERE ChannelId = @Lab", connection);
        command.Parameters.AddWithValue("Lab", (decimal)ChannelId!.Value);
        await command.ExecuteNonQueryAsync();
    }

    // ── Honest flags + lamps (mirrors HighlightCaptureSaga, minus Score Quality) ─

    private static async Task<(ScoreHighlightsCapturedEvent.HighlightedChange[] Changes,
            PlayerMilestoneRecord[] Lamps, IDictionary<Guid, double> ScoringLevels,
            IReadOnlyDictionary<Guid, Chart> Charts)>
        ComputeHonestHighlights(ServiceProvider provider, Guid userId, IReadOnlyList<SessionRow> rows)
    {
        var mediator = provider.GetRequiredService<IMediator>();
        var chartRepository = provider.GetRequiredService<IChartRepository>();
        var scores = provider.GetRequiredService<IScoreReader>();
        var titles = provider.GetRequiredService<ITitleRepository>();

        var charts = (await chartRepository.GetCharts(MixEnum.Phoenix)).ToDictionary(c => c.Id);
        var bests = (await scores.GetBestScores(MixEnum.Phoenix, userId, CancellationToken.None))
            .ToDictionary(s => s.ChartId);
        var top50 = (await mediator.Send(new GetTop50ForPlayerQuery(userId, null, Mix: MixEnum.Phoenix)))
            .Select(s => s.ChartId).ToHashSet();
        var completed = (await titles.GetCompletedTitles(MixEnum.Phoenix, userId, CancellationToken.None))
            .Select(t => t.Title).ToHashSet();
        var incompleteTitles = PhoenixTitleList.BuildProgress(charts, bests.Values, completed)
            .OfType<PhoenixTitleProgress>()
            .Where(t => !t.IsComplete && t.Title.CompletionRequired > 0)
            .ToArray();
        var scoringLevels = await mediator.Send(new GetChartScoringLevelsQuery(MixEnum.Phoenix));

        var folderSizes = charts.Values.GroupBy(c => (c.Type, c.Level)).ToDictionary(g => g.Key, g => g.Count());
        var folderClears = bests.Values
            .Where(b => !b.IsBroken && b.Score != null && charts.ContainsKey(b.ChartId))
            .GroupBy(b => (charts[b.ChartId].Type, charts[b.ChartId].Level))
            .ToDictionary(g => g.Key, g => g.Count());

        var known = rows.Where(r => charts.ContainsKey(r.ChartId) && bests.ContainsKey(r.ChartId)).ToArray();
        var flags = new Dictionary<Guid, HighlightFlags>();
        foreach (var row in known)
        {
            var chart = charts[row.ChartId];
            var best = bests[row.ChartId];
            var f = HighlightFlags.None;
            if (!best.IsBroken && best.Score != null)
            {
                if (top50.Contains(chart.Id)) f |= HighlightFlags.PumbilityTop50;
                if (incompleteTitles.Any(t => t.PhoenixTitle.CompletionProgress(chart, best) > 0))
                    f |= HighlightFlags.TitleProgress;
            }

            if (f != HighlightFlags.None) flags[chart.Id] = f;
        }

        var lamps = new List<PlayerMilestoneRecord>();
        var occurredAt = rows.Max(r => r.RecordedAt);
        foreach (var folder in known.GroupBy(r => (charts[r.ChartId].Type, charts[r.ChartId].Level)))
        {
            var (type, level) = folder.Key;
            var size = folderSizes.GetValueOrDefault(folder.Key);
            var clears = folderClears.GetValueOrDefault(folder.Key);
            var newPasses = folder.Where(r => !bests[r.ChartId].IsBroken).ToArray();

            if (size > 0 && clears / (double)size >= 0.9)
                foreach (var pass in newPasses)
                    flags[pass.ChartId] = flags.GetValueOrDefault(pass.ChartId) | HighlightFlags.FolderCompletion90;

            if (size == 0 || clears != size) continue;
            var folderName = $"{type.GetShortHand()}{(int)level}";
            lamps.Add(new PlayerMilestoneRecord(MilestoneKind.FolderPassLamp, null, occurredAt, null, null, null,
                folderName));
            var folderBests = charts.Values
                .Where(c => c.Type == type && c.Level == level)
                .Select(c => bests.GetValueOrDefault(c.Id))
                .ToArray();
            if (folderBests.Any(b => b?.Score == null || b.IsBroken)) continue;
            var minGrade = folderBests.Min(b => b!.Score!.Value.LetterGrade);
            lamps.Add(new PlayerMilestoneRecord(MilestoneKind.FolderGradeLamp, null, occurredAt, null, null, null,
                $"{folderName}|{minGrade.GetName()}"));
            if (folderBests.Any(b => b!.Plate == null)) continue;
            var minPlate = folderBests.Min(b => b!.Plate!.Value);
            lamps.Add(new PlayerMilestoneRecord(MilestoneKind.FolderPlateLamp, null, occurredAt, null, null, null,
                $"{folderName}|{minPlate}"));
        }

        var changes = known.Select(r => new ScoreHighlightsCapturedEvent.HighlightedChange(
                r.ChartId,
                IsNewPass: !r.IsBroken,
                OldScore: null,
                NewScore: r.Score,
                Plate: PhoenixPlateHelperMethods.TryParse(r.Plate)?.ToString() ?? r.Plate,
                IsBroken: r.IsBroken,
                Flags: flags.GetValueOrDefault(r.ChartId)))
            .ToArray();
        return (changes, lamps.ToArray(), scoringLevels, charts);
    }

    // ── Database stamping (the card's deep link lands on a real session) ────────

    /// <summary>
    ///     Journal, highlight, and milestone rows for the demo session, at the
    ///     historical timestamps — the "View all recent scores" button then opens the
    ///     Sessions page with this exact session expanded, flags and lamps included.
    ///     Re-runnable: previously stamped showcase rows for the day are cleared first
    ///     (organic journal rows keep a NULL SessionId and are never touched).
    /// </summary>
    private static async Task<Guid> StampDatabaseForDeepLink(PickedSession picked,
        ScoreHighlightsCapturedEvent.HighlightedChange[] changes, PlayerMilestoneRecord[] lamps,
        IDictionary<Guid, double> scoringLevels, IReadOnlyDictionary<Guid, Chart> charts)
    {
        var sessionId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using var connection = new SqlConnection(DatabaseConnection);
        await connection.OpenAsync();

        foreach (var table in new[] { "ScoreEventJournal", "ScoreHighlight", "PlayerMilestone" })
        {
            await using var cleanup = new SqlCommand(
                $"""
                 DELETE FROM scores.{table}
                 WHERE UserId = @UserId AND SessionId IS NOT NULL AND CAST(OccurredAt AS date) = @Day
                 """, connection);
            cleanup.Parameters.AddWithValue("UserId", picked.UserId);
            cleanup.Parameters.AddWithValue("Day", picked.Day);
            await cleanup.ExecuteNonQueryAsync();
        }

        foreach (var row in picked.Rows)
        {
            await using var insert = new SqlCommand(
                """
                INSERT INTO scores.ScoreEventJournal
                    (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken, SessionId)
                VALUES (NEWID(), @EventId, @OccurredAt, 'officialImport', @MixId, @UserId, @ChartId, @Score,
                        @Plate, @IsBroken, @SessionId)
                """, connection);
            insert.Parameters.AddWithValue("EventId", eventId);
            insert.Parameters.AddWithValue("OccurredAt", row.RecordedAt);
            insert.Parameters.AddWithValue("MixId", picked.PhoenixMixId);
            insert.Parameters.AddWithValue("UserId", picked.UserId);
            insert.Parameters.AddWithValue("ChartId", row.ChartId);
            insert.Parameters.AddWithValue("Score", row.Score);
            insert.Parameters.AddWithValue("Plate", row.Plate);
            insert.Parameters.AddWithValue("IsBroken", row.IsBroken);
            insert.Parameters.AddWithValue("SessionId", sessionId);
            await insert.ExecuteNonQueryAsync();
        }

        var stamps = picked.Rows.ToDictionary(r => r.ChartId, r => r.RecordedAt);
        foreach (var change in changes.Where(c => c.Flags != HighlightFlags.None))
        {
            await using var insert = new SqlCommand(
                """
                INSERT INTO scores.ScoreHighlight
                    (Id, UserId, MixId, ChartId, SessionId, OccurredAt, Flags, Level, ScoringLevel)
                VALUES (NEWID(), @UserId, @MixId, @ChartId, @SessionId, @OccurredAt, @Flags, @Level, @ScoringLevel)
                """, connection);
            insert.Parameters.AddWithValue("UserId", picked.UserId);
            insert.Parameters.AddWithValue("MixId", picked.PhoenixMixId);
            insert.Parameters.AddWithValue("ChartId", change.ChartId);
            insert.Parameters.AddWithValue("SessionId", sessionId);
            insert.Parameters.AddWithValue("OccurredAt", stamps[change.ChartId]);
            insert.Parameters.AddWithValue("Flags", (int)change.Flags);
            insert.Parameters.AddWithValue("Level", (int)charts[change.ChartId].Level);
            insert.Parameters.AddWithValue("ScoringLevel",
                scoringLevels.TryGetValue(change.ChartId, out var sl) ? (object)sl : DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }

        foreach (var lamp in lamps)
        {
            await using var insert = new SqlCommand(
                """
                INSERT INTO scores.PlayerMilestone
                    (Id, UserId, MixId, SessionId, OccurredAt, Kind, OldValue, NewValue, Title, Detail)
                VALUES (NEWID(), @UserId, @MixId, @SessionId, @OccurredAt, @Kind, NULL, NULL, NULL, @Detail)
                """, connection);
            insert.Parameters.AddWithValue("UserId", picked.UserId);
            insert.Parameters.AddWithValue("MixId", picked.PhoenixMixId);
            insert.Parameters.AddWithValue("SessionId", sessionId);
            insert.Parameters.AddWithValue("OccurredAt", lamp.OccurredAt);
            insert.Parameters.AddWithValue("Kind", lamp.Kind.ToString());
            insert.Parameters.AddWithValue("Detail", (object?)lamp.Detail ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }

        return sessionId;
    }

    // ── Readback ────────────────────────────────────────────────────────────────

    private static async Task<string[]> AwaitCardsInLabChannel(string userName, DateTimeOffset postedAfter)
    {
        await using var rest = new DiscordRestClient();
        await rest.LoginAsync(TokenType.Bot, Token);
        var channel = Assert.IsType<IMessageChannel>(await rest.GetChannelAsync(ChannelId!.Value), exactMatch: false);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var recent = (await channel.GetMessagesAsync(10).FlattenAsync()).ToArray();
            var cards = recent
                .Where(m => m.Timestamp > postedAfter)
                .SelectMany(m => DiscordCanaryTests.ComponentTexts(m.Components))
                .Where(t => t.Contains(userName))
                .ToArray();
            if (cards.Length > 0) return cards;
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        throw new TimeoutException($"No card mentioning {userName} appeared in the lab channel within 90s.");
    }

    // ── Small helpers ───────────────────────────────────────────────────────────

    private static async Task<object?> Scalar(SqlConnection connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<Guid> ScalarGuid(SqlConnection connection, string sql) =>
        (Guid)(await Scalar(connection, sql) ?? throw new InvalidOperationException($"No result: {sql}"));

    private static async Task<int> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(DatabaseConnection);
        await connection.OpenAsync();
        return (int)(await Scalar(connection, sql, parameters))!;
    }

    private static async Task WaitFor(Func<Task<bool>> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(500);
        }

        throw new TimeoutException($"Timed out waiting for {what}.");
    }

    private sealed class NoCurrentUser : ICurrentUserAccessor
    {
        public bool IsLoggedIn => false;
        public User User => throw new InvalidOperationException("The showcase pipeline has no current user.");
        public bool IsLoggedInAsAdmin => false;
        public Task SetCurrentUser(User user) => throw new InvalidOperationException();
    }

    private sealed class SystemClock : IDateTimeOffsetAccessor
    {
        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}

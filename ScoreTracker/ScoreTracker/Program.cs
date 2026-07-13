using BlazorApplicationInsights;
using Hangfire;
using Hangfire.SqlServer;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi;
using MudBlazor.Services;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Ucs.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;
using ScoreTracker.Web;
using ScoreTracker.Web.Accessors;
using ScoreTracker.Web.Configuration;
using ScoreTracker.Web.HostedServices;
using ScoreTracker.Web.Security;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Shared;
using ScoreTracker.Web.Swagger;
using Swashbuckle.AspNetCore.Filters;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.Configure<JsonSerializerOptions>(o =>
{
    o.Converters.Add(Name.Converter);
    o.Converters.Add(PhoenixScore.Converter);
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(1);
});
var discordConfig = builder.Configuration.GetSection("Discord").Get<DiscordConfiguration>();
var googleConfig = builder.Configuration.GetSection("Google").Get<GoogleConfiguration>();
var facebookConfig = builder.Configuration.GetSection("Facebook").Get<FacebookConfiguration>();

builder.Services.AddCors(o =>
{
    o.AddPolicy("API", p =>
    {
        p.AllowAnyOrigin();
        p.AllowAnyHeader();
        p.AllowAnyMethod();
    });
});
builder.Services.Configure<DiscordConfiguration>(builder.Configuration.GetSection("Discord"));
builder.Services.Configure<DevAuthConfiguration>(builder.Configuration.GetSection("DevAuth"));
builder.Services.Configure<ProdSyncConfiguration>(builder.Configuration.GetSection("ProdSync"));
builder.Services.Configure<PiuGameConfiguration>(builder.Configuration.GetSection("PiuGame"));
builder.Services.Configure<PiuCenterConfiguration>(builder.Configuration.GetSection("PiuCenter"));
builder.Services.Configure<GoogleConfiguration>(builder.Configuration.GetSection("Google"));
var sqlConfig = builder.Configuration.GetSection("SQL").Get<SqlConfiguration>()!;
builder.Services.AddMassTransit(o =>
{
    // Application and Web no longer hold public consumers — every saga lives in a
    // vertical now. The Web scan stays for future host-level consumers.
    o.AddConsumers(typeof(RecurringJobRunner).Assembly);
    // Vertical consumers are internal — assembly scanning skips them (see the
    // AddScoreLedgerConsumers doc comment and its tripwire test).
    o.AddPlayerProgressConsumers();
    o.AddScoreLedgerConsumers();
    o.AddOfficialMirrorConsumers();
    o.AddChartIntelligenceConsumers();
    o.AddWeeklyChallengeConsumers();
    o.AddEventCompetitionConsumers();
    o.AddCommunitiesConsumers();
    o.AddUcsConsumers();
    o.AddCatalogConsumers();
    o.AddIdentityConsumers();

    o.AddDelayedMessageScheduler();

    o.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);

        cfg.UseDelayedMessageScheduler();
    });
});
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(sqlConfig.ConnectionString, new SqlServerStorageOptions
    {
        SchemaName = "HangFire",
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(15)
    }));
builder.Services.AddHangfireServer();
builder.Services.AddTransient<RecurringJobRunner>();
builder.Services.AddAuthentication("DefaultAuthentication")
    .AddCookie("DefaultAuthentication", o =>
    {
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.Cookie.MaxAge = o.ExpireTimeSpan;
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var userIdClaim = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId)) return;

            var issuedAtClaim = ctx.Principal?.FindFirst(ScoreTrackerClaimTypes.ClaimsIssuedAt)?.Value;
            var issuedAt = DateTimeOffset.TryParse(issuedAtClaim, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;

            var users = ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var invalidatedAt = await users.GetClaimsInvalidatedAt(userId, ctx.HttpContext.RequestAborted);
            if (issuedAt < invalidatedAt)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync("DefaultAuthentication");
            }
        };
    })
    // Remote OAuth handlers persist their handshake result into their SignInScheme. Without a
    // dedicated scheme they default to the session cookie above, so every OAuth round-trip
    // briefly replaces the live session with the raw external principal — fatal for the
    // link/verify flows, which must keep the user signed in across the handshake.
    .AddCookie("ExternalAuthentication", o =>
    {
        o.SlidingExpiration = false;
        o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddDiscord("Discord", o =>
    {
        o.ClientId = discordConfig.ClientId;
        o.ClientSecret = discordConfig.ClientSecret;
        o.SignInScheme = "ExternalAuthentication";
    })
    .AddGoogle("Google", o =>
    {
        o.ClientId = googleConfig.ClientId;
        o.ClientSecret = googleConfig.ClientSecret;
        o.SignInScheme = "ExternalAuthentication";
    })
    .AddFacebook("Facebook", o =>
    {
        o.AppId = facebookConfig.AppId;
        o.AppSecret = facebookConfig.AppSecret;
        o.SignInScheme = "ExternalAuthentication";
    })
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationScheme>("ApiToken", o => { });

builder.Services.AddSwaggerExamplesFromAssemblyOf<RecordPhoenixScoreDtoExample>();
builder.Services.AddSwaggerGen(o =>
{
    o.ExampleFilters();
    o.UseInlineDefinitionsForEnums();
    var xml = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var path = Path.Combine(AppContext.BaseDirectory, xml);
    o.IncludeXmlComments(path);
    const string schemeId = "basic";

    o.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        In = ParameterLocation.Header,
        Scheme = "basic",
        Description = "ApiToken from Account page. Put anything in for username."
    });

    // Swashbuckle v10 / .NET 10 style
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(schemeId, document)] = new List<string>()
        // or just [] if you're on C# 12+
    });
    o.SchemaFilter<EnumSchemaFilter>();
});

builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy(nameof(ApiTokenAttribute), p => p.RequireAssertion(ApiTokenAttribute.AuthPolicy));
    });
builder.Services.AddBlazorApplicationInsights()
    .AddTransient<IPhoenixScoreFileExtractor, PhoenixScoreFileExtractor>()
    .AddMudServices()
    .AddScoped<ICurrentUserAccessor, HttpContextUserAccessor>()
    .AddTransient<IUiSettingsAccessor, UiSettingsAccessor>()
    .AddSingleton<AccountProofService>()
    .AddTransient<DevSyncService>()
    .AddHttpContextAccessor()
    .AddHttpClient()
    .AddHostedService<BotHostedService>()
    .AddMediatR(o =>
    {
        o.RegisterServicesFromAssemblies(
            // Data no longer holds MediatR handlers — its last two (player stats/history)
            // moved into the PlayerProgress vertical at C50.
            typeof(MatchSaga).Assembly
            , typeof(MainLayout).Assembly,
            typeof(PlayerProgressRegistrationExtensions).Assembly,
            typeof(ScoreTracker.Ucs.Wiring.UcsRegistrationExtensions).Assembly,
            typeof(IdentityRegistrationExtensions).Assembly,
            typeof(ScoreTracker.ScoreLedger.Wiring.ScoreLedgerRegistrationExtensions).Assembly,
            typeof(ScoreTracker.OfficialMirror.Wiring.OfficialMirrorRegistrationExtensions).Assembly,
            typeof(ScoreTracker.Catalog.Wiring.CatalogRegistrationExtensions).Assembly,
            typeof(ChartIntelligenceRegistrationExtensions).Assembly,
            typeof(WeeklyChallengeRegistrationExtensions).Assembly,
            typeof(EventCompetitionRegistrationExtensions).Assembly,
            typeof(CommunitiesRegistrationExtensions).Assembly,
            typeof(ScoreTracker.HomePage.Wiring.HomePageRegistrationExtensions).Assembly);
    })
    .AddTransient<IUserAccessService, UserAccessService>()
    .AddTransient<IBulkChartJsonParser, BulkChartJsonParser>()
    .AddInfrastructure(builder.Configuration.GetSection("AzureBlob").Get<AzureBlobConfiguration>(),
        sqlConfig,
        builder.Configuration.GetSection("Sendgrid").Get<SendGridConfiguration>())
    .AddTransient<IDateTimeOffsetAccessor, DateTimeOffsetAccessor>()
    .AddTransient<IRandomNumberGenerator, RandomNumberGenerator>()
    .AddControllers();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<IStringLocalizer<App>, StringLocalizer<App>>();
builder.Services.AddScoped<ChartVideoDisplayer>();
builder.Services.AddScoped<ChartScoringLevels>();
// Circuit-scoped: widgets on a home-page board share one chart catalog per mix (§2.5).
builder.Services.AddScoped<ScoreTracker.Web.Services.HomeDashboard.ChartCatalogCache>();
builder.Services.AddCookiePolicy(opts =>
{
    opts.CheckConsentNeeded = ctx => false;
    opts.OnAppendCookie = ctx => { ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30); };
});

var app = builder.Build();

// AutoMigrate is set by the Aspire AppHost for local dev; everywhere else this only
// logs drift (migrations stay manually applied in production).
await app.Services.ApplyOrReportMigrationsAsync(builder.Configuration["AutoMigrate"] == "true");


app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures("en-US", "pt-BR", "ko-KR", "en-ZW", "es-MX", "es-ES", "fr-FR", "ja-JP", "it-IT")
    .AddSupportedUICultures("en-US", "pt-BR", "ko-KR", "en-ZW", "es-MX", "es-ES", "fr-FR", "ja-JP", "it-IT")
    .SetDefaultCulture("en-US"));
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Tier-lists overhaul C3: legacy tier list URLs 301 to the canonical path form
// (/TierLists/{Single|Double|CoOp}/{level}); the lens survives as a query param.
// A real 301 (not a Blazor NavigateTo) so crawlers and old bookmarks consolidate.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isLegacyAlias = path.Equals("/ChartSkills", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("/PersonalizedTierList", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("/TierLists/Old", StringComparison.OrdinalIgnoreCase);
    var isLegacyQueryForm = path.Equals("/TierLists", StringComparison.OrdinalIgnoreCase) &&
                            (context.Request.Query.ContainsKey("Difficulty") ||
                             context.Request.Query.ContainsKey("ChartType"));
    if (isLegacyAlias || isLegacyQueryForm)
    {
        var query = context.Request.Query;
        var type = Enum.TryParse<ScoreTracker.SharedKernel.Enums.ChartType>(query["ChartType"], true,
            out var parsedType)
            ? parsedType
            : ScoreTracker.SharedKernel.Enums.ChartType.Double;
        var level = int.TryParse(query["Difficulty"], out var parsedLevel) && parsedLevel is >= 1 and <= 29
            ? parsedLevel
            : 18;
        var target = $"/TierLists/{type}/{level}";
        if (query.TryGetValue("TierListType", out var lens) && !string.IsNullOrWhiteSpace(lens))
            target += $"?TierListType={Uri.EscapeDataString(lens.ToString())}";
        context.Response.Redirect(target, true);
        return;
    }

    await next();
});

app.UseSwagger();
app.UseSwaggerUI(c => { });
app.UseRouting();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorization() }
});

// Cron expressions are UTC. Original schedule was Eastern Time (EST = UTC-5);
// times below are the UTC equivalents of the ET wall-clock slots.
var recurringJobs = new (string Id, System.Linq.Expressions.Expression<Func<RecurringJobRunner, Task>> Job, string Cron)[]
{
    ("process-scores-tier-list",         r => r.PublishProcessScoresTiersList(),          "0 7 * * *"),  // 02:00 ET
    ("calculate-scoring-difficulty",     r => r.PublishCalculateScoringDifficulty(),      "0 8 * * *"),  // 03:00 ET
    ("update-weekly-charts",             r => r.PublishUpdateWeeklyCharts(),              "0 5 * * *"),  // 00:00 ET (EST) — Monday board reset; was 0 9 (5am EDT), a Hangfire-extraction regression
    ("rotate-daily-step",                r => r.PublishRotateDailyStep(),                 "0 5 * * *"),  // 00:00 ET (EST) — Daily Step reset, per mix
    ("process-pass-tier-list",           r => r.PublishProcessPassTierList(),             "30 9 * * *"), // 04:30 ET
    ("calculate-chart-letter-difficulties", r => r.PublishCalculateChartLetterDifficulties(), "0 10 * * *"), // 05:00 ET
    ("start-leaderboard-import",         r => r.PublishStartLeaderboardImport(),          "30 10 * * 0"), // Sundays 05:30 ET
    // The P2 pumbility board recomputes daily at 01:00 GMT+9 (16:00 UTC); Sundays 16:30 UTC
    // imports right after a fresh recompute. Requires PiuGame:ServiceUsername/ServicePassword
    // (the P2 boards are login-gated) — without them the import fails loudly naming the keys.
    ("start-phoenix2-leaderboard-import", r => r.PublishStartPhoenix2LeaderboardImport(),  "30 16 * * 0"), // Sundays 16:30 UTC
    ("try-schedule-mom",                 r => r.PublishTryScheduleMoM(),                  "0 11 * * *"), // 06:00 ET
    ("flush-overdue-score-batches",      r => r.PublishFlushOverdueScoreBatches(),        "*/5 * * * *"), // every 5 min — safety net for stuck batches
    ("process-account-purges",           r => r.PublishProcessAccountPurges(),            "30 11 * * *"), // 06:30 ET — merged-account grace-window purges
    ("refresh-folder-share-cards",       r => r.PublishRefreshFolderShareCards(),         "30 10 * * *"), // 05:30 ET — og:images, right after the tier-list rebuilds
    ("crawl-piucenter",                  r => r.PublishCrawlPiuCenter(),                  "0 6 * * 1")   // Mondays 01:00 ET — gap-driven, near no-op unless piucenter shipped a new data release
};
if (builder.Configuration["PreventRecurringJobs"] == "true")
{
    foreach (var (id, _, _) in recurringJobs)
        RecurringJob.RemoveIfExists(id);
}
else
{
    foreach (var (id, job, cron) in recurringJobs)
        RecurringJob.AddOrUpdate(id, job, cron);
}

app.UseEndpoints(endpoints => { endpoints.MapControllers(); });


app.MapDefaultEndpoints();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// Exposes the top-level-statement entry point to WebApplicationFactory so the E2E
// suite can host the real app on Kestrel.
public partial class Program
{
}

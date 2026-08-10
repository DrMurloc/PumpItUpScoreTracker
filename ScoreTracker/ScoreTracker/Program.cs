using BlazorApplicationInsights;
using Hangfire;
using Hangfire.SqlServer;
using MassTransit;
using MediatR;
using MediatR.Pipeline;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi;
using MudBlazor.Services;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartComments.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.CommunityTools.Wiring;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.HomePage.Wiring;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.Randomizer.Wiring;
using ScoreTracker.Rivals.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;
using ScoreTracker.Web;
using ScoreTracker.Web.Accessors;
using ScoreTracker.Data.DevTooling;
using ScoreTracker.Web.Configuration;
using ScoreTracker.Web.HostedServices;
using ScoreTracker.Web.Security;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.Localization;
using ScoreTracker.Web.Services.UiNotifications;
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
// The front door owns "/Welcome" via its @page directive; a Razor Page can declare only
// one route, so "/Login" is attached as a second route to the same page here.
builder.Services.AddRazorPages(options => options.Conventions.AddPageRoute("/FrontDoor", "Login"));
// Render modes: components render as static HTML by default, and only what asks for it gets a
// circuit (docs/design/render-modes.md). Prerendering stays off — see App.razor.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
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
builder.Services.Configure<ChartCommentsConfiguration>(builder.Configuration.GetSection("ChartComments"));
builder.Services.Configure<ProdSyncConfiguration>(builder.Configuration.GetSection("ProdSync"));
builder.Services.Configure<ScoreTracker.CommunityTools.Wiring.CommunityToolsConfiguration>(
    builder.Configuration.GetSection(
        ScoreTracker.CommunityTools.Wiring.CommunityToolsConfiguration.SectionName));
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
    o.AddCommunityToolsConsumers();
    o.AddChartCommentsConsumers();
    o.AddEventCompetitionConsumers();
    o.AddCommunitiesConsumers();
    o.AddCatalogConsumers();
    o.AddIdentityConsumers();
    o.AddRandomizerConsumers();
    o.AddHomePageConsumers();
    o.AddRivalsConsumers();

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
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationScheme>("ApiToken", o => { })
    .AddScheme<AuthenticationSchemeOptions, ToolKeyAuthenticationScheme>(
        ToolKeyAuthenticationScheme.SchemeName, o => { });

builder.Services.AddRateLimiter(o => o.AddApiV2Policy());
builder.Services.AddSwaggerExamplesFromAssemblyOf<RecordPhoenixScoreDtoExample>();
builder.Services.AddSwaggerGen(o =>
{
    o.ExampleFilters();
    o.UseInlineDefinitionsForEnums();
    var xml = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var path = Path.Combine(AppContext.BaseDirectory, xml);
    o.IncludeXmlComments(path);
    const string schemeId = "basic";
    const string toolSchemeId = "toolKey";

    o.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        In = ParameterLocation.Header,
        Scheme = "basic",
        Description = "Personal API token from your Account page. Put anything in for username. " +
                      "A tool API key also works here."
    });

    // The other kind of caller. Without this the Authorize dialog offers only Basic, so a tool key
    // pasted into the one box on offer comes back 401 with nothing on screen saying why.
    o.AddSecurityDefinition(toolSchemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        In = ParameterLocation.Header,
        Scheme = "bearer",
        Description = "Tool API key from /Developers, e.g. piu_scores_live_… — paste the key itself, " +
                      "without the word Bearer. Authenticates the tool across the players who " +
                      "granted it access."
    });

    // Swashbuckle v10 / .NET 10 style
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(schemeId, document)] = new List<string>(),
        [new OpenApiSecuritySchemeReference(toolSchemeId, document)] = new List<string>()
    });
    o.SchemaFilter<EnumSchemaFilter>();
});

builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy(nameof(ApiTokenAttribute), p => p.RequireAssertion(ApiTokenAttribute.AuthPolicy));
        o.AddPolicy(nameof(ApiV2Attribute), p => p.RequireAssertion(ApiV2Attribute.AuthPolicy));
    });
builder.Services.AddBlazorApplicationInsights()
    .AddTransient<IPhoenixScoreFileExtractor, PhoenixScoreFileExtractor>()
    .AddMudServices()
    .AddScoped<ICurrentUserAccessor, HttpContextUserAccessor>()
    .AddScoped<AmbientUserContext>()
    // The UI notification hub is a singleton (shared across every circuit and the bus consumers
    // that publish through its MediatR bridges); the bridge handlers are picked up by the MediatR
    // assembly scan below.
    .AddSingleton<IUiNotificationHub, UiNotificationHub>()
    .AddTransient<IUiSettingsAccessor, UiSettingsAccessor>()
    .AddSingleton<AccountProofService>()
    .AddHttpContextAccessor()
    .AddHttpClient()
    .AddHostedService<BotHostedService>()
    .AddHostedService<ChartPageCacheWarmer>()
    // Restart recovery, once per boot. Not a recurring job on purpose — see the type's remarks.
    .AddHostedService<StartupRecoveryPublisher>()
    .AddMediatR(o =>
    {
        // Post-processors only run when wired through this configuration (a bare DI
        // registration is never invoked): the shell-settings cache eviction makes a
        // settings save visible on the very next request.
        o.AddRequestPostProcessor<IRequestPostProcessor<SaveUserUiSettingCommand, Unit>,
            UiSettingSavedCacheEviction>();
        // Application + Web, then every vertical. The vertical list is NOT written out here: it
        // used to be, and CommunityTools was left off it — 33 handlers silently unregistered, found
        // by a page throwing at runtime. VerticalAssemblies.All() is the one place, and a ratchet
        // checks it against the assemblies that actually contain handlers.
        // Data no longer holds MediatR handlers — its last two (player stats/history) moved into
        // the PlayerProgress vertical at C50.
        o.RegisterServicesFromAssemblies(
            new[] { typeof(GetSavedChartsHandler).Assembly, typeof(MainLayout).Assembly }
                .Concat(VerticalAssemblies.All()).ToArray());
    })
    .AddTransient<IUserAccessService, UserAccessService>()
    .AddTransient<IBulkChartJsonParser, BulkChartJsonParser>()
    .AddInfrastructure(builder.Configuration.GetSection("AzureBlob").Get<AzureBlobConfiguration>(),
        sqlConfig,
        builder.Configuration.GetSection("Sendgrid").Get<SendGridConfiguration>())
    .AddTransient<IDateTimeOffsetAccessor, DateTimeOffsetAccessor>()
    .AddTransient<IRandomNumberGenerator, RandomNumberGenerator>()
    .AddTransient<ILocalizedTextAccessor, ResxLocalizedTextAccessor>()
    .AddControllers();
builder.Services.Configure<KeyVaultConfiguration>(builder.Configuration.GetSection("KeyVault"));
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<IStringLocalizer<App>, StringLocalizer<App>>();
builder.Services.AddScoped<ChartScoringLevels>();
builder.Services.AddScoped<PageDockService>();
builder.Services.AddScoped<ShellContext>();
builder.Services.AddScoped<ShellModelFactory>();
builder.Services.AddScoped<ScoreTracker.Web.Services.SessionBreakdownBuilder>();
builder.Services.AddScoped<ChartUrlResolver>();
builder.Services.AddScoped<StaticHeadResolver>();
builder.Services.AddScoped<IImportCredentialClientStore, ImportCredentialClientStore>();
// Circuit-scoped: widgets on a home-page board share one chart catalog per mix (§2.5).
builder.Services.AddScoped<ScoreTracker.Web.Services.HomeDashboard.ChartCatalogCache>();
builder.Services.AddScoped<ScoreTracker.Web.Services.HomeDashboard.ByLevelDataSource>();
builder.Services.AddScoped<ScoreTracker.Web.Services.HomeDashboard.CommunityGlowReader>();
builder.Services.AddCookiePolicy(opts =>
{
    opts.CheckConsentNeeded = ctx => false;
    opts.OnAppendCookie = ctx => { ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30); };
});

var app = builder.Build();

// Baseline security headers on every response.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

// AutoMigrate is set by the Aspire AppHost for local dev; everywhere else this only
// logs drift (migrations stay manually applied in production).
await app.Services.ApplyOrReportMigrationsAsync(builder.Configuration["AutoMigrate"] == "true");


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// The retired /StepArtists page 301s to the chart browser, which filters by step artist
// (?StepArtist=) instead of grouping the whole catalog under one expansion panel.
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/StepArtists", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Charts", true);
        return;
    }

    await next();
});

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

// Between authentication and authorization, deliberately (docs/design/culture-resolution.md §4).
// Above UseAuthentication a culture provider cannot see who is asking, which is why a player's
// saved language had no say for years. Below UseAuthorization, HttpContext.User may have been
// replaced by a scheme-specific principal, so an api/* caller authenticating with an ApiToken
// would start receiving its owner's language in output meant to be stable for machines. Here,
// HttpContext.User is the cookie principal or nobody.
var localization = new RequestLocalizationOptions()
    .AddSupportedCultures(SupportedCultures.Codes())
    .AddSupportedUICultures(SupportedCultures.Codes())
    .SetDefaultCulture(SupportedCultures.Default);
// Rank 2, above the cookie: a signed-in player's saved language is the answer. Insert, not Add —
// position IS the ranking, and only an explicit ?culture= (index 0, a deliberately one-request
// preview) may outrank what the account says.
localization.RequestCultureProviders.Insert(1, new UserSettingRequestCultureProvider());
// Appended AFTER the three stock providers, so it only speaks when they found nothing: an
// explicit ?culture= or the saved cookie still wins, and an exactly-supported Accept-Language
// tag is still matched by the stock header provider. What reaches here is the case that used
// to fall through to English — a bare "es"/"ja", or a region we carry no catalogue for
// (es-CL, pt-PT, fr-CA). ResolveClosest maps those down; anything it can't place returns
// null, which leaves the default culture exactly as before.
localization.RequestCultureProviders.Add(new CustomRequestCultureProvider(context =>
{
    foreach (var language in context.Request.GetTypedHeaders().AcceptLanguage
                 .OrderByDescending(l => l.Quality ?? 1d))
    {
        var resolved = SupportedCultures.ResolveClosest(language.Value.Value);
        if (resolved != null) return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(resolved));
    }

    return Task.FromResult<ProviderCultureResult?>(null);
}));
app.UseRequestLocalization(localization);

app.UseRateLimiter();
app.UseAuthorization();
// Required by MapRazorComponents: a static-rendered form posts back to its own endpoint, so the
// endpoint carries an antiforgery requirement and the middleware is what satisfies it. Without
// this every component endpoint throws on the first request. After authorization, before the
// endpoints it protects.
app.UseAntiforgery();

// "/" is the dashboard, which needs an account; a visitor without one goes to the front door
// (docs/design/front-door.md). Middleware rather than a redirect inside the page, because the
// dashboard renders in the circuit: a crawler runs no JS, sees no content, and would never find
// out it was meant to be somewhere else. It has to sit after authentication — ahead of it every
// visitor reads as anonymous, and a signed-in one would bounce between "/" and "/Welcome".
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.Redirect("/Welcome");
        return;
    }

    await next();
});

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
    ("recalculate-chart-similarity",     r => r.PublishRecalculateChartSimilarity(),      "0 12 * * *"), // 07:00 ET — order-independent: reads only the piucenter crawl, not the jobs above

    ("start-leaderboard-import",         r => r.PublishStartLeaderboardImport(),          "30 10 * * 0"), // Sundays 05:30 ET
    // The P2 pumbility board recomputes daily at 01:00 GMT+9 (16:00 UTC); Sundays 16:30 UTC
    // imports right after a fresh recompute. Requires PiuGame:ServiceUsername/ServicePassword
    // (the P2 boards are login-gated) — without them the import fails loudly naming the keys.
    ("start-phoenix2-leaderboard-import", r => r.PublishStartPhoenix2LeaderboardImport(),  "30 16 * * 0"), // Sundays 16:30 UTC
    ("try-schedule-mom",                 r => r.PublishTryScheduleMoM(),                  "0 11 * * *"), // 06:00 ET
    ("process-account-purges",           r => r.PublishProcessAccountPurges(),            "30 11 * * *"), // 06:30 ET — merged-account grace-window purges
    ("crawl-piucenter",                  r => r.PublishCrawlPiuCenter(),                  "0 6 * * 1"),  // Mondays 01:00 ET — gap-driven, near no-op unless piucenter shipped a new data release
    ("purge-player-highlights",          r => r.PublishPurgePlayerHighlights(),           "0 9 * * 0"),  // Sundays 09:00 UTC — 30-day significant-wins retention (payload + community index)
    // The webhook queue lives in SQL, so a delivery survives a restart and this is what picks it
    // back up. Five minutes is well inside the first backoff step, so nothing waits on the sweep.
    ("retry-webhook-deliveries",         r => r.PublishRetryDueWebhookDeliveries(),       "*/5 * * * *"),
    ("prune-webhook-deliveries",         r => r.PublishPruneWebhookDeliveries(),          "0 8 * * *"),  // 08:00 UTC — 7-day bodies, 14-day activity log
    // Refills every account's deep-scan balance on the 1st. One UPDATE across the User table; an
    // unused allowance does not roll over.
    ("reset-deep-scans",                 r => r.PublishResetDeepScans(),                 "0 0 1 * *")
};
if (builder.Configuration["PreventRecurringJobs"] == "true")
{
    // Local dev: we don't want jobs auto-firing, but *removing* them hides them from the Hangfire
    // dashboard — where "Trigger now" is the one-click way to run any of them by hand (no per-job
    // admin buttons needed). So park them on a yearly Jan-1 schedule instead: still registered and
    // visible, manually runnable, but effectively never on their own during a dev session.
    // "0 0 1 1 *" = Jan 1 00:00 UTC.
    foreach (var (id, job, _) in recurringJobs)
        RecurringJob.AddOrUpdate(id, job, "0 0 1 1 *");
}
else
{
    foreach (var (id, job, cron) in recurringJobs)
        RecurringJob.AddOrUpdate(id, job, cron);
}

// Retired jobs come out of Hangfire's SQL storage too — an orphaned row would fail on
// every fire once its runner method is gone.
RecurringJob.RemoveIfExists("refresh-folder-share-cards");
// "purge-community-highlights" became "purge-player-highlights" when the payload moved to
// PlayerProgress and the job grew a second consumer. AddOrUpdate registers the new id; it
// cannot know the old one, which stays in storage pointing at a runner method that is gone.
RecurringJob.RemoveIfExists("purge-community-highlights");

app.UseEndpoints(endpoints => { endpoints.MapControllers(); });


app.MapDefaultEndpoints();
// Serves wwwroot and every RCL's _content/ from the build-time asset manifest. Two things
// follow from that, and both are the point:
//   - Assets requested through @Assets[...] / <ImportMap /> carry a content hash in the FILE
//     NAME (css/site.<hash>.css) and ship Cache-Control: immutable for a year. A release that
//     changes a file changes its URL, so a browser holding the old copy never serves it back.
//   - Assets still requested by their plain name (fonts and images reached from inside CSS)
//     get no-cache plus an ETag, so they revalidate and 304 rather than sitting in the cache
//     for whatever stretch the browser picks on its own.
// Only manifest assets are served; nothing writes to wwwroot at runtime (uploads go to blob
// storage through IFileUploadClient), so there is nothing here the build cannot see.
app.MapStaticAssets();
// Real Razor Pages (the static front door) route ahead of the Blazor fallback —
// AddRazorPages() alone only wires services, not endpoints.
app.MapRazorPages();
// Component routes are real endpoints now, not a fallback — which is why "/" had to stop being
// claimed by both the front door and the dashboard.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Exposes the top-level-statement entry point to WebApplicationFactory so the E2E
// suite can host the real app on Kestrel.
public partial class Program
{
}

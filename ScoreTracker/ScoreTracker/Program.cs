using BlazorApplicationInsights;
using Hangfire;
using Hangfire.SqlServer;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi;
using MudBlazor.Services;
using ScoreTracker.Application.Handlers;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Data.Repositories;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.PersonalProgress;
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
var sqlConfig = builder.Configuration.GetSection("SQL").Get<SqlConfiguration>()!;
builder.Services.AddMassTransit(o =>
{
    o.AddConsumers(typeof(PlayerRatingSaga).Assembly, typeof(TierListSaga).Assembly,
        typeof(RecurringJobRunner).Assembly);

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
    .AddDiscord("Discord", o =>
    {
        o.ClientId = discordConfig.ClientId;
        o.ClientSecret = discordConfig.ClientSecret;
    })
    .AddGoogle("Google", o =>
    {
        o.ClientId = googleConfig.ClientId;
        o.ClientSecret = googleConfig.ClientSecret;
    })
    .AddFacebook("Facebook", o =>
    {
        o.AppId = facebookConfig.AppId;
        o.AppSecret = facebookConfig.AppSecret;
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
    .AddHttpContextAccessor()
    .AddHttpClient()
    .AddHostedService<BotHostedService>()
    .AddMediatR(o =>
    {
        o.RegisterServicesFromAssemblies(
            typeof(UpdateXXBestAttemptHandler).Assembly
            , typeof(MainLayout).Assembly, typeof(EFPlayerStatsRepository).Assembly,
            typeof(PlayerRatingSaga).Assembly);
    })
    .AddTransient<IUserAccessService, UserAccessService>()
    .AddTransient<IWorldRankingService, WorldRankingService>()
    .AddInfrastructure(builder.Configuration.GetSection("AzureBlob").Get<AzureBlobConfiguration>(),
        sqlConfig,
        builder.Configuration.GetSection("Sendgrid").Get<SendGridConfiguration>())
    .AddTransient<IDateTimeOffsetAccessor, DateTimeOffsetAccessor>()
    .AddTransient<IRandomNumberGenerator, RandomNumberGenerator>()
    .AddSingleton<IPlayerScoreBatchAccumulator, PlayerScoreBatchAccumulator>()
    .AddControllers();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<IStringLocalizer<App>, StringLocalizer<App>>();
builder.Services.AddScoped<ChartVideoDisplayer>();
builder.Services.AddCookiePolicy(opts =>
{
    opts.CheckConsentNeeded = ctx => false;
    opts.OnAppendCookie = ctx => { ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30); };
});

var app = builder.Build();


app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures("en-US", "pt-BR", "ko-KR", "en-ZW", "es-MX", "fr-FR", "ja-JP", "it-IT")
    .AddSupportedUICultures("en-US", "pt-BR", "ko-KR", "en-ZW", "es-MX", "fr-FR", "ja-JP", "it-IT")
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
    ("update-weekly-charts",             r => r.PublishUpdateWeeklyCharts(),              "0 9 * * *"),  // 04:00 ET
    ("process-pass-tier-list",           r => r.PublishProcessPassTierList(),             "30 9 * * *"), // 04:30 ET
    ("calculate-chart-letter-difficulties", r => r.PublishCalculateChartLetterDifficulties(), "0 10 * * *"), // 05:00 ET
    ("start-leaderboard-import",         r => r.PublishStartLeaderboardImport(),          "30 10 * * 0"), // Sundays 05:30 ET
    ("try-schedule-mom",                 r => r.PublishTryScheduleMoM(),                  "0 11 * * *")  // 06:00 ET
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


app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

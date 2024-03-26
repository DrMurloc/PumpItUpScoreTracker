using System.Text.Json;
using BlazorApplicationInsights;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi.Models;
using MudBlazor.Services;
using OfficeOpenXml;
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

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<JsonSerializerOptions>(o =>
{
    o.Converters.Add(Name.Converter);
    o.Converters.Add(PhoenixScore.Converter);
});

ExcelPackage.LicenseContext = new LicenseContext();
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromHours(1);
});
var discordConfig = builder.Configuration.GetSection("Discord").Get<DiscordConfiguration>();
var googleConfig = builder.Configuration.GetSection("Google").Get<GoogleConfiguration>();
var facebookConfig = builder.Configuration.GetSection("Facebook").Get<FacebookConfiguration>();
builder.Services.Configure<DiscordConfiguration>(builder.Configuration.GetSection("Discord"));
builder.Services.AddMassTransit(o =>
{
    o.AddConsumers(typeof(PlayerRatingSaga).Assembly, typeof(TierListSaga).Assembly);
    o.UsingInMemory((context, cfg) => { cfg.ConfigureEndpoints(context); });
});
builder.Services.AddAuthentication("DefaultAuthentication")
    .AddCookie("DefaultAuthentication", o =>
    {
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.Cookie.MaxAge = o.ExpireTimeSpan;
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
    o.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        In = ParameterLocation.Header,
        Scheme = "basic",
        Description = "ApiToken from Account page. Put anything in for username."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            new string[] { }
        }
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
    .AddMediatR(typeof(UpdateXXBestAttemptHandler), typeof(MainLayout), typeof(EFPlayerStatsRepository),
        typeof(PlayerRatingSaga))
    .AddTransient<IUserAccessService, UserAccessService>()
    .AddTransient<IWorldRankingService, WorldRankingService>()
    .AddInfrastructure(builder.Configuration.GetSection("AzureBlob").Get<AzureBlobConfiguration>(),
        builder.Configuration.GetSection("SQL").Get<SqlConfiguration>(),
        builder.Configuration.GetSection("Sendgrid").Get<SendGridConfiguration>())
    .AddTransient<IDateTimeOffsetAccessor, DateTimeOffsetAccessor>()
    .AddControllers();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<IStringLocalizer<App>, StringLocalizer<App>>();

builder.Services.AddCookiePolicy(opts =>
{
    opts.CheckConsentNeeded = ctx => false;
    opts.OnAppendCookie = ctx => { ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(30); };
});


var app = builder.Build();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .AddSupportedCultures("en-US", "pt-BR", "ko-KR", "en-ZW", "es-MX")
    .AddSupportedUICultures("en-US", "pt-BR", "ko-KR", "en-ZW", "es-MX")
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

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints => { endpoints.MapControllers(); });


app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
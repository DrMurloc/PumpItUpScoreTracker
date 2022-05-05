using BlazorApplicationInsights;
using MudBlazor.Services;
using OfficeOpenXml;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Accessors;
using ScoreTracker.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);


ExcelPackage.LicenseContext = new LicenseContext();
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
var discordConfig = builder.Configuration.GetSection("Discord").Get<DiscordConfiguration>();
var googleConfig = builder.Configuration.GetSection("Google").Get<GoogleConfiguration>();
var facebookConfig = builder.Configuration.GetSection("Facebook").Get<FacebookConfiguration>();
builder.Services.AddAuthentication("DefaultAuthentication")
    .AddCookie("DefaultAuthentication")
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
    });
builder.Services.AddBlazorApplicationInsights()
    .AddMudServices()
    .AddTransient<ICurrentUserAccessor, HttpContextUserAccessor>()
    .AddHttpContextAccessor()
    .AddHttpClient()
    .AddCore()
    .AddInfrastructure(builder.Configuration.GetSection("SQL").Get<SqlConfiguration>())
    .AddTransient<IDateTimeOffsetAccessor, DateTimeOffsetAccessor>()
    .AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints => { endpoints.MapControllers(); });


app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
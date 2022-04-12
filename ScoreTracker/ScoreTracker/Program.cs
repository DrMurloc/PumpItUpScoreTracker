using ScoreTracker.CompositionRoot;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Accessors;
using ScoreTracker.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

builder.Services.AddCore()
    .AddInfrastructure()
    .AddTransient<ICurrentUserAccessor, HardCodedUserAccessor>()
    .AddTransient<IDateTimeOffsetAccessor, DateTimeOffsetAccessor>();

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
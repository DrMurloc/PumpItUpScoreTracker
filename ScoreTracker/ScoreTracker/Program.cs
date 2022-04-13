using System.ComponentModel;
using MudBlazor.Services;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Accessors;
using ScoreTracker.Web.TypeConverters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddMudServices()
    .AddCore()
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

//<Data seeding>, remove on external persistence refactor

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<ChartAttemptDbContext>();

    var songId = Guid.NewGuid();
    var songs = new SongEntity[]
    {
        new()
        {
            Id = songId,
            Name = "I'm So Sick"
        }
    };
    var charts = new ChartEntity[]
    {
        new()
        {
            Id = Guid.NewGuid(),
            Level = 3,
            SongId = songId,
            Type = ChartType.Single.ToString()
        },
        new()
        {
            Id = Guid.NewGuid(),
            Level = 14,
            SongId = songId,
            Type = ChartType.Double.ToString()
        },
        new()
        {
            Id = Guid.NewGuid(),
            Level = 28,
            SongId = songId,
            Type = ChartType.DoublePerformance.ToString()
        },
        new()
        {
            Id = Guid.NewGuid(),
            Level = 20,
            SongId = songId,
            Type = ChartType.SinglePerformance.ToString()
        },
        new()
        {
            Id = Guid.NewGuid(),
            Level = 3,
            SongId = songId,
            Type = ChartType.CoOp.ToString()
        }
    };
    database.Song.AddRange(songs);
    database.Chart.AddRange(charts);
    database.SaveChanges();
}

//</Data seeding>


AssignTypeConverter<Chart, ChartTypeConverter>();

app.Run();

void AssignTypeConverter<TType, TConverterType>()
{
    TypeDescriptor.AddAttributes(typeof(TType), new TypeConverterAttribute(typeof(TConverterType)));
}
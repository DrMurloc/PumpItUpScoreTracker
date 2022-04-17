using System.ComponentModel;
using MudBlazor.Services;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Configuration;
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
    .AddInfrastructure(builder.Configuration.GetSection("SQL").Get<SqlConfiguration>())
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


AssignTypeConverter<Chart, ChartTypeConverter>();

app.Run();

void AssignTypeConverter<TType, TConverterType>()
{
    TypeDescriptor.AddAttributes(typeof(TType), new TypeConverterAttribute(typeof(TConverterType)));
}
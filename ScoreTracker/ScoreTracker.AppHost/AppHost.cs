// Local-dev orchestration (2026-07 workshop): AppHost is the single config authority for
// running the site locally. Non-secret local defaults (SQL container password/port) live in
// this project's appsettings.json; secrets live in THIS project's user-secrets and flow
// through to the Web app as environment variables - the Web app's configuration code is
// unchanged and never needs its own local secrets.
using Microsoft.Extensions.Configuration;
var builder = DistributedApplication.CreateBuilder(args);

// Deterministic SQL Server container: pinned sa password + host port from appsettings.json,
// persistent volume so data survives restarts. The pinned values also let you connect SSMS
// (or anything else) straight to localhost,<port> with the same committed credentials.
var saPassword = builder.AddParameter("sql-password", builder.Configuration["Sql:Password"]!);
var sqlPort = int.Parse(builder.Configuration["Sql:HostPort"]!);

// Same engine version the Testcontainers integration suite uses - one cached image,
// no engine drift between local dev and the real-DB tests.
var sql = builder.AddSqlServer("sql", saPassword, port: sqlPort)
    .WithImageTag("2025-latest")
    .WithDataVolume("scoretracker-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = sql.AddDatabase("ScoreTracker");

var web = builder.AddProject<Projects.ScoreTracker_Web>("web")
    .WithExternalHttpEndpoints()
    // The app reads SQL:ConnectionString (see SqlConfiguration) - flow the container's
    // connection string into that section rather than the Aspire-conventional
    // ConnectionStrings__* name, so production config wiring stays untouched.
    .WithEnvironment("SQL__ConnectionString", database)
    // Dev-harness prod sync: token comes from AppHost user-secrets
    // (dotnet user-secrets set "ProdSync:ApiToken" "<token>" --project ScoreTracker.AppHost).
    .WithEnvironment("ProdSync__ApiToken", builder.Configuration["ProdSync:ApiToken"] ?? string.Empty)
    .WithEnvironment("ProdSync__BaseUrl", builder.Configuration["ProdSync:BaseUrl"] ?? "https://piuscores.arroweclip.se")
    // Running under Aspire IS the local-dev signal: migrations auto-apply and the
    // DevAuth login backdoor lights up. Plain `dotnet run` gets neither.
    .WithEnvironment("AutoMigrate", "true")
    .WithEnvironment("DevAuth__Enabled", "true")
    .WaitFor(database);

// Copy-paste secret flow-through: any values set in AppHost user-secrets under these
// sections are forwarded to the Web app verbatim, so prod secrets can be pasted as-is
// (dotnet user-secrets set "Google:ClientId" "..." --project ScoreTracker.AppHost).
// SQL is deliberately NOT forwarded - the container's connection string always wins
// locally, so a pasted prod connection string can never point local dev at prod.
// Caution: pasting Discord:BotToken makes the LOCAL app connect as the real prod bot
// (two live gateway sessions, slash commands answered from an empty local DB) - leave
// it out unless that's what you want; OAuth ClientId/ClientSecret are harmless.
string[] forwardedSections = ["Discord", "Google", "Facebook", "AzureBlob", "Sendgrid"];
foreach (var sectionName in forwardedSections)
foreach (var entry in builder.Configuration.GetSection(sectionName).AsEnumerable())
    if (entry.Value is not null)
        web.WithEnvironment(entry.Key.Replace(":", "__"), entry.Value);

builder.Build().Run();

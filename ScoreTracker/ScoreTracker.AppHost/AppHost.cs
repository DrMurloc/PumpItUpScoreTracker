// Local-dev orchestration (2026-07 workshop): AppHost is the single config authority for
// running the site locally. Non-secret local defaults (SQL container password/port) live in
// this project's appsettings.json; secrets (prod API token for the dev harness, optional
// real OAuth creds) live in THIS project's user-secrets and flow through to the Web app as
// environment variables â the Web app's configuration code is unchanged and never needs its
// own local secrets.
var builder = DistributedApplication.CreateBuilder(args);

// Deterministic SQL Server container: pinned sa password + host port from appsettings.json,
// persistent volume so data survives restarts. The pinned values also let you connect SSMS
// (or anything else) straight to localhost,<port> with the same committed credentials.
var saPassword = builder.AddParameter("sql-password", builder.Configuration["Sql:Password"]!);
var sqlPort = int.Parse(builder.Configuration["Sql:HostPort"]!);

// Same engine version the Testcontainers integration suite uses — one cached image,
// no engine drift between local dev and the real-DB tests.
var sql = builder.AddSqlServer("sql", saPassword, port: sqlPort)
    .WithImageTag("2025-latest")
    .WithDataVolume("scoretracker-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = sql.AddDatabase("ScoreTracker");

builder.AddProject<Projects.ScoreTracker_Web>("web")
    .WithExternalHttpEndpoints()
    // The app reads SQL:ConnectionString (see SqlConfiguration) â flow the container's
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

builder.Build().Run();

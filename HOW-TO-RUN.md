# How to Run

Running the site locally is one command once the prerequisites are installed. No credentials, no manual database setup, no config editing — the local orchestration provisions everything, and a guided setup page populates your database with real chart data from the live site.

## Prerequisites

Install these once:

| Tool | What it's for | Get it |
|---|---|---|
| **Git** | Getting the code | <https://git-scm.com/downloads> (or [GitHub Desktop](https://desktop.github.com/) if you prefer a UI) |
| **.NET 10 SDK** | Building and running the solution | <https://dotnet.microsoft.com/download/dotnet/10.0> |
| **Docker Desktop** | The local SQL Server container (and the integration test suite) | <https://www.docker.com/products/docker-desktop/> — the free Personal tier is fine |
| **An IDE** (optional but recommended) | Editing and debugging | [Visual Studio 2026](https://visualstudio.microsoft.com/) or newer (VS 2022 cannot target .NET 10), [JetBrains Rider](https://www.jetbrains.com/rider/), or [VS Code](https://code.visualstudio.com/) with the C# Dev Kit |

### Checking out the code (new to git?)

```sh
git clone https://github.com/DrMurloc/PumpItUpScoreTracker.git
cd PumpItUpScoreTracker
```

That downloads the repository into a `PumpItUpScoreTracker` folder. If you plan to contribute, ping DrMurloc on [Discord](https://discord.gg/AvS5PxnvSN) for repo access, then branch from `main` — see [CONTRIBUTING.md](CONTRIBUTING.md).

## Getting your environment set up

**1. Start Docker Desktop** (it just needs to be running — you don't have to touch it).

**2. Run the AppHost:**

```sh
dotnet run --project ScoreTracker/ScoreTracker.AppHost
```

(Or open `ScoreTracker/ScoreTracker.sln` in your IDE and F5 the **ScoreTracker.AppHost** project.)

This boots the [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) local orchestration, which:

- provisions a **SQL Server container** with a pinned password and port (from [AppHost appsettings.json](ScoreTracker/ScoreTracker.AppHost/appsettings.json)) and a persistent volume, so your data survives restarts;
- **applies all EF migrations automatically** at startup;
- enables the **dev login backdoor** so you don't need OAuth credentials to sign in;
- opens the **Aspire dashboard** (logs, traces, resource states) in your browser.

Click through to the **web** resource in the dashboard (or go to `https://localhost:7144`).

**3. Populate your database.** On an empty database the site redirects you to the **Set Up Local Database** page, which walks you through three steps:

1. **Sign in** — click *Create Dev User & Sign In* (no credentials needed; this is the dev backdoor, disabled outside local runs). On later runs you can sign back in as the same user.
2. **Paste your API token** — from your [Account page](https://piuscores.arroweclip.se/Account) on the live site (create a free account there if you don't have one). The token is used read-only and stored in your local database.
3. **Populate** — pulls the full chart/song catalog, tier lists, scoring levels, and *your own scores* from the live site into your local database. Takes a minute or two.

That's it — you have a working local copy of the site with real data.

### Poking at the database directly

While the AppHost is running, connect SSMS / Azure Data Studio to `localhost,14330`, user `sa`, password `LocalDev_Passw0rd!` (both pinned in the AppHost's appsettings.json). Note the port is an Aspire proxy — it's only listening while the AppHost runs.

## Configuring locally

**Nothing is mandatory.** The AppHost supplies everything the core loop needs (database, login, migrations). Secrets exist only to light up specific integrations, and they go in the **AppHost's user-secrets** — the AppHost forwards them to the web app as environment variables, so you configure exactly one place:

```sh
dotnet user-secrets set "Google:ClientId" "..." --project ScoreTracker/ScoreTracker.AppHost
```

| Section | Feature it lights up | Where to get credentials | If unset |
|---|---|---|---|
| `Discord:ClientId` / `Discord:ClientSecret` | "Sign in with Discord" | [Discord Developer Portal](https://discord.com/developers/applications) → New Application → OAuth2. Add `https://localhost:7144/Login/Discord/Callback` as a redirect URI. | Button hidden locally; dev login covers you |
| `Google:ClientId` / `Google:ClientSecret` | "Sign in with Google" | [Google Cloud Console](https://console.cloud.google.com/apis/credentials) → OAuth client ID. | Button hidden locally |
| `Facebook:AppId` / `Facebook:AppSecret` | "Sign in with Facebook" | [Meta for Developers](https://developers.facebook.com/) | Button hidden locally |
| `Discord:BotToken` | The Discord bot | Same Discord application → Bot tab. **Careful:** pasting the *production* bot token makes your local app connect as the real bot. Use your own bot application. | Bot host no-ops with a log warning |
| `Sendgrid:ApiKey` | Admin notification emails | [SendGrid](https://sendgrid.com/) account | Sends fail quietly at the call site; nothing else affected |
| `AzureBlob:ConnectionString` | Photo uploads (tournament verification, qualifiers) | See below | Defaults to the Azurite emulator connection string |

All of these are **bring-your-own credentials**: if you want to test a feature that needs one, create your own (Discord application, Google OAuth client, Meta app, SendGrid account, storage account). No credentials are provided for local development — production ones are not shared.

**SQL is deliberately not configurable through secrets** — the AppHost always injects the container's connection string, so a pasted production connection string can never point your local environment at prod.

### Azure Blob Storage / Azurite

Photo-upload features store files in Azure Blob Storage. Locally, [appsettings.Development.json](ScoreTracker/ScoreTracker/appsettings.Development.json) already points the client at the local emulator (`UseDevelopmentStorage=true`), and the client doesn't connect until a file operation happens — so **the app runs fine with no blob storage at all**; only the moment you actually upload a photo will error.

To exercise uploads for real, run [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) in Docker:

```sh
docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0
```

Caveat: uploaded-image *URLs* are currently hardcoded to the production CDN host (`piuimages.arroweclip.se`), so even with Azurite the uploaded images won't render back locally. Blob-backed features are only partially testable locally today.

### Other knobs

- `PreventRecurringJobs=true` (already set in `appsettings.Development.json`) keeps the [Hangfire recurring jobs](SCHEDULED-JOBS.md) from running locally. Remove it if you're working on one of those jobs; you can also trigger any job manually from the `/hangfire` dashboard (admin only).
- Running the web project directly (`dotnet run` on `ScoreTracker.Web`, without the AppHost) is unsupported for local dev: you'd need to supply your own SQL connection string, apply migrations yourself, and you won't get the dev login. Use the AppHost.

## Running the tests

See [HOW-TO-TEST.md](HOW-TO-TEST.md). Short version: the fast suites need no Docker; the real-database integration suite provisions its own SQL container and needs Docker Desktop running.

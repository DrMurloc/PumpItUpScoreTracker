# Technologies

What's in the stack, what each piece contributes, and where it's wired. Everything targets **.NET 10** (`net10.0`, nullable + implicit usings on).

## Runtime & UI

### ASP.NET Core
One process hosts everything: Razor Pages bootstrapping, **Blazor Server** for the UI, and **MVC controllers** for the [HTTP API](API.md). Composition happens in [`Program.cs`](../ScoreTracker/ScoreTracker/Program.cs) — the single place where authentication, MediatR, MassTransit, Hangfire, localization, and DI wiring all meet.

### Blazor Server
The UI model. Pages run on the server; the browser holds a SignalR (WebSocket) connection and receives DOM diffs. Implications: page code can inject services and hit the database directly through MediatR (no client API layer needed), but every interaction is a server round-trip, and a "circuit" holds per-user state. Pages live in [`Pages/`](../ScoreTracker/ScoreTracker/Pages/), grouped by feature.

### MudBlazor
The Blazor component library — tables, dialogs, autocompletes, the app bar and drawer in `MainLayout.razor`. UI work is almost entirely MudBlazor composition; there's very little hand-rolled CSS.

### ApexCharts (Blazor bindings)
Charting for progress/stat visualizations.

### DartSassBuilder
Compiles the repo's lone Sass file ([charts.scss](../ScoreTracker/ScoreTracker/wwwroot/css/charts.scss) → committed `charts.css`, compressed) on build. Chosen because it runs on any OS — its predecessor (`Delegate.SassBuilder`) shipped a Windows-only compiler binary that broke the Linux E2E CI job; anything replacing it must stay cross-platform. Its build tool targets **net8.0**: machines with only a newer runtime need `DOTNET_ROLL_FORWARD=Major` (CI sets this as a pipeline variable; dev machines normally have a net8 runtime around).

### Static asset delivery (`MapStaticAssets`)
The framework's build-time asset pipeline, in place of `UseStaticFiles`. Every file under `wwwroot` (and every RCL's `_content/`) is published twice: under its plain name, and under a content-hashed one (`css/site.ivd8qpcnra.css`), gzip-precompressed. Razor components ask for the hashed name via `@Assets["css/site.css"]` and get `Cache-Control: max-age=31536000, immutable` back. **A release that changes a file changes its URL**, which is what stops a browser from painting the previous release's stylesheet over the new markup; the hand-bumped `nav.js?v=3` was the manual version of this. Anything still requested by its plain name (fonts and images referenced from inside CSS) gets `no-cache` + an ETag, so it revalidates and 304s instead of being heuristically cached for however long the browser felt like.

⚠ **`@Assets` is components-only.** The collection behind it belongs to the Blazor component endpoint and is *not* registered in DI, so a Razor Page that does `@inject ResourceAssetCollection` throws `No service for type ...` at render time and returns a 500 — which, on `FrontDoor.cshtml`, is the site's most-linked public page. Razor Pages version their assets with the `asp-append-version="true"` TagHelper instead (a content hash on the query, which is as far as that surface goes).

**In Development, `MapStaticAssets` answers `no-cache` for every asset**, hashed ones included, so local work never fights the browser cache. The immutable year-long headers are a property of the shipped manifest (`ScoreTracker.Web.staticwebassets.endpoints.json`) and only appear at runtime outside Development — which is why the E2E suite, which hosts in Development, pins them by reading the manifest rather than the response.

The three ES modules the circuit imports at runtime (`./js/helpers.js`, `./js/life-calculator.js`, `./js/phoenix-recap.js`) have no tag to carry a hashed name, so `<ImportMap />` in `App.razor` rewrites those specifiers in the browser — the C# call sites keep asking for the plain path. `_framework/blazor.web.js` stays on its plain name; the framework versions its own boot script.

DartSassBuilder runs **before** the manifest is fingerprinted (verified: editing `charts.scss` moves the hash on `charts.<hash>.css`), so a Sass-only change busts the cache like any other. Ratcheted by `StaticAssetVersioningTests`.

### Localization (resx)
Eight locales (`en-US`, `pt-BR`, `ko-KR`, `en-ZW`, `es-MX`, `fr-FR`, `ja-JP`, `it-IT`). A scoped `IStringLocalizer<App>` is injected globally as `L` ([_Imports.razor](../ScoreTracker/ScoreTracker/_Imports.razor)); resource keys are the **English UI text verbatim**. Per-locale translation glossaries live at the repo root (`LOCALIZATION-<locale>.md`). New keys must be populated in every locale's resx in the same pass.

## Application core

### MediatR
In-process use-case dispatch — the seam between presentation and everything else. Razor pages and API controllers inject `IMediator` and send commands/queries; handlers live in `Application/Handlers/` or inside the owning vertical. **No page or controller touches a repository or DbContext directly.** The `IQuery<T>` marker (SharedKernel) distinguishes reads from commands.

### MassTransit (in-memory transport)
Asynchronous work: past-tense **events** (`Domain/Events/`, vertical `Contracts/Events/`) and imperative **trigger messages** (`Application/Messages/`) published via `IBus`. Consumers are the feature-grouped "Saga" classes inside verticals, registered through each vertical's `AddXxxConsumers` hook (MassTransit's assembly scan can't see `internal` types). The in-memory transport is the *production* transport — cheap and fast, but **mid-flight messages die with the process**; design consumers idempotent and use Hangfire for anything that must re-fire.

### Hangfire (SQL Server storage)
Recurring scheduled work — see [SCHEDULED-JOBS.md](SCHEDULED-JOBS.md). Schedules persist in the auto-created `HangFire` SQL schema and survive restarts (unlike bus messages). Each job is a one-liner that publishes a MassTransit message. Dashboard at `/hangfire`, admin-gated. Do not introduce a second scheduler.

## Data

### EF Core + SQL Server
One `DbContext` ([`ChartAttemptDbContext`](../ScoreTracker/ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs)) for the whole database — see [DATABASE-SCHEMA.md](DATABASE-SCHEMA.md). Verticals contribute their own entities via `IDbModelContribution`. Repositories implement Domain ports (`EF<Port>` naming) and create scoped contexts through `IDbContextFactory`. Migrations are applied by a self-contained **EF migration bundle** during the gated deploy (never at startup in production); the local AppHost auto-migrates.

### Azure Blob Storage
Photo storage (tournament verification, qualifiers) through the `IFileUploadClient` port. Locally defaults to the Azurite emulator connection string — see [HOW-TO-RUN.md](HOW-TO-RUN.md) for the caveats.

## Local development

### .NET Aspire (AppHost)
Local orchestration ([`ScoreTracker.AppHost`](../ScoreTracker/ScoreTracker.AppHost/AppHost.cs)): a deterministic SQL Server container (pinned port/password, persistent volume), automatic migrations, the dev-login backdoor, config/secret flow-through to the web app, and a dashboard with logs and traces. Running the AppHost **is** the local-dev signal — plain `dotnet run` on the web project gets none of this.

## Integrations

### Discord.Net
The Discord bot (`BotHostedService`): slash commands and community-channel notifications fed by the Communities vertical's event streams. No-ops gracefully when no bot token is configured.

### HtmlAgilityPack + typed HttpClient (`PiuGameApi`)
The anti-corruption layer against the official PiuGame website — HTML scraping for leaderboards, avatars, and score imports. Owned by the **OfficialMirror** vertical.

### SendGrid
Admin notification emails. Fails quietly at the call site when unconfigured.

### CsvHelper
Spreadsheet parsing for the bulk score-upload page (`PhoenixScoreFileExtractor` reads Song/Difficulty/Score/Plate columns out of uploaded CSVs). There is no OCR — a Tesseract experiment from the XX era was removed 2026-07 without ever shipping.

### OAuth providers (Discord / Google / Facebook)
Cookie-based auth (`DefaultAuthentication`, 30-day sliding) with three OAuth challenge providers, plus a custom **ApiToken** Basic-auth scheme for API callers. All wired in `Program.cs`.

### Swashbuckle (Swagger)
OpenAPI generation for the partner API — `/swagger/ui`. The source of truth for API shapes ([API.md](API.md)).

### Application Insights (Blazor)
Client-side telemetry; no-ops without an instrumentation key.

## Testing

### xUnit + Moq
The only test frameworks — see [HOW-TO-TEST.md](HOW-TO-TEST.md).

### Testcontainers.MsSql + Respawn
The integration and E2E suites provision a real SQL Server 2025 container per run (same image tag the AppHost uses — no engine drift), apply migrations, and Respawn resets state between tests.

### Playwright (+ Mvc.Testing Kestrel host)
The E2E suite drives a headless Chromium against the real web app, hosted in-process on Kestrel via .NET 10's `WebApplicationFactory.UseKestrel`. The browser downloads automatically on first run.

### WireMock.Net
Stands in for phoenix.piugame.com in the E2E suite, serving PII-scrubbed snapshot pages (`ScoreTracker.Tests.E2E/PiuGame/Fixtures/`) so logins and score imports never touch the real site. The app's PIU endpoints come from the `PiuGame` config section (defaults = production hosts).

## Delivery

### Azure Pipelines
[Multi-stage YAML](../azure-pipelines.yml): build + all test suites on every PR and merge; merges to `main` continue into an **approval-gated production deploy** that first applies the EF migration bundle, then zip-deploys the app to Azure App Service.

### SonarQube Cloud
Quality/maintainability analysis wrapping the Windows build job (prepare → build → tests → analyze → publish quality gate); PR decoration via the SonarQube Cloud GitHub app. EF `Migrations/` are excluded — generated code would swamp the metrics. Free for public repos.

### CodeQL (GitHub code scanning)
Security analysis (taint tracking, injection flows) on pushes and PRs, via GitHub's default setup — no workflow file in the repo; configured under Settings → Code security. Free for public repos.

### Dependabot
Dependency alerts + auto security-fix PRs (repo settings) and weekly grouped NuGet version bumps ([.github/dependabot.yml](../.github/dependabot.yml) — minor/patch in one PR; MassTransit majors ignored, the repo is deliberately on 7.x). Bump PRs run the full pipeline including E2E.

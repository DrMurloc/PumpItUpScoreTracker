# ScoreTracker.ExplorationTests

A **manual-only workbench**, not a test suite. It is named in no `azure-pipelines.yml`
`dotnet test` step, so it never runs in CI, and it is excluded from Sonar analysis. It builds
with the solution only so the compiler keeps it honest against the code it pokes at.

## What it's for

A sanctioned place to:

- **Crawl live pages** — `phoenix.piugame.com` (the real PIU site) through the app's own
  `PiuGameApi`, so exploration uses the same parser production does.
- **Download files** — jackets, avatars, score screenshots — to a scratch location for
  inspection or as fixtures for a later feature.
- **Try formulas against real data/images/files** — reconcile a rating/pumbility formula against
  mirrored boards, and (soon) run OCR experiments against real score-screen images.

## The rules

- **Read-only by default.** A test here issues GETs and computes; it does not POST, PUT, DELETE,
  or otherwise mutate a remote system **unless the owner explicitly asks for that run to mutate.**
  The one standing exception is the Discord canary, which posts sample cards to the owner's own
  lab channel by design (`[DiscordCanaryFact]`, manual-only).
- **Manual only.** Every test is gated behind a config check (`[LiveSiteFact]`,
  `[DiscordCanaryFact]`, `[SessionShowcaseFact]`) and **skips** when the credentials/channel
  aren't configured — which is always in CI. Nothing here is a feature test; a formula that must
  be guaranteed belongs in `ScoreTracker.Tests` (unit) or `ScoreTracker.Tests.Integration`
  (real DB).

## Running it

Configure the shared AppHost user-secrets store once (or the env vars), then run by path:

```
# PIU live-site probes (LiveSite/)
dotnet user-secrets set "PiuTest:Username" "..." --project ScoreTracker/ScoreTracker.AppHost
dotnet user-secrets set "PiuTest:Password" "..." --project ScoreTracker/ScoreTracker.AppHost
#   or PIU_TEST_USERNAME / PIU_TEST_PASSWORD env vars

# Discord canary (DiscordCanary/)
dotnet user-secrets set "Discord:BotToken" "..." --project ScoreTracker/ScoreTracker.AppHost
dotnet user-secrets set "DiscordTest:CanaryChannelId" "..." --project ScoreTracker/ScoreTracker.AppHost
#   or DISCORD_CANARY_TOKEN / DISCORD_CANARY_CHANNEL env vars

dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj \
  --filter "FullyQualifiedName~PiuGameLiveSiteTests"
```

Without credentials configured, every test skips — the assembly is inert.

## Catalog probes (`Catalog/`)

Checks against a **real, populated catalog** — the owner's prod-synced local Aspire database or a
read replica. `TitleChartResolutionProbeTests` asserts every chart-specific title in both title
lists resolves to a chart that exists; a title naming a song the way the official requirement page
abbreviates it matches nothing and stays frozen at zero forever without erroring. Read-only
SELECTs, and gated like the rest — CI's database is a migrated empty schema, where every title
would fail for the wrong reason.

```sh
dotnet user-secrets set "CatalogProbe:ConnectionString" "Server=127.0.0.1,14330;Database=ScoreTracker;User Id=sa;Password=...;TrustServerCertificate=true" \
  --project ScoreTracker/ScoreTracker.AppHost
#   or the SCORETRACKER_CATALOG_CONNECTION env var

dotnet test ScoreTracker/ScoreTracker.ExplorationTests/ScoreTracker.ExplorationTests.csproj \
  --filter "FullyQualifiedName~TitleChartResolutionProbe"
```

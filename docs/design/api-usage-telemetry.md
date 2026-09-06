# API usage — the console counters and the request trace

Status: **built** (2026-09-06). Owner's ask on 2026-09-05, after a census of the prod-synced local
database: *"I want data on how tools are using us"* — and whether to lean on telemetry or SQL for
it. Decided: both, split by who reads the number. SQL carries the two counters the maker console
already promises; one structured log line per API request carries everything the owner analyses.
Anything not written here is not in scope.

## 1. What the census found

- `ToolActivity` had rows for deliveries and directory clicks and nothing else. The kinds the
  console sums — `KeyUsed`, `RateLimited`, `KeyExpired`, `PlayerConnected`, `PlayerDisconnected` —
  had no writer anywhere in the solution, so every maker's API card read 0 calls and 0 rate-limited
  from the day it shipped. The only key-traffic signal in the database was `ToolApiKey.LastUsedAt`.
- PIU Tracker, the largest integrator by far, holds no tool key. It reads on its maker's personal
  token, and v1 accepts personal tokens only. No tool-keyed counter can ever show it.
- The repository ships no server-side telemetry. `ServiceDefaults` exports OpenTelemetry to OTLP
  only when an endpoint is configured (the Aspire dashboard, locally), the Azure Monitor block is
  commented out, and Web references only the browser-side Application Insights package. What the
  `piuscores` resource holds — requests, dependencies, traces — comes from the App Service's own
  auto-instrumentation, and `ILogger` lines reach it: the `ScoringObservation` traces are the proof.

## 2. Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **SQL for the console counters, a log line for the analysis.** | The console has to read its numbers back, exactly, for the life of the tool; the owner's questions are per-endpoint, per-credential and per-day, which KQL answers in one line and a SQL table would need a page for. |
| 2 | **A call is counted when the key resolves**, not when the response leaves. | Authenticated is used. Rate-limited requests never reach the scheme, so nothing double-counts. |
| 3 | **The cache lives in the vertical**, keyed by the key's hash, five minutes from the last successful resolve — absolute, never sliding — and evicted the moment a resolve comes back revoked or expired. | The limiter runs before the v2 scheme, so a rejected request is anonymous there; the cache is what turns a credential back into a tool. A sliding window would have been refreshed by the rejection path's own reads, so a key revoked mid-flood would have kept counting for as long as the flood lasted. Credential vocabulary stays where `ApiKeyMint` lives. |
| 4 | **A cache miss on the rejected path counts nothing.** | A tool is only limited after hundreds of successes inside the same minute, so the cache is warm by construction. The one way to miss is a restart mid-storm, and losing that hour's tally is the cheaper failure. |
| 5 | **Write-through on every count, and every count is best-effort.** | Today's volume is tens of requests a minute. An in-memory meter is the escalation if a storm ever shows on the DTU graph, not the starting point. A tally that cannot be written is logged as a warning and lost: telemetry could not break authentication before this, and a counter must never fail the call it counts. The rejection hook's count runs on its own token, because a flooding client that hangs up before its 429 is exactly the request worth counting. |
| 6 | **The key's name travels as a claim.** | The middleware never asks the database for what the scheme already knew. |
| 7 | **Every `/api/` request is logged**, v1 and v2, anonymous included. Ids and names only, never a credential. | PIU Tracker only appears through the personal tier; an unauthenticated burst is the thing a rate limit exists for. |
| 10 | **The limiter partitions on the credential, hashed**, not on the raw header. | The same key as Bearer, as a Basic password, or with stray whitespace was three or four buckets of 600 a minute, and the per-key tally would have been the first thing to show a tool past a ceiling the limiter believed it had set. Found by the bug check, fixed in the same PR. |
| 8 | **The console phrases name the key.** | Two keys live per tool so rotation costs no downtime — a count that does not say which key is half a number. |
| 9 | **Not in this build:** the player-connected rows, the key list's "requests rejected since" chips from [api-v2-community-tools.md §7](api-v2-community-tools.md), an admin usage strip, the Application Insights SDK or the Azure Monitor exporter. | Each is its own change; the SDK in particular would be a second telemetry pipeline for one dimension. |

## 3. The counters

Every row is an hourly roll-up in `scores.ToolActivity`: one row per tool, kind, hour **and
detail**, where the detail is the key's name. The upsert's identity used to stop at the hour, so
two keys used in the same hour would have folded into one row labelled with whichever came first.

- **`KeyUsed`** — added to when `GetToolByApiKeyQuery` resolves a live key, before the scheme
  writes its claims. The same resolve stamps `LastUsedAt`, so a maker sees both move together.
- **`KeyExpired`** — added to when the hash matches a key whose expiry has passed. Authentication
  still fails; the row is what turns "my key stopped working" into "your key expired on the 3rd and
  412 requests have bounced since".
- **`RateLimited`** — added to from the limiter's rejection hook. The hook hands the credential to
  `RecordRateLimitedRequestCommand`; the handler hashes it, finds the tool in the cache the resolve
  path filled, and counts. Revoked and unknown keys count nothing, on either path.

The console's API card sums the last few hundred rows, so the tile is recent activity rather than
a lifetime total. The rows themselves are never pruned: `IToolActivityRepository.Prune` has no
caller, and the all-time click count already depends on that.

## 4. The trace

One `Information` line per request under `/api/`, written by `ApiRequestLogMiddleware` after the
rest of the pipeline has answered. The message head is the literal `ApiRequest`; every field is a
named placeholder, which is what makes it a column in App Insights rather than a sentence.

| Field | Value |
|---|---|
| `Tier` | `tool`, `personal`, `session` or `anonymous`. The first two come from claims the API schemes write: the tool id, or the mark both schemes put on a principal a personal token brought. `session` is a signed-in browser reaching an API route on its cookie — the admin's own page, a Swagger try-out — which carries the same site claims as a token's principal and would otherwise have read as a person. |
| `ToolId` / `KeyName` | the tool tier only; empty otherwise |
| `UserId` | the personal tier only; empty otherwise, and never for a session |
| `Method` / `Route` | the verb and the endpoint's route template (`api/v2/players/{id}/scores`), never the concrete URL; `(unmatched)` when nothing routed |
| `Status` / `DurationMs` | the response as sent, and the time from the middleware to the last byte. A request the client abandoned mid-response logs `499`, the conventional client-closed-request, so a hang-up never reads as a crash; a genuine unhandled exception logs the `500` the exception handler is about to produce |

Absent fields are empty strings rather than nulls, so `isnotempty()` and `coalesce()` read the same
whatever the exporter would have made of a null.

The middleware sits between `UseRateLimiter` and `UseAuthorization`. Below authorization it would
never see a 401, because the authorization middleware answers those itself; above the limiter it
would see the 429s but not the principal. So the limiter's rejection hook writes the same line for
a 429, with whatever principal `RecordRateLimitedRequestCommand` handed back. Nothing on either
path logs a token or a key: the personal tier logs the user's id, the tool tier logs the key's
name.

Locally the same lines appear in the Aspire dashboard's structured-log view, which is how the
shape was checked before it ever reached App Insights.

## 5. Reading it

The `traces` table in the `piuscores` resource. Three queries answer the owner's question; each
sums `itemCount` rather than counting rows, because adaptive sampling may have kept one row for
several requests.

Calls per tool per day:

```kusto
traces
| where message startswith "ApiRequest"
| extend tool = tostring(customDimensions.ToolId), key = tostring(customDimensions.KeyName)
| where isnotempty(tool)
| summarize calls = sum(itemCount) by tool, key, bin(timestamp, 1d)
```

What one credential reads, by endpoint — this is the only place PIU Tracker is visible, on its
maker's personal token:

```kusto
traces
| where message startswith "ApiRequest"
| extend tier = tostring(customDimensions.Tier), who = coalesce(tostring(customDimensions.ToolId), tostring(customDimensions.UserId))
| extend route = strcat(tostring(customDimensions.Method), " ", tostring(customDimensions.Route))
| summarize calls = sum(itemCount) by tier, who, route
| order by calls desc
```

Who is hitting the limit, and how hard:

```kusto
traces
| where message startswith "ApiRequest" and toint(customDimensions.Status) == 429
| summarize hits = sum(itemCount) by tostring(customDimensions.ToolId), bin(timestamp, 1h)
```

Two rules for the Azure CLI, both learned the hard way: pass `--offset` explicitly (it defaults to
one hour and silently intersects any `ago()` in the query), and run it from a shell where the
query's double quotes survive (Bash with single-quoted KQL strings inside). Retention is the
resource's setting, 90 days unless raised; raising it is a portal change the owner makes.

## 6. What proves it

- Handler tests: a live key adds one to `KeyUsed` under the key's name; an expired key adds one to
  `KeyExpired` and still fails; a revoked or unknown key adds nothing; the rate-limit command
  counts a cached key and drops an unknown one.
- The v2 authentication tests read the key-name claim; no wire shape moves, so no golden moves.
- Against a real database: two keys in one hour make two rows, one key twice makes one row with a
  count of two, a null detail still folds — the directory-click path is unchanged.
- The middleware: one line with every field for a routed request, the anonymous tier when no claim
  is present, a session for a cookie principal, `499` for an abandoned request and `500` for a
  crash, and nothing at all for a request outside `/api/`.
- The limiter's partition: one bucket for one key however it was presented, and never the key
  itself.

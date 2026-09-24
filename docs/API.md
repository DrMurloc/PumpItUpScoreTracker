# API

High-level map of the HTTP surface. **Swagger is the source of truth for request/response shapes**: browse `/swagger` on the live site (or locally while running the app) — the OpenAPI document lives at `/swagger/v1/swagger.json`.

**Building a tool? Start with the Code section of your tool's console** (`/Developers/{tool}/code`),
not this page. That carries runnable snippets with your own key, URL and mixes already filled in —
which is the maker's
manual — what the data means, what trips people up, how sharing and webhooks work. This page is the
map.

## Two surfaces

| | `api/*` (v1) | `api/v2/*` |
|---|---|---|
| Status | **Frozen.** Still supported, still tested, no new endpoints | Where new work lands |
| Auth | Personal token (Basic) | Personal token (Basic) **or** tool key (Bearer) |
| Reads | Your own data | Your own data, or any player who shared with your tool |
| Writes | Yes | **One.** `POST api/v2/players/me/plays` — an observed play, personal token only. Every other mutation stays on v1 with a personal token |
| Errors | Plain text / status codes | `application/problem+json` (RFC 9457) |
| Paging | Page numbers | Opaque cursors — follow `next`, never construct it |
| `mix` | Optional, defaults to Phoenix | **Required**, all 30 mixes accepted |

v1 is not deprecated and has no removal date. If it does what you need, keep using it.

## Authentication

**Personal token** — HTTP Basic with the token as the password (username ignored):

```
Authorization: Basic base64("anything:<your-api-token>")
```

Issued per-user on the **Account page**. Every call runs in that user's context. This is the only
way to write.

**Tool key** — Bearer, for a registered community tool reading the players who shared with it:

```
Authorization: Bearer piu_scores_live_...
```

Issued on the **Developers page** and shown once. Read-only. Also accepted in the password position
of Basic auth, because that is where v1 taught everyone to put a credential. See
the console's Code section.

## `api/v2/*`

Cursor-paginated, `mix` required, RFC 9457 problem documents on failure. Catalog reads carry ETags —
send `If-None-Match` and expect `304`.

| Area | Route | What's there |
|---|---|---|
| Mixes | `api/v2/mixes` | Every mix, with its `scoringModel` (`phoenix` or `legacy`). Read this first — half the mixes score differently. **Additive 2026-09-22:** `Rise` and `RiseArcade` (Pump It Up RISE, the PC game, and its Arcade Station) list as `phoenix`-scored mixes; every v2 read takes them ([rise.md](design/rise.md)) |
| Songs | `api/v2/songs` | The song catalog for one mix. **Additive 2026-09-13:** every row carries `channel` — the song's channel on that mix (`Original`, `KPop`, `WorldMusic`, `JMusic`, `Xross`; null when unknown) — and the read takes `channel` (one or a comma list, any-of) ([song-channels.md](design/song-channels.md)) |
| Charts | `api/v2/charts` | Charts for one mix; `{id}`, `{id}/similar`, `random`. Carries `scoringLevel` — how hard it is to *score* on, null where unmeasured. **Additive 2026-09-05:** `{id}/scores` — every readable player's best on that chart, passes first then failed bests, each row the per-player score row with `userId`, `username` and `gameTag` in front. Share-gated exactly like `/players`: a tool sees the players who shared with it, a personal token its own user, and nobody else is on the page ([api-v2-round-2.md](design/api-v2-round-2.md)). **Additive 2026-09-12, reshaped 2026-09-13 before release:** every chart row carries two timelines — `addedInVersion` and `addedOn`, the patch of *this* mix the chart entered in and its date (a carry-over reads the mix's launch version), and `debutVersion` and `debutedOn`, its first appearance anywhere, a patch of `originalMix` — all null when unknown, plus `debut`, true when this mix is the chart's origin mix. `charts`, `charts/skills` and `charts/random` take `addedInVersion` (one or a comma list), `addedByVersion` (up to and including), `addedAfterVersion` (strictly after), `addedAfter` (a date, exclusive), `debutedInVersion` (what a patch introduced: `addedInVersion` plus `debut=true`) and `debut` (`true` or `false`); the filters AND together, a named filter never matches an unknown patch, and an unknown name is a `400` pointing at `versions` ([chart-versions.md](design/chart-versions.md)). **Additive 2026-09-13:** every chart row carries `channel` — the song's channel on this mix, null when unknown — and `charts`, `charts/skills` and `charts/random` take `channel` (one or a comma list, any-of; a name the mix does not offer is a `400` pointing at `channels`) ([song-channels.md](design/song-channels.md)) |
| Versions | `api/v2/versions` | **Additive 2026-09-12:** a mix's patches oldest first — `name`, `releaseDate` (null on an undated legacy patch), `sortOrder`, `chartCount` (charts that entered the mix in that patch) and `debutCount` (those that first appeared anywhere there). The value every `added*Version` and `debutedInVersion` parameter takes ([chart-versions.md](design/chart-versions.md)) |
| Channels | `api/v2/channels` | **Additive 2026-09-13:** the channels a mix offers, in the game's order — `name` (the token `channel` takes), `displayName`, `sortOrder`, `songCount`, `chartCount`. Phoenix lists five; Phoenix 2 lists four, having folded J-Music into World Music ([song-channels.md](design/song-channels.md)) |
| Tier lists | `api/v2/tier-lists/{list}` | `score-difficulty` · `pass-difficulty` · `pg-difficulty`. Phoenix and Phoenix 2 publish all three; earlier mixes publish `pass-difficulty` only, and the other two answer `404` |
| Chart skills | `api/v2/charts/{id}/skills`, `api/v2/charts/skills` | PIU Center's step analysis, one chart or filtered in bulk. **The analysis is mix-invariant** — `mix` on the bulk route filters which charts match, it does not change the answer |
| Official | `api/v2/official/*` | The piugame mirror: rankings, players, per-chart boards, popularity, what-it-takes, weekly highlights. Public data — no sharing needed, and no PIU Scores `userId` on these rows. **Additive 2026-09-05:** `rankings`, `players/{gameTag}`, `charts/{id}/board` and `weekly-highlights` take `supplemented=true` — the PIU Scores Supplemented switch from the site, folding public accounts' verified scores in below the official rows; every official player object carries `isSupplemented`, placements carry it too, and the profile carries `pumbilityIsSupplemented`. Default false, byte-identical to before ([api-v2-round-2.md](design/api-v2-round-2.md)) |
| Players | `api/v2/players` | Who shared with you; `{id}`, `{id}/scores`, `{id}/sessions`, `{id}/journal`. `me` works with a personal token. **Additive 2026-08-17:** `judgments` (on scores and journal entries) carries `maxCombo` — solved from the score and the five counts, `null` when unsolvable; journal entries carry `isStageBroken` — a play the stage interrupted, `isBest` false, `score` null ([stage-breaks-and-max-combo.md](design/stage-breaks-and-max-combo.md)). **Additive 2026-09-05:** `{id}/scores` takes `chartIds` — a comma-separated list of at most 50 chart ids, the point read for one player on one chart; the list takes `community=<name>` (World, a country, or a community's own name), which only ever narrows the players you can already read and filters a private community only for its members; `{id}/stats` and the bulk `stats` carry a player's PUMBILITY numbers per mix — the merged, singles, doubles and co-op pools, competitive levels, highest level, clear count and the estimated place on piugame's official ranking — highest PUMBILITY first in the bulk, 404 on a legacy mix ([api-v2-round-2.md](design/api-v2-round-2.md)) |
| Weekly charts | `api/v2/weekly-charts` | The current board and scores on it |
| Plays (write) | `POST api/v2/players/me/plays` | **New 2026-09-22, the one write on v2:** a play the caller observed — mix, chart (`chartId`, or `songName` + `chartType` + `level`), the five judgment counts, `maxCombo`, `score`, `isBroken`, `playedAt`, a `source` naming the capturing tool and an optional `recordBrokenAsBest` (the mix's default when omitted). The server recomputes the score from the judgments and answers `400` when they do not reconcile (the judgment checksum), derives the award from them and refuses a claimed award they do not earn; a play that reconciles lands in the journal and, when it beats the record, becomes the personal best. Personal token only; a mix must be `phoenix`-scored. Built for the RISE capture app, open to any tool ([rise.md §6.3](design/rise.md)). **Additive 2026-09-23:** the five counts and `maxCombo` are optional and travel together — all six or none, `400` `judgments-incomplete` otherwise. A play without them is recorded as read: no checksum, and an optional `award` is taken as sent provided the score agrees with it (only `1000000` is a Perfect Game); a million with no `award` records a Perfect Game, and a pass without judgments must score above zero. A play with them is checked exactly as before ([rise.md D24](design/rise.md)) |

## The frozen surface — `api/*`

The original contract. Exact JSON wire shapes are pinned by approval tests
(`ScoreTracker.Tests.Api`) — a breaking change here is breaking-change review, not a casual edit.

| Area | Route | What's there |
|---|---|---|
| Charts | `api/charts` | Paginated chart listing by mix/level/type; `api/charts/random` for weighted random draws |
| Phoenix scores | `api/phoenixScores` | GET your recorded scores (paginated; sortable via `SortBy` = RecordedDate/Score/LetterGrade/Plate/Level/Pumbility/PumbilityPlus + `SortDir`; filterable via `MinLevel`/`MaxLevel`/`ChartType`/`MinLetterGrade`/`MinPlate`/`IsBroken`; each record carries its Pumbility and PUMBILITY+ worth — the Pumbility value uses the requested mix's formula, so the same score reads differently on `mix=Phoenix` vs `mix=Phoenix2`); POST a single best attempt — authoritative by default, so it **overwrites** the record and may lower it; pass `?KeepBestStats=true` to apply the best-attempt policy instead and only ever raise it ([score-truth-model.md](design/score-truth-model.md)); POST `import` to trigger an official-site import with your game account credentials |
| Tier lists | `api/tierlist` | Four rankings per level+chart type: `scores`, `officialscores`, `passcount`, `popularity` |
| Weekly charts | `api/weeklyCharts` | The current weekly challenge board and player scores on it. **Breaking change 2026-07-30:** a score's `Plate` is now `null` when `IsBroken` is true — the game awards no plate for a failed stage, and the field previously carried a fabricated one ([score-truth-model.md](design/score-truth-model.md) D8) |
| Tournaments | `api/tournaments` | Tournament list |

One field on the `api/phoenixScores` POST is accepted and ignored: `syncScoreTracker`. Sending a
player's session to PIU Tracker is a share they hold on the Community Tools page now, not a
per-request flag. The field stays so an existing caller still gets a `200`, and the delivery it was
asking for happens anyway if that player granted it.

### The `Mix` parameter (Phoenix 2)

Mix-aware endpoints take an **optional `Mix` parameter** — a query parameter on GETs (`?Mix=Phoenix2`), a body field on the `api/phoenixScores` score POST:

- **The default is `Phoenix`, permanently.** Omitting `Mix` never follows the player's on-site mix selection, so integrations that predate Phoenix 2 keep receiving byte-identical responses.
- Accepted values (case-insensitive): `Phoenix` and `Phoenix2` — anything else, including `XX`, is a `400` listing the valid options. One grandfathered exception: `api/charts` GET predates the parameter and still accepts `XX` for legacy catalog reads (and previously *required* `Mix`; omitting it now defaults to Phoenix).
- Applies to: `api/phoenixScores` GET + score POST (**not** POST `import` — the importer is Phoenix-only for now), `api/charts` GET + `random`, all four `api/tierlist/*` rankings, and both `api/weeklyCharts` GETs (each mix runs its own weekly board).
- Tier lists return the **raw list for the requested mix**: unlike the site UI, the API never substitutes Phoenix data for an empty Phoenix 2 tier list, so expect `[]` until Phoenix 2 data accumulates rather than a response that silently changes meaning later.
- On v2, three tier lists are published and each is named for the question it answers. `/TierLists` shows more than these — the extras are blend inputs and mirror-derived rankings, not difficulty judgements, and a `popularity` sort is not a difficulty sort. **`404` and `[]` mean different things**: `404` is "this mix never had that scoring model", `[]` is "nobody has voted yet".
- `api/tournaments` takes no `Mix` parameter — tournament sessions carry their own mix.

## NOT the partner surface

- **UI-supporting controllers** — `login/*` (OAuth challenge/callback + the dev-only backdoor), `logout/*`, `culture/Set` (locale cookie), `sitemap.xml`, `api/admin/*` (admin diagnostics), and the step-chart JSON pair (`/Charts/StepChart/{chartId}` payload + `/Charts/StepChart/{chartId}/Breaks` pins — [step-chart-failure-map.md](design/step-chart-failure-map.md) D13). These serve the Blazor app, not API callers.

## Conventions

- Controllers are thin: every action dispatches a MediatR query/command — no business logic lives in the controller layer.
- Every v2 action declares its response shapes, so `/swagger` shows the JSON schema of a 200 and the problem document of each 400/404 rather than a bare status. A convention test (`ScoreTracker.Tests.Api`) fails when a new v2 action lacks a declared 200 shape.
- CORS: partner endpoints allow cross-origin calls via the `API` policy.
- Rate limits on v2: 600 requests a minute, for a tool key and a personal token alike, counted per credential. A `429` carries `Retry-After` — wait it out rather than retrying straight away. A full catalog pull (every chart, song and tier list across all 31 mixes) is roughly 500 requests, so it fits inside one window.
- Building a PIU tool? You don't need to build your own importer — register the tool and let the webhooks push to you. See the Code section of your tool's console, then `#tool-makers` on [Discord](https://discord.gg/AvS5PxnvSN).
- **Webhook deliveries are at-least-once.** If PIU Scores restarts partway through an import, that import is replayed once the app comes back, and its delivery is sent a second time ([import-restart-recovery.md](design/import-restart-recovery.md)). Key your handler on the chart plus the score's recorded time rather than assuming each delivery is unique. PIUGame **session** deliveries are the exception — they are never re-sent, because the session is gone by the time the replay runs.

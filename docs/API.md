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
| Writes | Yes | **None.** Every mutation stays on v1 with a personal token |
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
| Mixes | `api/v2/mixes` | Every mix, with its `scoringModel` (`phoenix` or `legacy`). Read this first — half the mixes score differently |
| Songs | `api/v2/songs` | The song catalog for one mix |
| Charts | `api/v2/charts` | Charts for one mix; `{id}`, `{id}/similar`, `random`. Carries `scoringLevel` — how hard it is to *score* on, null where unmeasured |
| Tier lists | `api/v2/tier-lists/{list}` | `score-difficulty` · `pass-difficulty` · `pg-difficulty`. Phoenix and Phoenix 2 publish all three; earlier mixes publish `pass-difficulty` only, and the other two answer `404` |
| Chart skills | `api/v2/charts/{id}/skills`, `api/v2/charts/skills` | PIU Center's step analysis, one chart or filtered in bulk. **The analysis is mix-invariant** — `mix` on the bulk route filters which charts match, it does not change the answer |
| Official | `api/v2/official/*` | The piugame mirror: rankings, players, per-chart boards, popularity, what-it-takes, weekly highlights. Public data — no sharing needed, and no PIU Scores `userId` on these rows. **Additive 2026-09-05:** `rankings`, `players/{gameTag}`, `charts/{id}/board` and `weekly-highlights` take `supplemented=true` — the PIU Scores Supplemented switch from the site, folding public accounts' verified scores in below the official rows; every official player object carries `isSupplemented`, placements carry it too, and the profile carries `pumbilityIsSupplemented`. Default false, byte-identical to before ([api-v2-round-2.md](design/api-v2-round-2.md)) |
| Players | `api/v2/players` | Who shared with you; `{id}`, `{id}/scores`, `{id}/sessions`, `{id}/journal`. `me` works with a personal token. **Additive 2026-08-17:** `judgments` (on scores and journal entries) carries `maxCombo` — solved from the score and the five counts, `null` when unsolvable; journal entries carry `isStageBroken` — a play the stage interrupted, `isBest` false, `score` null ([stage-breaks-and-max-combo.md](design/stage-breaks-and-max-combo.md)). **Additive 2026-09-05:** `{id}/scores` takes `chartIds` — a comma-separated list of at most 50 chart ids, the point read for one player on one chart ([api-v2-round-2.md](design/api-v2-round-2.md)) |
| Weekly charts | `api/v2/weekly-charts` | The current board and scores on it |

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
- CORS: partner endpoints allow cross-origin calls via the `API` policy.
- Rate limits on v2: 600 requests a minute, for a tool key and a personal token alike, counted per credential. A `429` carries `Retry-After` — wait it out rather than retrying straight away. A full catalog pull (every chart, song and tier list across all 31 mixes) is roughly 500 requests, so it fits inside one window.
- Building a PIU tool? You don't need to build your own importer — register the tool and let the webhooks push to you. See the Code section of your tool's console, then `#tool-makers` on [Discord](https://discord.gg/AvS5PxnvSN).
- **Webhook deliveries are at-least-once.** If PIU Scores restarts partway through an import, that import is replayed once the app comes back, and its delivery is sent a second time ([import-restart-recovery.md](design/import-restart-recovery.md)). Key your handler on the chart plus the score's recorded time rather than assuming each delivery is unique. PIUGame **session** deliveries are the exception — they are never re-sent, because the session is gone by the time the replay runs.

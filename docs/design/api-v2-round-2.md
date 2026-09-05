# API v2 round 2 — the supplemented reading, scores by chart, community filter, player stats

Status: **building** (2026-09-05). Three partner requests relayed by the owner on 2026-09-05, scoped
and decided the same day. This document is the record of what was approved; the commits follow it
and do not extend it. Anything not written here is not in scope
([api-v2-community-tools.md §16](api-v2-community-tools.md) is the precedent for that rule, and
the scope-creep audit of 2026-08-02 is the reason for it).

The three requests, in the toolmaker's terms:

1. Looking up a game tag that piugame's boards do not list but PIU Scores knows should answer with
   that player's supplemented standing, not an empty profile.
2. Scores by chart — one player's score on one chart, and every player's score on one chart.
3. Filtering players, and a player's PUMBILITY, which no `/players` read carries today.

## 1. Decisions (owner, 2026-09-05)

| # | Decision | Owner's words |
|---|---|---|
| 1 | **Supplemented is an opt-in query flag, default official.** No automatic fallback. | *"leaning to a ?UsePIUScoresSupplemented query string (or something like that), so default is true official. would match UX of the actual boards, then you can just always use the supplemented views"* |
| 2 | **Both halves of request 2**: a chart filter on the per-player scores read, and a new cross-player read on a chart. | *"both 2a and 2b"* |
| 3 | **The cross-player row names the player**: user id, username **and** game tag. | *"do username and gametag for cross player row"* |
| 4 | **No country filter. A community filter**, which covers countries because every country is a community. | *"lets not do country filters. lets do community filters, which will include country."* |
| 5 | **A private community is filterable only by its members.** The tool's maker for a tool key, the caller for a personal token. | *"filterable only when you're a member, yes"* |
| 6 | **Nothing else.** No console snippet, no wide public country ranking, no club-membership read beyond the filter. | *"no on extra stuff"* |
| 7 | **Swagger declares every v2 response type**, on all existing actions as well as the new ones. | *"we should tell swagger the response types please. just go ahead and do all of them"* |

Decision 1 supersedes decision 13 of [supplemented-leaderboards.md](supplemented-leaderboards.md)
("`api/v2/official/*` stays official-only for now"). The privacy reasoning there was about the site
user id, which stays stripped from every official read exactly as before. What the supplemented
reading does reveal — that a tag belongs to a public PIU Scores account — the site's own switch
already shows to anonymous visitors, and private accounts never enter the supplemented cohort.

## 2. The surface

| Route | Change |
|---|---|
| `GET api/v2/official/rankings` | `supplemented` (bool, default false) |
| `GET api/v2/official/players/{gameTag}` | `supplemented`; profile gains `pumbilityIsSupplemented`; placements gain `isSupplemented` |
| `GET api/v2/official/charts/{chartId}/board` | `supplemented` |
| `GET api/v2/official/weekly-highlights` | `supplemented` |
| every official player object | gains `isSupplemented` (always false in the official reading) |
| `GET api/v2/players/{playerId}/scores` | `chartIds` — comma-separated chart ids, at most 50 |
| `GET api/v2/charts/{chartId}/scores` | **new** — every readable player's best on one chart |
| `GET api/v2/players` | `community` — a community name |
| `GET api/v2/players/{playerId}/stats` | **new** — one player's PUMBILITY numbers in one mix |
| `GET api/v2/players/stats` | **new** — the same for every readable player, filtered like `/players` |

`popularity` and `what-it-takes` take no flag, because the site's switch does not reach them either:
popularity is ranked on full play data and What It Takes is a fact about the real board.

`mix` stays required on every mix-scoped read. Errors stay RFC 9457 problem documents. Cursors stay
opaque.

## 3. Shapes

Pinned by the goldens in `ScoreTracker.Tests.Api`; the examples below are what the goldens say.

### 3.1 The official player, everywhere it appears

```json
{ "playerId": 88213, "gameTag": "MURLOC#1", "avatarUrl": "https://…/avatar.png", "isSupplemented": false }
```

`isSupplemented` is a property of the **reading**, not the person: true when the player is on this
board only because PIU Scores knows their scores, false in the official reading always.

### 3.2 The profile, `official/players/{gameTag}?supplemented=true`

```json
{
  "player": { "playerId": 88213, "gameTag": "MURLOC#1", "avatarUrl": "…", "isSupplemented": true },
  "playerType": null,
  "pumbility": 1432.50,
  "pumbilityIsSupplemented": true,
  "pumbilityRank": 1432,
  "pumbilityRankDelta": null,
  "boardsInTop": 512,
  "numberOnes": 0,
  "bestPlace": 301,
  "topTens": 0,
  "history": [ … ],
  "placements": [
    { "chartId": "…", "place": 301, "placeDelta": null, "score": 987654, "computedRating": 1234.56, "isSupplemented": true }
  ]
}
```

`pumbilityIsSupplemented` is true when the PUMBILITY value came from PIU Scores' supplemented
ranking row — the site's computed number — rather than from piugame's ranking. It is false when
`pumbility` is null, which is why it is not spelled `pumbilityIsOfficial`: a null number is nobody's.
An off-board player's `pumbilityRank` only means something against `rankings?supplemented=true`,
and their placements only against `charts/{id}/board?supplemented=true`; that is why the flag is
offered on all four reads rather than on the lookup alone.

### 3.3 Scores on a chart, `charts/{chartId}/scores?mix=`

```json
{
  "mix": "Phoenix",
  "scoringModel": "phoenix",
  "data": [
    {
      "userId": "33333333-3333-3333-3333-333333333333",
      "username": "VisiblePlayer",
      "gameTag": "VISIBL",
      "chartId": "11111111-1111-1111-1111-111111111111",
      "recordedAt": "2026-01-15T00:00:00+00:00",
      "source": "officialImport",
      "score": 987654,
      "letterGrade": "SSS",
      "plate": "Superb Game",
      "isBroken": false,
      "pumbility": 1234.56,
      "judgments": null
    }
  ],
  "limit": 100,
  "total": 1,
  "next": null
}
```

The row is the per-player score row with the player's identity in front of it, byte-for-byte the
same fields after `gameTag`. Passes come first, highest score first; failed bests follow, highest
score first; ties break on user id so a page never reshuffles. `total` is present because the
rows are already in memory. On a legacy mix `scoringModel` is `legacy` and `pumbility` is null,
exactly as on the per-player read.

### 3.4 Player stats, `players/{playerId}/stats?mix=` and `players/stats?mix=`

```json
{
  "userId": "33333333-3333-3333-3333-333333333333",
  "username": "VisiblePlayer",
  "gameTag": "VISIBL",
  "pumbility": 17173.29,
  "singlesPumbility": 9000.12,
  "doublesPumbility": 8173.17,
  "coOpPumbility": 1234.50,
  "competitiveLevel": 21.34,
  "singlesCompetitiveLevel": 21.10,
  "doublesCompetitiveLevel": 20.90,
  "highestLevel": 24,
  "clearCount": 1532,
  "estimatedPumbilityRank": 812,
  "estimatedSinglesPumbilityRank": null,
  "estimatedDoublesPumbilityRank": null,
  "estimatedRankAsOf": "2026-08-30T00:00:00+00:00"
}
```

These are the numbers the PUMBILITY page shows. `pumbility` is the merged pool, the singles and
doubles pools are the per-type top fifties, `coOpPumbility` the co-op pool. The three estimated
ranks are where the site places the player on piugame's official PUMBILITY ranking, downloaded
weekly — null when the site has no estimate — and `estimatedRankAsOf` is the download that estimate
was made against. Pools and levels print with two decimals, the same presentation rounding the
per-score `pumbility` already uses; nothing below the DTO rounds.

The bulk read is the same object per row inside the standard collection envelope, **highest
PUMBILITY first**, and carries `total`. The lifetime rating and the per-pool average score and
level columns stay out: they are inputs, not the numbers a maker asked for.

## 4. Rules

**Who a read reaches is unchanged.** Every `/players` read and the new chart read resolve the same
readable set: a tool key reaches the players who shared with it ([api-v2-community-tools.md §5](api-v2-community-tools.md)),
a personal token reaches its own user. A player outside the set is simply absent from a list and a
404 on a direct read, never a 403.

**The community filter only ever narrows.** `community` intersects the readable set with the
community's current members (banned members are not members). It cannot widen a tool's reach to a
player who did not share. World and every country name work because they are communities.

**A private community is invisible unless the viewer is a member.** The viewer is the tool's maker
for a tool key, the caller for a personal token. An unknown community and a private one the viewer
may not see answer the same 404, so names cannot be probed. Public and public-with-code
communities are filterable by anyone, which is what the site does with their rosters. An empty or
malformed name is a 400.

**Supplemented means what the switch means.** `supplemented=true` is the PIU Scores Supplemented
switch from the Official Leaderboards section: public PIU Scores accounts' verified scores folded
into the boards, official rows never displaced, official places renumbered, and no site user id
anywhere in the response. A maker who reads a supplemented rank should read the supplemented
ranking to compare it against.

**Legacy mixes have no PUMBILITY.** `stats` on one is a 404 in the same voice as a tier list that a
mix never published. A shared player with no record in the mix is a 404 on the single read and
absent from the bulk.

## 5. Technical scope by layer

**Web** (`ScoreTracker/`) — the bulk of the change. `OfficialController` threads `supplemented`
into the four queries that already accept it; `PlayersController` gains `chartIds`, `community`,
and the two stats actions; a new `ChartScoresController` under the `/charts` prefix holds the
cross-player read, separate from the catalog controller because that one is documented as
share-free and this read is share-gated. New DTOs: `ChartScoreDto`, `ChartScorePageDto`,
`PlayerStatsDto`; three flags on the official DTOs. One shared helper resolves usernames and game
tags for a page of user ids in two round trips. Every v2 action declares its response types and a
convention test keeps it that way.

**OfficialMirror** — `OfficialPlayerProfileRecord` gains `PumbilityIsSupplemented`; the rankings
handler threads each row's supplemented flag into its player (it was dropped, so the site's own
Rankings rail marker could never render — the fix rides here); a bulk
`GetLinkedOfficialPlayerTagsQuery` with a `GetPlayersByUserIds` repository read.

**ScoreLedger** — `GetChartRecordsForPlayersQuery`: every named player's best on one chart, the
indexed per-chart read filtered to the caller's ids in memory, so a several-hundred-GUID list never
reaches SQL.

**Identity** — `GetUsersByIdsQuery` over the existing `IUserRepository.GetUsers`.

**Communities** — `GetCommunityMembersForViewerQuery`, which is where the private-community rule
lives, so the controller composes reads and never re-implements a Communities rule.

**CommunityTools** — `GetToolOwnerQuery`, ungated: the existing tool query resolves the caller
through the signed-in user, and a tool key has none.

**Not touched:** the schema (no migration), CompositionRoot, SharedKernel, every shared Domain
port, the v1 surface and its goldens, the webhook contract, the console's Code section, any UI
string.

## 6. Verification

| Suite | Coverage |
|---|---|
| `Tests` / `ApplicationTests` | Rankings rows carry the flag; the profile carries the PUMBILITY-row flag; members-for-viewer across public, public-with-code, private member, private non-member, unknown; tool owner; users by ids; linked tags by ids; chart records filtered to the given players. |
| `Tests.Api` | Goldens for every new or changed shape; the default reading is official; `chartIds` filters and rejects a bad id; the chart read for a tool, a personal token and an unshared player; the community filter narrows, hides a private community from a non-member, and answers unknown and hidden identically; stats on a legacy mix and on a player without a record; `players/stats` is reachable beside `players/{playerId}`; every v2 action declares a 200 response type. |
| `Tests.Integration` | The per-chart ledger read against real SQL; the mirror's by-user-ids read. |
| Acceptance, by hand against the prod-synced local database | Each new or changed read called with a personal token and a tool key; `/swagger` opened and every v2 operation showing a response schema. |

## 7. Commit plan

S0 this document · S1 `fix(mirror)` ranking rows carry the mark · S2 `feat(api)` the
supplemented reading · S3 `feat(api)` `chartIds` · S4 `feat(identity)` users by ids ·
S5 `feat(mirror)` linked tags by ids · S6 `feat(ledger)` chart records for players ·
S7 `feat(api)` scores on a chart · S8 `feat(communities)` members for a viewer ·
S9 `feat(tools)` tool owner · S10 `feat(api)` community filter · S11 `feat(api)` player stats ·
S12 `feat(api)` declared response types + the ratchet · S13 `docs(api)` DTO property summaries ·
S14 `docs(api)` API.md and this document flipped to built.

## 8. Not built, on purpose

- **A public country or community ranking with no share** — the site shows one, the API does not.
  It would be the first player data reachable without a share and would list public players who
  turned all-tools sharing off. Owner's call, not taken this round.
- **Automatic supplemented fallback** on the tag lookup — offered, declined in favor of the flag.
- **Per-row supplemented marks on weekly highlights.** The highlights handler never carried them;
  the reading is what the flag says. Additive later if a maker asks.
- **A `/countries` read.** `country` still rides every player row; the filter vocabulary is the
  community name.

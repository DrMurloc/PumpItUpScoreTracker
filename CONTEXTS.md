# Domain Contexts

> Status: **working map** · Last updated 2026-06-11
>
> This is the conceptual bounded-context map from the 2026-06 domain analysis. It feeds the [rearchitecture](BACKLOG.md#onion--ddd--hexagonal-rearchitecture) — the friction inventory below seeds Step 1.6's diagnostic catalog, and the open questions go to the Step 1.7 ADR. **It is direction, not locked design**: boundaries here will be refined as rearch sessions touch real code. [ARCHITECTURE.md](ARCHITECTURE.md) describes the code as it *is*; this file describes the domain as we currently *understand* it. For the audience and strategy behind the core/supporting calls, see [PRODUCT.md](PRODUCT.md).

## How to read this

A context here is a conceptual ownership boundary — one language, one set of rules, one owner for its data. It does **not** have to be one assembly; BACKLOG's A1 vertical taxonomy is the physical realization, and the mapping is given per context. Several contexts may share an assembly, and the shared kernel isn't a context at all.

## Shared kernel — PIU Game Model

Facts about the game itself: `PhoenixScore`, `XXScore`, `DifficultyLevel`, `ChartType`, `PhoenixLetterGrade`, `PhoenixPlate`, `Mix`, `Judgment`, grade/plate math, lifebar simulation, and chart/song identity. Stable and safe to share precisely because it's externally defined — it changes when Andamiro ships a mix, not when we refactor. The calculators under `Pages/Tools/` are UI over this kernel and need no context.

BACKLOG A2 defines a deliberately *tighter* kernel (no scoring engine). Whether `ScoringConfiguration` joins the kernel is an open ADR question (Q3) — today it's used by tournament scoring, Pumbility, and world rankings alike.

## Context overview

| # | Context | Class | Primary audience | A1 vertical (physical) |
|---|---|---|---|---|
| 1 | Score Ledger | **Core** | All | `ScoreLedger` |
| 2 | Player Progression | **Core** | Casual-competitive | `PlayerProgress` (+ `Recommendations`, Q1) |
| 3 | Chart Intelligence | **Core** | Casual-competitive | `ChartIntelligence` |
| 4 | Game Content Catalog | Supporting | All | shared kernel + shared ports today (Q2) |
| 5 | Weekly Challenge | Supporting | Casual-competitive | `WeeklyChallenge` |
| 6 | Event Competition | Supporting | Competitive | `EventCompetition` |
| 7 | Community & Notification | Supporting | Casual | `Community` |
| 8 | Official Game Mirror | Supporting (ACL) | cross-cutting | `OfficialMirror` |
| 9 | Identity & Access | Generic | All | kernel `User` (identity) + Web auth |

UCS keeps its own small `UCS` vertical per A1 (chart metadata + leaderboard + tags); conceptually it spans Catalog and competition, but it's small and cohesive enough that splitting it isn't worth the ceremony today.

### 1. Score Ledger — core

- **Purpose.** System of record for personal play results, plus the acquisition pipeline that fills it. The strategic data asset everything downstream derives from.
- **Owns.** Phoenix best-attempts, legacy XX attempts, recorded dates; manual entry, official-account import, OCR and CSV upload (with song-name correction); the score-batch accumulation policy; score wipe/restore.
- **Today in code.** `UpdatePhoenixRecordHandler`, `EFPhoenixRecordsRepository`, `EFXXChartAttemptRepository`, `PhoenixScoreFileExtractor`, the upload pages, `api/phoenixScores`.
- **Contract surface.** `PlayerScoreUpdatedEvent` and `RecentScoreImportedEvent` are the de facto integration events of the whole system, and (via webhooks) of partner tools — treat them as published language. Plus `api/phoenixScores`.

### 2. Player Progression — core

- **Purpose.** Everything derived about a *player*: ratings, history, achievements, and the improvement loop.
- **Owns.** PlayerStats (Pumbility, singles/doubles/skill ratings, competitive level), rating history, title/paragon evaluation, score-quality percentiles, Pumbility gain projections, chart recommendations.
- **Today in code.** `PlayerRatingSaga` (PersonalProgress), `TitleSaga`, `PlayerHistorySaga`, `ScoreQualitySaga`, `PumbilityProjectionSaga`, `RecommendedChartsSaga`; the Progress / CompetitiveLevel / Titles / WhatShouldIPlay pages.
- **Contract surface.** `PlayerRatingsImprovedEvent`, `NewTitlesAcquiredEvent`, `PlayerStatsUpdatedEvent`. No public API today — deliberate open question (PRODUCT.md §Platform stance).

### 3. Chart Intelligence — core

- **Purpose.** Everything derived about a *chart* from crowd data — the community's difficulty knowledge.
- **Owns.** Tier lists of all categories, scoring-difficulty recalculation, letter-grade difficulty percentiles, difficulty/co-op/preference votes, popularity aggregates, chart tags.
- **Today in code.** `TierListSaga`, `ScoringDifficultySaga`, `RateChartDifficultyHandler`, `RateCoOpDifficultyHandler`, `PreferenceRatingHandler`, BulkVote admin page, TierLists pages, `api/tierlist`.
- **Contract surface.** `api/tierlist`. Note: this context consumes *two* feeds — community scores (Ledger) and official scores (Mirror). The official feed already populates an "Official Scores" tier list, which doubles as the cold-start story for a new mix before community data accumulates.

### 4. Game Content Catalog — supporting

- **Purpose.** The reference data everyone speaks: songs, charts, mixes, official levels, note counts, step artists, videos, name localization; the weighted random-selection engine over the catalog.
- **Today in code.** `Chart`/`Song` models, `EFChartRepository`, `SkillsSaga` (Q5), `RandomizerSaga` (Q4), `ChartUpdate` admin page, `api/charts/random`.
- **Contract surface.** ChartId/SongId are the published language of the entire system.
- **Physical note (Q2).** A1/A2 realize the catalog as shared-kernel types plus shared ports rather than a vertical; this map records it as a conceptual context either way.

### 5. Weekly Challenge — supporting, core-audience

- **Purpose.** The automated recurring competition: weekly chart rotation, the live board, placement history. No organizer, no registration — entries arrive from score imports; it runs on a clock, not a human. Split from Event Competition because it serves a different audience (casual-competitive vs competitive).
- **Today in code.** `WeeklyTournamentSaga`, WeeklyCharts page, `api/weeklyCharts`.
- **Contract surface.** `UserWeeklyChartsProgressedEvent`, `api/weeklyCharts`.

### 6. Event Competition — supporting

- **Purpose.** Human-organized competition. Post-Phoenix 2 this slims to **M.o.M. + Qualifiers** — the Match/bracket subsystem and the rest of the generic tournament domain are dropped per the [Phoenix 2 carve-out](PHOENIX2-ROADMAP.md).
- **Owns (target).** M.o.M. cycling and stamina sessions (`TournamentSession` — the codebase's best aggregate: approval workflow, photo verification), qualifier configs and submissions, the qualifier auto-enrollment policy (a clean process manager subscribing to Ledger events), per-event scoring presets.
- **Today in code.** `TournamentHandler`, `QualifiersSaga`, `MarchOfMurlocsHandler`, `AutoBuildSessionHandler`; `MatchSaga` and the bracket pages exist today but are slated for removal.

### 7. Community & Notification — supporting

- **Purpose.** User groups (membership, invites, channels, privacy, regional/world auto-membership), community leaderboard views, and the achievement-broadcast policy to Discord. Almost purely a *downstream subscriber* — it owns routing policy, not the facts it routes. That's its correct nature; lean into it.
- **Today in code.** `CommunitySaga` (consumes six event types), `EFCommunitiesRepository`, Communities pages, the bot's notification side.

### 8. Official Game Mirror — supporting + anticorruption layer

- **Purpose.** Everything scraped from piugame.com and derived from scraped data: official leaderboard snapshots, world rankings (computed over *all* players, including non-users — which is why this isn't Player Progression), avatars, account-data import handed to the Ledger, popularity feeds handed to Chart Intelligence, PiuTracker sync. HTML structure, session cookies, and song-name mapping quirks stay inside; everyone else receives clean facts. Game credentials never cross this boundary outward.
- **Today in code.** `OfficialLeaderboardSaga`, `WorldRankingService`, `OfficialSiteClient`/`PiuGameApi`/`PiuTrackerClient`, OfficialLeaderboards pages.
- **Usage note.** Its UI draws <1% of traffic — invest in contracts and scraper resilience, not pages.

### 9. Identity & Access — generic

- **Purpose.** Accounts, OAuth logins, API tokens, admin status, content locks, UI settings, profile privacy.
- **Today in code.** `CreateUserHandler`, `UpdateUserHandler`, `ApiTokenHandler`, `EFUserRepository`, Account/Login pages, `UserAccessService`.
- **Note.** Today's `User` record mixes auth identity with player-facing profile (game tag, country, visibility) — see F6.

## Context map

```
 piugame.com ══ ACL ══> ┌──────────────┐  charts   ┌─────────────────┐
 piutracker.app ══════> │ OFFICIAL     │ ────────> │ GAME CONTENT    │
                        │ MIRROR       │ popularity│ CATALOG         │──ChartId/SongId──> (everyone)
                        └───┬──────────┘     │     └─────────────────┘
        "scores observed"   │                ▼
                            ▼          ┌──────────────────┐
   ┌────────────────┐  score facts     │ CHART            │
   │  SCORE LEDGER  │ ───────────────> │ INTELLIGENCE     │
   │  (CORE)        │                  └──────────────────┘
   └──┬─────┬───┬───┘                        ▲ skill weights
      │     │   └── events ──> ┌──────────────────┐
      │     │                  │ PLAYER           │── ratings/titles ──┐
      │     │                  │ PROGRESSION      │                    ▼
      │     │                  └──────────────────┘   ┌───────────────────────────┐
      │     └─ RecentScoreImported ─> EVENT           │ COMMUNITY & NOTIFICATION  │
      │                               COMPETITION     │ (downstream subscriber →  │
      ▼                                               │  Discord fan-out)         │
   WEEKLY CHALLENGE ── placement events ────────────> └───────────────────────────┘

   PIU GAME MODEL (shared kernel) — under everything
   IDENTITY & ACCESS (generic) — account facts to all
```

| Relationship | Style |
|---|---|
| Catalog → everyone | Published language (ChartId, chart facts) |
| Mirror → Ledger, Chart Intelligence, Weekly | Facts via events; Mirror is the ACL for piugame.com |
| Ledger → Progression, Chart Intelligence, Weekly, Competition, Community | Customer–supplier via published events; also the partner-webhook source |
| Progression → Community, Chart Intelligence | Rating/title events; skill-weight feed |
| Community ← everything | Downstream subscriber (owns routing, not facts) |
| Game Model | Shared kernel — stable because externally defined |

## Friction inventory — where today's code crosses these boundaries

Feeds rearch Step 1.6. Factual observations about current code, not blame.

- **F1 — The score table is a shared database.** `IPhoenixRecordRepository` is read directly by `TierListSaga`, `ScoringDifficultySaga`, `CommunitySaga`, `ScoreQualitySaga`, `PumbilityProjectionSaga`, `RecommendedChartsSaga`, `TitleSaga`, `RandomizerSaga`, `AutoBuildSessionHandler`, and `OfficialLeaderboardSaga` — six of nine contexts querying one context's tables. The central rearch decision is how each consumer gets score facts instead (fat events, a read API, or context-local projections — different consumers warrant different answers).
- **F2 — Events are thin, so consumers reach back.** `PlayerScoreUpdatedEvent` carries only IDs; `CommunitySaga` re-queries scores, charts, and users to compose a Discord message. Carrying events also matter externally: their payloads are the webhook bodies partner tools receive. Fatten them while the transport is still in-memory and the contracts private.
- **F3 — The gateway knows about the weekly tournament.** [OfficialSiteClient.cs:230](ScoreTracker/ScoreTracker.Data/Clients/OfficialSiteClient.cs) sends `RegisterWeeklyChartScore` directly into the weekly feature. The Mirror should publish "official score observed" facts; Weekly subscribes. (Also the worst instance of the `Data → Application` divergence.)
- **F4 — Chart Intelligence writes into the Catalog's aggregate.** `ScoringDifficultySaga` stores computed scoring levels on [Chart](ScoreTracker/ScoreTracker.Domain/Models/Chart.cs) (`ScoringLevel`). Split: Catalog owns the official level; Intelligence owns effective scoring level as its own projection.
- **F5 — PlayerStats is everyone's input.** Community leaderboards, weekly bucketing, tier-list weighting, score-quality cohorts, and recommendations all read Progression's `PlayerStats` directly (`EFCommunitiesRepository` joins Community × PlayerStats × User). Progression needs a deliberate published read model — its biggest consumer is ~10% of site traffic (community leaderboards).
- **F6 — `User` is two concepts.** Auth/token/lock data (Identity) and player-facing profile (game tag, country, visibility) in one record; `User.IsAdmin` is a hardcoded GUID. Split Account from PlayerProfile.
- **F7 — `EFTierListRepository` spans three contexts' data** (tier entries + `UserHighestTitle` + `PhoenixBestAttempt`).
- **F8 — `CommunitySaga` doubles as membership manager and notification router.** Separable once contexts exist: Community owns the channel registry; routing is a policy over published events.

**Already aligned** (keep, and copy the pattern): the Ledger → Progression → Community event backbone; M.o.M. *snapshotting* scoring levels instead of referencing them live (correct inter-context copying); the qualifier auto-enroll process manager; `TournamentSession` and `UserQualifiers` as genuine aggregates.

## Open questions for the rearch ADR (Step 1.7)

- **Q1.** Recommendations: own vertical (A1, and active WSIP feature work) or inside Player Progression (this map)?
- **Q2.** Catalog: conceptual context realized as shared kernel + shared ports (A2), or its own vertical?
- **Q3.** Scoring engine (`ScoringConfiguration`): into the shared kernel (used by tournaments, Pumbility, world rankings) or vertical-local presets over a smaller kernel?
- **Q4.** Randomizer home: catalog query service vs Chart Intelligence (A1's `CatalogDifficulty` placement) vs competition utility (match card draws used it; matches are being dropped).
- **Q5.** Chart skill tagging (`SkillsSaga`): PlayerProgress (A1 — skills feed skill titles) vs Catalog (it's chart curation)?
- **Q6.** Does `IPiuGameApi` remain a shared port (A2) once OfficialMirror owns the ACL, or do other verticals receive official-site facts only via Mirror events/interfaces?
- **Q7.** Webhooks: per-context event contracts + a generic outbound-delivery service (subscriptions, signing, retries)? Today's partner wiring is bespoke; formalizing it removes per-partner labor and makes the contracts explicit.

Strategy-level (not ADR): whether to expose a Progression API — see [PRODUCT.md](PRODUCT.md#platform-stance).

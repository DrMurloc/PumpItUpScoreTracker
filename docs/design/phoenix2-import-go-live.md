# Phoenix 2 Import Go-Live

Status: **implemented** (C0–C7 on `claude/phoenix-2-importer-validation-7c3eb1`, owner-approved
plan) — awaiting field test and the §8 runbook. The recon instrument and the Phoenix 2 LiveSite
canaries live in
[Phoenix2ImporterReconTests.cs](../../ScoreTracker/ScoreTracker.Tests.Integration/LiveSite/Phoenix2ImporterReconTests.cs)
(dumps to `%TEMP%\p2-importer-recon`, overridable via `PIU_RECON_DUMP_DIR`).

## 1. Context

The owner's test account now carries live Phoenix 2 play data, which made the first real
importer validation possible (2026-07-17). The Phoenix 2 backend has been wired since PR #120
under the "assume identical to Phoenix 1" rule; the recon proves the assumption is broken in
exactly one place — and that the redesigned pages offer three upgrades worth taking at the
same time: per-score saved dates, per-play judgement tables, and broken attempts surfacing on
the best-scores list.

## 2. What the live recon established

Verified against piugame.com with the test account (DRMURLOC #7251, 5 plays), snapshots in
`%TEMP%\p2-importer-recon`:

| Surface | Phoenix 2 state | Importer impact |
|---|---|---|
| Login / session / SSO | Unchanged (login_check + sid + am-pass hop) | Works |
| `game_id_information.php` | Unchanged | `GetCards` works (2 cards, 1 active) |
| `title.php` | 312 entries, **36 `[LEGACY]`-prefixed carry-overs** | `GetAccountData` works; `[LEGACY]` names won't match the P2 title list (silently dropped — see §9) |
| `recently_played.php` | Same card grammar as P1 **plus `p.recently_date_tt` per-play datetime `2026-07-17 23:16:30 (GMT+9)`**; judgement table per card incl. STAGE BREAK cards; no max combo | `GetRecentScores` works (4/4 non-break cards); dates and judgements currently discarded |
| `my_best_score.php` | **REDESIGNED**: old `ul.my_best_scoreList` + `etc_con` grammar is gone; cards reuse the `recently_playeList` layout — song, stepball, grade icon (`/l_img/p2/grade/…`, new), plate img (`/l_img/p2/plate/…`), score `i.tx`, **saved datetime per card**; **strictly newest-first**; `?lv=` level filter (verified live); pager chrome present | **`GetBestScores` parses ZERO scores** — the one hard break |
| Broken attempts | **Appear on the best list**: the stage-broken Chimera D26 shows score `0` with an **empty plate slot** and no grade icon | New capability: broken bests are on the paged, dated list — no longer confined to the 50-play recent window |
| `play_data.php` (new) | Completion dashboard (per-grade / per-plate counts, level filter, catalog total **4,476** charts) with ajax drill-in `/ajax/user_play_log.php` (`data-lv` / `data-type` / `data-division`) | Not used by this effort; candidate future import surface |
| `my_page/pumbility.php` | **Per-chart PUMBILITY + grade + plate** (e.g. ALiVE D21 978,147 SG → 343.57) | Out of scope here; finally makes the per-chart formula (and the singles +1 question) directly testable |
| Phoenix 1 (comparison) | `my_best_score.php` unchanged (no dates); **`recently_played.php` gained the same per-play datetimes** (50 dated cards) | P1 keeps the legacy best-page heuristic; recent-play dates + judgements are capturable on BOTH mixes |

Evidence caveats (n=1 account, 5 plays):

- Multi-page pagination of the new best list is **unverified** (account fits one page;
  out-of-range `?page=` clamps to identical bytes, consistent with P2 board pages). The
  owner will generate more data; re-run the recon before trusting the pager walk.
- The one broken best observed scored 0 (a walkoff). **Assume broken bests usually carry
  real partial scores** (owner-confirmed expectation) — broken detection keys on the empty
  plate slot, never on `score == 0`.

## 3. Decisions (owner-locked 2026-07-17)

1. **Judgement distributions**: nullable columns on the best-attempt row **and** on the
   journal row. Capture now, UI later.
2. **Incremental cutoff**: per-user watermark = **newest card date seen at last import**,
   replacing the `PreviousPageCount` guess + 3-pages-without-upscore walk on Phoenix 2.
   Phoenix 1 keeps the legacy heuristic (its best page has no dates).
3. **Include Broken Scores defaults ON when mix == Phoenix 2** (stays off for Phoenix 1).
4. **Broken scores are excluded from all four contaminated analysis surfaces** (§7):
   competitive level pools, co-op rating pool, cohort score distributions, total rating —
   the last one on both mixes, deliberately bending the "P1 ratings byte-identical"
   guarantee for the rare users with broken imports. Rationale includes abuse resistance:
   partial runs deep into overrated charts could otherwise farm competitive level.
5. Mix-switch staleness is in scope: it blocks switching to Phoenix 2 at all, which blocks
   the import itself (the import page follows the current mix; the old P2 gate no longer
   exists).

## 4. Design

### 4.1 Best-scores parser v2 (OfficialMirror ACL)

`PiuGameApi.GetBestScores` learns the second page shape. **Shape-sniffed, not mix-keyed**:
a page containing `my_best_scoreList` parses via the legacy grammar; otherwise a page
containing `recently_playeList` parses via the new card grammar. (Sniffing survives the day
Andamiro backports the redesign to the Phoenix 1 site.)

New-shape card grammar, per `li` under `ul.recently_playeList`:

- Song name: `div.song_name p` (HtmlDecode, existing name-mapping pipeline).
- Type + level: existing stepball parsing (`tw`/`numw` imgs, `LevelRegex` with its `p2/`
  segment, `?? → 29` fallback).
- Score: the `i.tx` inside `div.li_in.ac`, thousands-stripped, InvariantCulture.
- Plate: `PlateRegex` over the sibling img — **an empty/absent plate img marks the card
  broken** (`IsBroken = true`, plate = null). Score is kept regardless of value.
- Grade icon: ignored (grade derives from score).
- Saved date: `p.recently_date_tt`, format `yyyy-MM-dd HH:mm:ss (GMT+9)` → parse
  InvariantCulture with an explicit `+09:00` offset into a `DateTimeOffset`.
- Pager: existing `next`/`last` icon walk; `MaxPage` parse retained for the legacy shape.

DTO (`PiuGameGetBestScoresResult.ScoreDto`) gains `IsBroken` and `RecordedAt
(DateTimeOffset?)`. Legacy-shape parses leave them `false`/`null`.

`GetRecentScores` (both shapes are already identical here) additionally parses the same
`recently_date_tt` into a `RecordedAt` on its DTO, and **stops discarding the judgement
counts** it already reads (`Perfects/Greats/Goods/Bads/Misses` ride the DTO). STAGE BREAK
cards remain skipped in `GetRecentScores` (unchanged behavior — the broken data path is the
best list now).

### 4.2 Watermark cutoff (Phoenix 2 incremental import)

In `OfficialSiteClient.GetRecordedScores`, the Phoenix 2 path replaces both legacy passes:

```
watermark = UiSetting["BestScoreWatermark__{mix}__{cardId}"]  (null on first import)
page = 1
loop:
    cards = GetBestScores(page)
    take cards where RecordedAt >= watermark   (ties included — overlap by design)
    if any card.RecordedAt < watermark: stop after this page
    if page == last page: stop
    page++
newWatermark = max(RecordedAt) seen this run (falls back to previous when no cards)
saved at the same point the legacy path saved PreviousPageCount
```

- The list is strictly newest-first (recon-verified), so the first card older than the
  watermark ends the walk; the page containing the crossing is processed whole. Upserts are
  idempotent, so the overlap is free correctness margin.
- Watermark comparisons are site-time vs site-time — the local clock never participates, so
  clock skew cannot lose scores.
- **Keyed per card**, fixing a latent bug the page-count heuristic always had (two cards on
  one account previously shared one page count).
- First import (no watermark): full walk, exactly like today's first import.
- Phoenix 1 keeps `PreviousPageCount` + the upscore walk untouched; the pre-walk
  `GetScorePageCount` call is skipped on the P2 path.
- Up-scores need no separate pass on P2: improving a score refreshes its saved date, which
  re-surfaces the chart above the watermark. (Implied by "saved date", sanity-checked
  during the owner's next data session: improve one existing score, re-import.)
- `includeBroken: false` on P2 must **skip broken best-list cards** at this merge point —
  otherwise the checkbox stops meaning anything on Phoenix 2.

### 4.3 Journal truth-time + judgement capture (ScoreLedger)

- `UpdatePhoenixBestAttemptCommand` gains optional `RecordedAt (DateTimeOffset?)` and the
  five judgement counts (all nullable).
- `UpdatePhoenixRecordHandler` persists them: journal rows get `OccurredAt = RecordedAt ??
  clock.Now` (real play/save time for P2 imports from day zero of the mix), best-attempt
  rows get `RecordedDate = RecordedAt ?? clock.Now` plus the judgement columns. The
  KeepBestStats broken-over-pass rule extends to judgements (stats travel together).
- `PhoenixRecordEntity` and `ScoreEventJournalEntity` each gain five nullable int columns
  (`Perfects`, `Greats`, `Goods`, `Bads`, `Misses`). One EF migration covers both tables.
- **Attribution rule** (import-side, in `OfficialSiteClient`): a recent play attributes its
  judgements to the best attempt being saved when `chartId` matches, `score` matches
  exactly, and broken-ness matches. Ambiguity (two same-score plays of one chart in the
  window) resolves to the latest by `RecordedAt` — identical judgements are overwhelmingly
  likely anyway at equal score.
- The domain read model (`RecordedPhoenixScore`) exposes the new fields for future UI.
  **`api/*` DTOs are untouched** — no wire-shape change, no Tests.Api golden churn.
- `RecordedDate = site date` also feeds the personalized-breakdown freshness weighting with
  truthful ages (better than import-time for players who import rarely).

### 4.4 Broken-score exclusions (the §3.4 audit fixes)

| Site | Fix |
|---|---|
| `PlayerRatingSaga.RecalculateCore` competitive pools (~205) | `competitiveScores` filters `!IsBroken` |
| Same file, co-op pool (~224) | `coOps` filters `!IsBroken` (fixes P1 broken-co-ops-add-rating and the polluted co-op average) |
| Same file, total rating (~243) | Sum over `!IsBroken` on both mixes |
| `EFPhoenixRecordsRepository.GetPlayerScores` (userIds+chartIds overload, ~312) | Adds `!pba.IsBroken` — cleans `CohortScoreProvider` distributions and every percentile consumer behind it (`ScoreQualitySaga`, `GetChartScoreRankingsQuery`, suggested-charts gates, breakdown cohort medians). Build-time check: enumerate the overload's callers; if any turns out to want broken rows, the filter moves up into `CohortScoreProvider` instead. |

Everything else audited clean (2026-07-17): tier-list sagas, scoring difficulty, chart
verdicts, blend builder, recaps, recommendations (broken = not-passed, correct), folder
lamps, top-50 PUMBILITY pools, By-Level widget, attempts-vs-passes aggregates, display
surfaces that label broken deliberately, Experiments pages (consume filtered products).

**Stored stats go stale on deploy** — see the runbook (§8).

### 4.5 Include Broken defaults (Presentation)

- `UploadPhoenixScores`: `_includeBroken` initializes to `_currentMix == MixEnum.Phoenix2`
  once the mix loads (checkbox stays user-overridable).
- `ImportScoresWidget`: the hardcoded `false` in its `StartOfficialImportCommand` becomes
  `_mix == MixEnum.Phoenix2`.

### 4.6 Mix-switch fix (Presentation)

Root cause: `ShellModelFactory` caches per-user UiSettings in `IMemoryCache` for 5 minutes,
`ResolveMix` prefers the cached setting over the cookie, and nothing invalidates on
`/Mix/Set` — a signed-in switch waits out the TTL (a user's first-ever switch works via the
cookie path, which is why fresh-user tests pass). Fix: a Web-registered MediatR
post-processor on `SaveUserUiSettingCommand` evicts `ShellModelFactory__Settings__{userId}`
for the current user — mix switches, /Account theme overrides, game-tag and avatar writes
all become immediately visible. (Build-time check: confirm the singular command is the only
UiSettings write path.)

## 5. Technical scope by layer

| Layer | Changes |
|---|---|
| **SharedKernel** | None. |
| **Domain (core)** | Additive record fields only: `OfficialRecordedScore` (+`RecordedAt`, judgements), journal entry record, `RecordedPhoenixScore` read model. No new ports, no new services, no behavior. |
| **Application (core)** | None — everything lives in verticals. |
| **OfficialMirror** | The bulk: `PiuGameApi` parser v2 + DTO fields (Infrastructure/Apis), `OfficialSiteClient` watermark walk + attribution + includeBroken semantics, saga watermark persistence (Application). Vertical-internal except the ScoreLedger command call. |
| **ScoreLedger** | `UpdatePhoenixBestAttemptCommand` optional fields (Contracts), `UpdatePhoenixRecordHandler` persistence (Application), entity columns + journal `OccurredAt` pass-through + `GetPlayerScores` broken filter (Infrastructure). |
| **PlayerProgress** | `PlayerRatingSaga` three `!IsBroken` filters (Application). |
| **Data** | One EF migration (column adds on `PhoenixRecordEntity` + `ScoreEventJournalEntity` tables). |
| **Web (Presentation)** | Three small touches: UiSettings-save cache eviction (post-processor + `ShellModelFactory` key exposure), `UploadPhoenixScores` checkbox default, `ImportScoresWidget` default. No new components, no routing, **no new localization keys**. |
| **CompositionRoot** | None. |
| **Contracts NOT touched** | `api/*` DTOs (Tests.Api goldens stay), bus event shapes (`ScoreImportCompletedEvent` unchanged), E2E fixtures (P1-shaped, still valid). |

## 6. Commit plan

Each commit builds green and passes the fast suites on its own.

- **C0** — this document.
- **C1** — Mix-switch fix: UiSettings-save eviction post-processor + `ShellModelFactory`
  key exposure; service-level regression test (real `MemoryCache`, save → rebuild sees the
  new mix). *First so the owner can drive Phoenix 2 in-app immediately.*
- **C2** — Parser v2: shape-sniffed `GetBestScores`, DTO `IsBroken`/`RecordedAt`,
  `GetRecentScores` dates + judgement DTO fields; sanitized approval fixtures cut from the
  recon snapshots (new-shape happy path, broken empty-plate card, date parse, legacy shape
  pinned unchanged). Pure ACL — nothing downstream consumes the new fields yet.
- **C3** — Storage: EF migration (judgement columns ×2 tables), `UpdatePhoenixBestAttemptCommand`
  optional fields, handler persistence + journal `OccurredAt` pass-through; integration
  round-trip facts; DATABASE-SCHEMA.md column notes.
- **C4** — Import orchestration: P2 watermark walk (per-card key), retire the P2
  page-count/upscore path, includeBroken best-card semantics, judgement attribution,
  real-time journal stamps; component tests over `OfficialSiteClient` with a stubbed
  `IPiuGameApi` (cutoff stop condition, watermark save, attribution matcher, broken
  filtering both checkbox states).
- **C5** — Broken-score exclusions: `PlayerRatingSaga` ×3 + `GetPlayerScores` filter (after
  the caller check); rating-saga facts for each exclusion.
- **C6** — Include Broken defaults ON for P2 (page + widget), bUnit default-state facts.
- **C7** — Wrap-up: convert the recon Record()-probes into asserting LiveSite canaries for
  the P2 import path, docs touch-ups, full suite run.

Optional (owner call, not in the plan until requested): **journal-all-recent-plays** —
append every recent play (not just best-improvements) as journal events with a
`(UserId, ChartId, OccurredAt, Source)` dedupe check, now possible because plays carry
site datetimes. Richest possible chart-analysis corpus; adds journal volume and a
dedupe-on-write path. See §9.

## 7. Test plan

| Suite | Coverage |
|---|---|
| `Tests` (approval) | Parser v2 fixtures: new shape, broken card, dates, judgements; legacy shape pinned byte-stable |
| `Tests` (Application) | `OfficialSiteClient` watermark/attribution/includeBroken; `UpdatePhoenixRecordHandler` field persistence + KeepBestStats interplay; `PlayerRatingSaga` exclusions |
| `Tests.Components` | Shell settings eviction regression; checkbox/widget defaults per mix |
| `Tests.Integration` | Journal + record column round-trips on real SQL; migration applies via fixture |
| `Tests.Api` | Untouched — goldens must not change (that's the point) |
| `Tests.E2E` | Untouched — P1-shape WireMock fixtures remain valid |
| LiveSite (manual) | C7 canaries: P2 login → cards → account → best (non-empty, typed, dated) → recent (judgements) |

## 8. Post-deploy runbook (owner)

1. Press **Recalculate Player Ratings** for both mixes (the §4.4 exclusions change stored
   `PlayerStats`; cohort caches self-heal within the hour).
2. First Phoenix 2 import on the real account; verify the session card, journal rows carry
   site datetimes, judgements present where attributable.
3. Next play session, generate the validation set: a **mid-chart break with a real partial
   score** (confirms nonzero broken bests), enough plays to spill `my_best_score.php` onto
   page 2 (validates the pager walk + cutoff), and one **up-score of an old chart**
   (confirms date-refresh re-surfacing). Re-run the recon test afterwards.

## 9. Open questions (parked, not blocking)

1. **Journal-all-recent-plays** (optional commit above) — say the word and it slots in
   after C4.
2. **`[LEGACY]` titles** (36 on the P2 site): currently dropped by name-mismatch on import.
   Map to P1 equivalents, surface as-is, or keep ignoring?
3. **4,476 vs 4,469**: the site's play_data denominator exceeds our compiled P2 catalog by
   7 charts. Diff on a future catalog sweep.
4. `/ajax/user_play_log.php` as a structured import surface — unprobed; revisit if page
   scraping gets brittle.

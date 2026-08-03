# Import Completeness Check

Status: **designed, not built.** Branch `claude/pumbility-mismatch-detector-378a55`.

## 1. The problem

Players report "my PUMBILITY doesn't match piugame" with a screenshot. Every reported case so far
has been the same thing: one or two charts the importer never saw, buried too deep in the
best-score list for the incremental walk to reach. The formula has been exonerated repeatedly
(`ExplorationTests/LiveSite/PumbilityOfficialReconciliationTests` reproduces the official
per-chart values to the cent on both mixes).

Today the diagnosis is manual — the owner queries the database, compares against the screenshot,
and the only repair is clearing the player's rows so the next import runs a full walk
([p2-best-score-page-semantics](../../CLAUDE.md), [score-truth-model.md](score-truth-model.md)).
This feature makes the site diagnose and repair it.

## 2. What the official site actually exposes

Probed live 2026-08-02 on both hosts against a 2,851-chart Phoenix account and an 18-chart
Phoenix 2 one — `ExplorationTests/LiveSite/OfficialCensusProbeTests`, which stays in the tree as
the instrument that re-verifies these grammars when Andamiro redesigns something.

### 2.1 `my_page/play_data.php` — the census surface

Same `?lv=` parameter on both mixes; **different page**.

| | Phoenix | Phoenix 2 |
|---|---|---|
| Grade tiles | **none** | 16, **cumulative** (SSS+→F), rendering `N / <catalogTotal>` |
| Plate tiles | 8, **exact** counts summing to the clear count | 8, **cumulative** |
| Headline | `div.clear_w .l_con .t1` = `"2,776/3,646"` + `Progress 76%` | none — the `F` tile is the pass count |
| Level buckets | **10**–26, 27over, 10over, coop | **1**–26, 27over, 10over, coop |
| Counts | passes only (stage breaks absent) | passes only |

Tiles are `a.play_log_btn.txt[data-type][data-division] > i.t_num`. Phoenix omits `data-division`
and offers plate types only.

Cumulative means "this grade or better", so on Phoenix 2 the **`F` tile is the total pass count**
and consecutive differences give an exact grade histogram. Phoenix's plate counts are already
exact. Both normalise to the same shape: *passes at this level, broken down by grade (P2) or
plate (P1)*.

### 2.2 `my_page/my_best_score.php` — the repair surface

Both mixes: `div.total_wrap i.t2` = `Total.`, same `?lv=` buckets (no sub-10 bucket on either),
12 cards per page. Phoenix lists passes only; Phoenix 2's redesigned list also carries stage
breaks, which is why `MyBest All − play_data F` = the broken-best count exactly (18 − 16 = 2 on
the probed account). We do not surface that number — see §7.

### 2.3 `my_page/pumbility.php` — the headline

Exists on **both** mixes and is **live** (the ranking board's MY RANKING DATA is a daily 01:00
KST batch and must not be used).

- **Phoenix 2**: the per-chart breakdown — `li > div.top-wrap` rows carrying grade, plate, score
  and the official per-chart value (`pumbility-point-sub`).
- **Phoenix**: the total (`PUMBILITY 64,466`) plus 50 stepball cards, in a *different* grammar —
  no `top-wrap`, no per-chart values. Phoenix PUMBILITY is plate-blind (`Base × gradeModifier`),
  so we price its 50 charts ourselves; only the total needs reading.

### 2.4 `/ajax/user_play_log*` — naming charts inside a cell

Two hops. `POST /ajax/user_play_log.php {lv, type, division}` returns a stub that GETs
`/ajax/user_play_log_detail.php` (Phoenix / plate) or `…detail2.php` (Phoenix 2 / grade), paged
`?lv=&type=&page=N` at **6 rows per page** — half of `my_best_score.php`'s 12. It is worth using
only when the histogram has localised the gap to a small cell; for anything larger, page the
best-score list, whose parser is already approval-pinned.

The POST is a read — it returns modal HTML and changes nothing on the account.

## 3. Detectors

Four, in ascending cost. The first three run together as one pass.

| Detector | Catches | Cost |
|---|---|---|
| PUMBILITY headline | anything wrong inside the top 50 | 1 request |
| Level census (pass counts) | a chart missing anywhere in the account | ~19 requests |
| Grade / plate histogram | a stale score that crossed a grade (P2) or plate (P1) boundary | free — same responses as the census |
| Deep scan | everything, including a same-band up-score | `ceil(total / 12)` requests |

**Stated honestly: the census detects presence, not freshness.** A score we hold at a stale value
inside the same grade and plate band is invisible to it. That is what the deep scan is for, and
why the deep scan cannot be designed away.

### 3.1 The census algorithm

1. **Run a normal import first.** The check imports before it counts, never counts on its own —
   otherwise a player who played twenty minutes ago reads as "missing 6 charts", which is true and
   useless. The button on the page is *Import and check*. The reverse is **not** true: a plain
   import does not run a check (§6).
2. For each `?lv=` bucket the mix offers, fetch `play_data.php?lv=X` and normalise the tiles into
   `passes[level]` plus `histogram[level][grade|plate]`.
   - Phoenix 2 covers levels 1–26 directly.
   - Phoenix starts at 10; sub-10 passes come from `my_best_score.php` `All` minus the buckets
     (verified closing on the probed account).
3. Read our side: records where `IsBroken = 0 AND Score IS NOT NULL`, grouped by the **mix's**
   level (`ChartMix`, not `Chart.Level`), co-op separated. Grades are **re-derived per mix** from
   the raw score — a stored `LetterGrade` on a Phoenix row is the Phoenix grade
   ([phoenix2-grade-thresholds](../../CLAUDE.md)).
4. Compare per level, then per histogram cell.

### 3.2 Never trust the whole-account total

On the probed Phoenix account the totals matched **exactly** — site 2,851, ours 2,851 — while the
per-level census was short one at level 18 and long one below level 10. Two unrelated
discrepancies that cancelled. A one-request total check would have reported "in sync" and been
wrong.

**The comparison is per level. There is no cheaper correct version of it.**

### 3.3 Localise, then name

For each level where the site has more passes than we do, pick the cheaper enumeration:

- `ceil(cellCount / 6)` — the drill-in, when the histogram narrows it to one grade/plate cell.
- `ceil(levelTotal / 12)` — `my_best_score.php?lv=N`, otherwise.

Then diff the enumerated charts against our records and repair through the saga's existing save
path.

### 3.4 Charts we cannot map

A site chart that resolves to no catalog chart is a **catalog gap**, and no amount of crawling
fixes it — `MapBestList` drops it silently today
([OfficialSiteClient.cs](../../ScoreTracker/ScoreTracker.OfficialMirror/Infrastructure/OfficialSiteClient.cs)).
Every unmappable sighting this check produces upserts into `OfficialMissingChart`, the admin
inbox the leaderboard sweep already feeds, so a player's complaint becomes an actionable row on
`/Admin/OfficialLeaderboards` instead of a support message. The player is told we have flagged it.

## 4. Outcomes

Exactly five, and the UI says which one it is.

| Outcome | Meaning | Action offered |
|---|---|---|
| **In sync** | every level and cell agrees | none |
| **Missing scores** | the site has passes we do not | named list + *Add these scores* |
| **Out-of-date scores** | histogram disagrees at matching totals | named list + *Update these scores* |
| **Unrecognised charts** | the site lists charts our catalog lacks | reported to the admin inbox automatically |
| **Nothing found** | census clean, player still disagrees | *Deep scan* (credited, §5) |

A level where **we** hold more than the site is not an error — it is a CSV import, a manual entry,
or a chart the site retired. It never triggers a repair and is not shown by default.

## 5. Deep scan and rate limiting

A full walk of `my_best_score.php` ignoring the up-score window: `ceil(total / 12)` requests, ~238
for a 2,851-chart account. This is the escape hatch for "the census says clean and I still
disagree", and the only repair for a same-band stale score.

- **3 per calendar month per user.** Admins exempt.
- **Only a blind deep scan burns a credit.** A repair the census localised is bounded and
  evidence-driven, so it is free — credits govern "walk everything", not "fix what we found".
- **Global concurrency cap** — the existing per-user `IImportConcurrencyGuard` gains a global slot
  count rather than growing a second guard beside it; it is the same concern. Three simultaneous
  full walks is a rude amount of traffic to point at piugame.
- When credits are exhausted, the panel shows the date the next one unlocks **and the last check's
  summary in a copyable form**, so the message that reaches DrMurloc in Discord arrives with data
  attached.

## 6. Where it lives

`/UploadPhoenixScores`, below the import controls — it needs a session, and the page already owns
the saved-credential flow, the background-job handoff and the status hub from PR #150.

**A player presses it. It never rides along with a normal import** (owner call, 2026-08-02): a
check is ~20 extra requests at piugame, and making every import pay that permanently is the wrong
default for a problem a minority hit. The cost of that choice is honest — discovery now depends on
the player finding the button, so the bug class does not self-report. Revisit if the button turns
out to be pressed constantly.

Progress rides the existing `ImportStatusUpdatedEvent`, which means the nav-bar import pulse lights
during a check too. Accepted: it is the same kind of work from the player's side.

The last result is persisted and rendered on load, so opening the page shows the standing verdict
without touching piugame.

## 7. Explicitly out of scope

- **The broken-best delta.** We can compute exactly how many stage breaks the site holds that we
  do not, and it is noise for a player who opted out of importing them. Not shown. (Owner call,
  2026-08-02.)
- **A single-request "quick check".** §3.2 — it is wrong often enough to be worse than nothing.
- **Repairing scores below the top 50 automatically.** The census names them; the player presses
  the button.
- **Sub-10 per-level detail on Phoenix.** Its `play_data.php` starts at 10, so sub-10 is one
  aggregate residual there. Sub-10 charts price at zero in PUMBILITY on both mixes.

## 8. Architecture

Everything scraping-side is **OfficialMirror** — it is the PiuGame anti-corruption layer.

| Piece | Where |
|---|---|
| `GetPlayDataCensus(mix, sid, ct)`, `GetPumbilityTotal(mix, sid, ct)`, `GetPlayLogPage(...)` | `IPiuGameApi` (internal) + `PiuGameApi` |
| Census normalisation (cumulative→exact, bucket→level) | `OfficialMirror/Domain`, pure and unit-testable |
| `StartImportCheckCommand` → `RunImportCheckCommand` (bus) → consumer → saga | mirrors `StartOfficialImportCommand` exactly, including `SetScopedUser` |
| `GetLastImportCheckQuery`, `ImportCheckReport` record | `OfficialMirror/Contracts` |
| `ImportCheckRun` entity (UserId, MixId, RanAt, Kind, OfficialTotal, OurTotal, FindingsJson) | `OfficialMirror/Infrastructure/Entities`, registered via its `IDbModelContribution` |
| Progress + completion to the UI | the existing `IUiNotificationHub` user topic, via a bridge |

The new table carries a `UserId`, so it must be named in OfficialMirror's `UserOwned` purge
manifest and gets a row in [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md). Rate-limit accounting reads
the same table (`Kind = 'deep'`, current month).

Our-side counts come through ports OfficialMirror already holds (`IPhoenixRecordRepository`,
the catalog reader) — no new cross-vertical reference.

## 9. Verification plan

- **Unit** (`DomainTests`): cumulative→exact normalisation, bucket→level mapping incl. `27over` /
  `coop` / the Phoenix sub-10 residual, per-level and per-cell diffing, the cheaper-enumeration
  choice.
- **Component** (`ApplicationTests`): the saga against mocked ports — each of the five outcomes,
  the credit ledger, and that a localised repair does not burn a credit.
- **Component** (`Tests.Components`): the panel's five states.
- **Integration**: `ImportCheckRun` round-trip and the purge.
- **Exploration**: `OfficialCensusProbeTests` stays as the grammar canary. The build should extend
  it once to assert the census agrees with our stored counts **at every level** on a real account,
  not just in aggregate — that is the check §3.2 exists because of.

## 10. Open questions

1. **UCS in the denominators.** Our Phoenix catalog matched the site's level denominators exactly
   at 18/25/26 (373/68/35), which is good evidence that UCS charts do not pollute the level
   buckets — but it was three levels, not all of them. The build should sweep every level once.
2. **Co-op.** Both mixes bucket it separately and we store co-op records; the census should cover
   it, but co-op never contributes to PUMBILITY, so a co-op-only gap should probably read as
   informational rather than as a PUMBILITY explanation.
3. **Half-double / performance charts.** Present in the best list, not yet checked against the
   census denominators.

## 11. Technical scope

Everything is inside **OfficialMirror**. `OfficialSiteClient` already injects `IChartRepository`
(mix-resolved levels) and `IScoreReader` (our bests), so both sides of the comparison are reachable
with **no new project reference and no new cross-vertical port**. Nothing is added to core
`Domain` or `Application`.

| Layer | Location | New types |
|---|---|---|
| Contracts *(public)* | `OfficialMirror/Contracts` | `StartImportCheckCommand`, `StartDeepScanCommand`, `GetLastImportCheckQuery`, `ImportCheckReport` + findings, `Messages/RunImportCheckCommand`, `Events/ImportCheckCompletedEvent` |
| Domain *(internal)* | `OfficialMirror/Domain` | `OfficialCensus`/`LevelCensus`, `CensusNormalizer`, `CensusDiff`, `IImportCheckRepository`, `IOfficialSiteClient` additions |
| Application *(internal)* | `OfficialMirror/Application` | `ImportCheckSaga`, `StartImportCheckHandler`, `RunImportCheckConsumer`, `ExecuteImportCheckCommand` |
| Infrastructure *(internal)* | `OfficialMirror/Infrastructure` | `PiuGameApi` parsers + DTOs, `OfficialSiteClient.GetOfficialCensus`, `ImportCheckRunEntity`, `EFImportCheckRepository`, `EFAccountPurgeRepository.UserOwned` |
| Wiring *(public)* | `OfficialMirror/Wiring` | model-contribution row, DI, consumer registration |
| Data | `ScoreTracker.Data/Migrations` | `AddImportCheckRun` |
| Web | `Pages/UploadPhoenixScores.razor`, `Components/ScoreCheckPanel.razor` | the panel |

**Infrastructure scrapes, Domain compares, Application orchestrates.** `GetOfficialCensus` returns
the official side only and never reads our records, so the whole detection algorithm is a pure
function under unit test. The `Start*` (circuit) → `Run*` (bus) → `Execute*` (internal MediatR)
triplet is copied from the import path verbatim, including **`SetScopedUser`, never
`SetCurrentUser`**.

⚠ **OfficialMirror has no `UserOwned` purge manifest today** — its purge only *unlinks*
`OfficialPlayerEntity`, which sits in `AccountPurgeCoverageTests.Exempt` with a written reason.
`ImportCheckRun` carries a `UserId`, so this feature introduces that vertical's first manifest and
delete loop, covered by the decoy-account integration test.

### Commit order

| # | Commit | Proof |
|---|---|---|
| C1 | ACL parsers + captured fixtures | approval tests, both mixes |
| C2 | `CensusNormalizer` + `CensusDiff`; probe asserts per-level agreement | `DomainTests` + exploration probe |
| C3 | `GetOfficialCensus` scrape orchestration | component tests, mocked `IPiuGameApi` |
| C4 | Entity, migration, repository, purge manifest, schema doc | integration round-trip + purge |
| C5 | Start/Run/Execute triplet + saga | component tests, all five outcomes |
| C6 | Repair path + unmappables to the admin inbox | component tests |
| C7 | Deep scan, credit ledger, global concurrency slot | component tests |
| C8 | UI panel + hub subscription | bUnit, the nine states |
| C9 | l10n ×9 + docs | ratchets |

An automatic check at the tail of every import was scoped and then **cut** (§6).

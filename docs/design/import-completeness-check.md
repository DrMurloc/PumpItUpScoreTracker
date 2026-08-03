# Import Completeness Check

Status: **built** on `claude/pumbility-mismatch-detector-378a55`, not yet PR'd. Suites green
(2051 / 148 / 557 / 209). **Deferred: the nine-locale sweep** — the panel's strings render English
by key-fallback and the locale-parity ratchet stays green because no locale carries the keys yet.

> **A check saves what it finds** (owner call, 2026-08-03). Nobody looks at a list of their own
> scores from the official site and declines one, so anything a run recovers is written on the spot
> as a normal import — same session, same journal, same rating sweep — and lands on the player's
> sessions page like any other. A run hands back a count, not a verdict, and the panel is a line
> and two buttons.
>
> **Nothing about a check is persisted** (owner call, 2026-08-03, after field-testing a verdict
> that survived an app restart). A table, a migration, a repository and the vertical's first purge
> manifest were disproportionate machinery for remembering a sentence for a few minutes. The
> result rides `ImportCheckCompletedEvent` to whichever circuit is still listening and lives in
> the page until the player navigates away — which is what the panel's "stay on this page" line is
> buying. The one thing that genuinely outlives a session is the deep-scan allowance (§5).

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
`?lv=&type=&page=N` at **6 rows per page** — half of `my_best_score.php`'s 12. The POST is a read:
it returns modal HTML and changes nothing on the account.

**It is not on the repair path**, and the enumeration-cost choice this section originally scoped
does not exist. Its rows carry chart identity and a grade image but **no score and no plate**, so
nothing there can be saved. The best-score list is the only surface that can, and it filters by
level and nothing finer — which is why a repair always re-reads whole levels. The parser stays in
the ACL (approval-pinned) because it is how the gap was diagnosed by hand, and it names charts
inside a cell in one request when a human is looking.

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

### 3.3 Localise, then re-read

For each level that is short a chart or holding a stale one, page `my_best_score.php?lv=N` to the
end and save anything that beats what we hold, through the import's own save loop (extracted, not
copied — a scrape may only ever RAISE a record, and a second hand-written copy of that rule is
what let plate improvements drag scores down). The repair opens a real score session, so Undo and
the journal see it as one more official run.

Whole levels, never the band the finding localised to — §2.4. And a repair that localised nothing
returns early rather than passing an empty bucket list, which downstream means "walk everything":
the one thing a free repair must never quietly become.

The Phoenix sub-10 residual is **not repairable this way** — that mix will not filter its best list
below level 10 either, so those charts are reachable only by a deep scan.

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
| **In sync** | every level and cell agrees | *Deep scan* (credited, §5) |
| **Needs attention** | the site has passes we do not, and/or we hold a score it has beaten | **one named list**, both kinds together + *Add these N scores* |
| **Unrecognised charts** | the site lists charts our catalog lacks | not built — see §11 |

Missing and out-of-date are **one verdict and one list**, never two views: an account can be short
a chart and behind on another at the same time, and splitting them would make the player fix the
same account twice. A row that carries the score we already hold is one they have beaten since
their last import — that "was" is the entire distinction, so the rows need no other label.

There is no *check again* button. Re-running an unchanged account returns the same verdict.

A level where **we** hold more than the site is not an error — it is a CSV import, a manual entry,
or a chart the site retired. It never triggers a repair and is not shown by default.

## 5. Deep scan and rate limiting

A full walk of `my_best_score.php` ignoring the up-score window: `ceil(total / 12)` requests, ~238
for a 2,851-chart account. This is the escape hatch for "the census says clean and I still
disagree", and the only repair for a same-band stale score.

- **A balance on the user row**, not a dated usage count: `User.DeepScansRemaining`, refilled to
  `DeepScanAllowance.PerMonth` by the `reset-deep-scans` Hangfire job on the 1st. Granting someone
  extra scans is then a single UPDATE that survives until the next reset. The spend is one guarded
  UPDATE, so two tabs racing the last scan produce one grant and one refusal.
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
| `ImportCheckCompletedEvent` (a count, not a verdict) | `OfficialMirror/Contracts/Events` |
| `ImportCheckCompletedEvent` (the verdict itself — nothing stores it) | `OfficialMirror/Contracts/Events`, bridged to `UiTopics.User` |
| `User.DeepScansRemaining` + `SpendDeepScanCommand` / `GetDeepScansRemainingQuery` / `ResetDeepScansCommand` | `ScoreTracker.Data` entity; Identity owns the operations |
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
- **Integration**: the deep-scan balance — an atomic spend that never oversells, and a reset that refills every row.
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
| Contracts *(public)* | `OfficialMirror/Contracts` | `StartImportCheckCommand`, `ImportCheckStartResult`, `Messages/RunImportCheckCommand`, `Events/ImportCheckCompletedEvent` |
| Domain *(internal)* | `OfficialMirror/Domain` | `AccountCensus`/`CensusBucket`, `CensusBuckets`, `LocalCensusBuilder`, `CensusDiff`, `IOfficialSiteClient` additions |
| Application *(internal)* | `OfficialMirror/Application` | `ImportCheckSaga`, `StartImportCheckHandler`, `RunImportCheckConsumer`, `ExecuteImportCheckCommand` |
| Infrastructure *(internal)* | `OfficialMirror/Infrastructure` | `PiuGameApi` parsers + DTOs, `OfficialSiteClient.GetOfficialCensus` and `GetBestScoresIn` |
| Wiring *(public)* | `OfficialMirror/Wiring` | model-contribution row, DI, consumer registration |
| Data | `ScoreTracker.Data` | `UserEntity.DeepScansRemaining` + `AddUserDeepScansRemaining`; `IUserRepository` gains the spend/read/reset trio |
| Web | `Pages/UploadPhoenixScores.razor`, `Components/ScoreCheckPanel.razor` | the panel |

**Infrastructure scrapes, Domain compares, Application orchestrates.** `GetOfficialCensus` returns
the official side only and never reads our records, so the whole detection algorithm is a pure
function under unit test. The `Start*` (circuit) → `Run*` (bus) → `Execute*` (internal MediatR)
triplet is copied from the import path verbatim, including **`SetScopedUser`, never
`SetCurrentUser`**.

**OfficialMirror still has no `UserOwned` purge manifest**, and that is now a feature of the
design rather than a gap in it: the vertical owns no user-keyed table, so its purge stays the
one-line unlink of `OfficialPlayerEntity` it has always been. The allowance lives on the `User`
row, which the account purge already removes.

### Commit order

| # | Commit | Proof |
|---|---|---|
| C1 | ✅ ACL parsers + captured fixtures | 11 approval tests, both mixes |
| C2 | ✅ census normalisation + diff; probe reconciles per level | 21 `DomainTests` + the probe |
| C3 | ✅ `GetOfficialCensus` scrape orchestration | 7 component tests |
| C4 | ⛔ **reverted** — the run table and its purge manifest | replaced by "nothing is stored" |
| C5 | ✅ Start/Run/Execute triplet + saga | 14 component tests |
| C6 | ✅ repair path and deep scan (one walk, buckets or none) | folded into C5's suite |
| C7 | ✅ UI panel + hub subscription | 12 bUnit tests |
| D1 | ✅ allowance as a balance on `User` + monthly Hangfire reset | 5 integration tests, incl. an 8-way spend race |
| D2 | ✅ persistence out; one findings list; song art | the suites above |
| D3 | ⛔ l10n ×9 | deferred — English by key-fallback |

Four things were scoped and then dropped, recorded here rather than silently: an automatic check
at the tail of every import (§6, owner call); the drill-in enumeration-cost choice (§2.4 — the
modal carries no score, so there was never a choice); persisting the verdict (the header note);
and a *check again* button (§4 — an unchanged account returns the same verdict).

**Not yet built**: unmappable sightings from a check do not yet reach the `OfficialMissingChart`
admin inbox (§3.4). The census reports the level as short; the catalog gap behind it still needs
the wiring.

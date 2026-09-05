# March of Murlocs Overhaul

Status: **Slices 0, 1, R, 2 and 3 are shipped** — the tables are live under the old pages
(PR #274) and MoM's import reads the score journal (§2.5, resolved 2026-08-24). The slice order
was re-ordered after Slice 1 (§8); next is 4a, the read surfaces, which deletes the old pages. Explored 2026-08-09, workshopped
and mocked 2026-08-10/11; mocks redrawn and re-decided 2026-09-05 (§11.9).

March of Murlocs is the site's quarterly stamina ladder: 1 hour 45 minutes to bank as many
points as you can, scored with PUMBILITY+. It has been running on autopilot since
[`4474ce15`](#) and the autopilot has been broken since 2026-07-01.

This pass does four things: stop a live runaway that has created 71 garbage tournaments,
**decouple MoM from the `scores.Tournament` table** (which is about to lose qualifiers too and
then die), give **each mix its own leaderboard**, and rebuild a UI that has never had a design
pass.

Today's pages: [`MarchOfMurlocs.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/MarchOfMurlocs.razor)
(season list), [`StaminaTournament.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/StaminaTournament.razor)
(board), [`RecordTournamentSession.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/RecordTournamentSession.razor)
(submission), [`SessionBuilder.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/SessionBuilder.razor)
(the "Test Scores" planner). Logic lives in
[`MarchOfMurlocsHandler.cs`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs).

All four are **retired rather than ported** — see §11, which is what Slice 1 decided and what the
mocks were built from. Slice 1 deliberately precedes the schema work so UI decisions can still move
the tables, and it did: D16 alone deleted a uniqueness constraint from §6, D20 and D21 arrived from
drawing the pages, and building them surfaced two live defects (§2.8, §2.9).

**The mocks were redrawn on 2026-09-05** (seven surfaces — the six from August plus the My
Sessions on-ramp) and live as Artifacts; the decisions they settled are in §11.9 and the build
scope in §12. They are **not checked in**: each page embeds its own art (about 8 MB across the
seven), and the previous checked-in set was removed — its landing mock had never rendered (an
unterminated string in its script) and none of the six declared a charset. The build pipeline
(templates, the shared skin that mirrors `site.css`, the SQL-pull → JSON scripts, the rest-chart
analysis) sits in the owner's `Downloads\mom-mocks-v2-2026-09-05\`, README inside. Every figure
in them is production-synced data.

| | Surface | Route | Mock |
|---|---|---|---|
| 1 | Season | `/MarchOfMurlocs` | [artifact](https://claude.ai/code/artifact/b6d79211-5b45-41cd-9f8d-3f1e9b5e3c03) |
| 2 | Session Breakdown | `/MarchOfMurlocs/Session/{id}` | [artifact](https://claude.ai/code/artifact/8baf7014-c37d-4f8b-bffc-cca62a227ea1) |
| 3 | Submit | `/MarchOfMurlocs/Session/{id}/Edit` | [artifact](https://claude.ai/code/artifact/8211ff05-b654-4445-96ef-aa26d5aee0c2) |
| 4 | Planner | `/MarchOfMurlocs/Planner` | [artifact](https://claude.ai/code/artifact/17d2ed81-eaef-451a-9c74-0ab5792ba3f2) |
| 5 | Discord card | — | [artifact](https://claude.ai/code/artifact/58eb672e-a479-4845-80ac-4c3d36544b55) |
| 6 | Past seasons | *a dialog, no route* | [artifact](https://claude.ai/code/artifact/360658de-b554-4e15-9de4-37c1bb6faba4) |
| 7 | My Sessions on-ramp | `/Player/{id}/Sessions` | [artifact](https://claude.ai/code/artifact/b0b13385-89b9-412b-b135-16d9099122ec) |

---

## 1. The rules

Confirmed by the owner 2026-08-09 against the
[rules doc](https://docs.google.com/document/d/1Nwr-PDy6lgkTSt4dKu1-0fdeDXdgLWvl7j5yiuIcRCw/edit).
**The rules are not changing in this pass.** Verification is the one exception, and it is a
deletion rather than a rule change (§3).

- **Quarterly seasons.** Jan–Mar = *Winter*, Apr–Jun = *Spring*, Jul–Sep = *Summer*,
  Oct–Dec = *Fall*. A season ends at 23:59:59 on the last day of its final month, UTC−5.
- **1 hour 45 minutes** of play per session. **The window governs when a song may *start*, not
  when it must finish** — a chart you started before the buzzer counts in full, and closing a session
  on Gargoyle FULL SONG is the standard play (owner, 2026-08-11). `CanAdd` implements the stricter
  rule today; see §2.9.
- **Singles and Doubles are separate boards, and nothing in MoM ever compares them** (D15).
  As of this pass, Phoenix and Phoenix 2 are separate too.
- **A player may run a season as many times as they like** (D16). Each is its own session
  and boards rank sessions, not players — three good sessions may hold the top three places.
  In practice almost nobody runs twice; the session is brutal.
- **Repeats are banned** — but the identity is song + chart type + *level*. The same song at a
  different difficulty is a different chart and is legal.
- **Ties never happen in practice**; when they do, **earliest submission wins**.
- **Phoenix 1 scoring is frozen. No part of it changes in this pass** (owner, 2026-08-11, "in any
  way shape or form"). What the ladder does today is the rule of record: a score below 750,000 is
  worth zero, the 750,000–824,999 A band earns interpolated partial credit rising to nearly half
  an A+, and a zero-point play is an ordinary entry — it counts toward chart count and blocks a
  repeat of that chart. The external rules doc's "A and below are worth zero" and its non-play
  clause describe neither; the code stands and the divergence is documented in §2.8, not fixed.
- **Phoenix 2: the "A or below" rule is unsettled** and will be decided after the scoring
  experiment (§10). P2 ships on stock P2 pumbility, where a passing F pays nothing at all and the
  worst grade that still pays is a D.
- Song length scales value, with a 2-minute baseline.
- Charts 22+ carry an exponential bonus (Phoenix 1 only — see §5).
- **Stage break is deliberately outside the algorithm, on both mixes.** Passing and scoring are
  different concepts and MoM scores the latter — a failed SS was not a mash. A broken play is
  worth exactly what its score is worth. `StageBreakModifier` stays `1.0`; see §9.2 for the
  inheritance hazard this creates for PUMBILITY2+.

---

## 2. Why now — eight defects, all confirmed against production-synced data

### 2.1 The runaway — **fixed in Slice 0**; the junk rows are still owner-run cleanup

[`MarchOfMurlocsHandler.cs:65`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs:65)
maps "month the last season ended" to "month the next season ends" in quarters:

```
{12,1,2} → 3    {3,4,5} → 6    {6,7,8} → 9    {9,10,11} → 12
```

The switch is written longhand and **`6 => 9` was never typed**, so month 6 falls to `_ => 3`.
Spring 2026 ended June 30. On July 1 the cycle computed March 31 2026 — an end date three
months in the past — and then:

- `CycleMoM`'s idempotency guard only returns early when a **future**-dated MoM exists. Every
  row is past-dated, so it never trips.
- `oldEnd` is `MAX(EndDate)`, still June 30, so it recomputes the same wrong answer forever.
- `TryScheduleMoM` sees the newest MoM expired and republishes `CycleMoM`.

Once per day at 11:00 UTC since 2026-07-01. Measured blast radius:

| | |
|---|---|
| MoM tournament rows | 79 (8 real, **71 junk**) |
| Orphan `TournamentChartLevel` rows | **141,632** |
| Player data on junk rows | **zero** sessions, registrations, roles, photos |

The regression test added with the previous runaway fix covers March→June only, which is why
this hole survived.

**A second, latent variant:** `newEndDate` was built from `_dateTime.Now.Year` with the computed
month, so any cycle that ran *late* — missed season, downtime, manual trigger while behind —
minted another past-dated season and restarted the loop. Fixing only the switch arm would have
left this in place.

**Slice 0 fixed both**: the quarter map is arithmetic that cannot omit a month, and the season
advances a quarter at a time until it lies ahead of now, so a cycle five quarters behind lands
on the current quarter and creates one season rather than a backlog. §6 then removes the whole
*class* by making a duplicate season impossible in the schema.

### 2.2 MoM has never appeared in the nav — **fixed in Slice 0**

[`MarchOfMurlocsHandler.cs:114`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs:114)
constructs each season with `isHighlighted: false`, and `ShellNav` renders its event links from
`Model.HighlightedEvents`. **Every MoM row in the database has `IsHighlighted = 0`**, including
Winter 2025 and Spring 2026. Only the static "March of Murlocs" menu link has ever existed.

This is why Spring 2026 ran a full quarter and received **zero submissions**, and why 39 days of
garbage went unnoticed.

### 2.3 Phoenix 2 sessions are graded on Phoenix 1 cutoffs

`ScoringConfiguration.Mix` defaults to `Phoenix` and MoM never sets it, so
`score.LetterGradeFor(Mix)` uses P1 floors for a P2 session. The floors moved below AAA, so P2
sessions are systematically **over**-scored — and because MoM runs
`ContinuousLetterGradeScale = true`, the interpolation is wrong across the whole band, not just
at boundaries.

### 2.4 The leaderboard ignores mix

[`EFTournamentRepository.cs:289`](../../ScoreTracker/ScoreTracker.EventCompetition/Infrastructure/EFTournamentRepository.cs:289)
selects every session for a tournament and ranks them together. P1 and P2 sessions currently
share one board — combined with 2.3, P2 players hold an unearned advantage.

### 2.5 Play timestamps are parsed and then discarded — **resolved in Slice 3, by subtraction**

`PiuGameGetRecentScoresResult.RecordedAt` is parsed to the second, with the site's own UTC
offset (`PiuGameApi.ParseRecordedAt`). By the time Slice 3 landed, the main import already kept
it the whole way down: best attempts store it as `RecordedDate`, and every play the recent
window observes — stage breaks included — is journaled with its real time
(`RecordObservedPlaysCommand.PlayedAt`). The one path still dropping the stamp was a
MoM-only credential overload, `OfficialSiteClient.GetRecentScores(mix, username, password)`,
whose sole caller was the record page's import dialog.

**Slice 3 deleted that path instead of fixing it** (owner, 2026-08-24): MoM's import reads the
score journal through ScoreLedger's published `GetPlayerJournalQuery`, which serves
`OccurredAt`, judgements and stage breaks for free — one scraper, one journal, MoM is a
reader. The `GetOfficialRecentScoresQuery` contract and the credential overload are gone, and
with them the last surface outside Import Scores that asked for a piugame password.

### 2.6 The 1h45m rule is unenforceable

`TournamentSession.CanAdd` checks `TotalPlayTime + duration > MaxTime` — the **sum of song
durations**, not elapsed wall clock. Rest time is *modelled* as `MaxTime − TotalPlayTime`, i.e.
the system assumes the window was filled. A player could spread 30 charts across four hours and
be accepted. 2.5 is what makes this checkable for the first time.

### 2.7 Smaller things, fix in passing

- [`RecordTournamentSession.razor:227`](../../ScoreTracker/ScoreTracker/Pages/Competition/RecordTournamentSession.razor:227):
  `ValueChanged="v=>_editScore=v\n    =v"` — a wrapped-line typo that compiles.
- `StaminaTournament.razor` carries two hex literals (`#00FFFF`, `#FFFFFF`) in ApexChart series
  — a `UiColorTokenTests` violation waiting to be noticed.
- `StaminaTournament` and `RecordTournamentSession` inject `ITournamentRepository` **directly**,
  bypassing `IMediator`. The one hard architecture violation in the feature.
- `StaminaTournament.ShowUserCharts` divides by `Entries.Count() - 1`; a one-chart session throws.
- `MarchOfMurlocs.razor` computes `_hasQualifiers` in an N+1 mediator loop and never renders it;
  `_isLoading` is declared and never set.
- `RecordTournamentSession.DifficultyBubblePath` is dead — the `DifficultyBubble` component
  replaced it.

### 2.8 The anti-mash zero does not actually zero anything — found 2026-08-10, **not being fixed**

> **Decided 2026-08-11: Phoenix 1 MoM scoring does not change, in any way, in this pass.** This
> section stays because the behavior below is real and someone will rediscover it; it is a record
> of how the ladder prices the A band, not a work item. Do not open it as one. The Phoenix 2 "A or
> below" question (§10.1) is a separate decision about a mix with zero recorded sessions and is
> still open.

**The continuous grade scale interpolates straight through the zeroed rungs, so "A and below is
worth zero" is only true at or below 750,000.**

`ContinuousLetterGradeScale = true` means a score between two rungs takes a modifier between the
two rungs' values. PUMBILITY+ sets A to `0` and A+ to `0.50`, and the A band runs 750,000–824,999
— so the interpolation ramps a *zeroed* grade from 0 up to very nearly 0.5 across that band:

| Score | Grade | Modifier |
|---|---|---|
| 750,000 | A | 0.000 |
| 780,000 | A | 0.200 |
| 812,400 | A | 0.416 |
| 824,999 | A | **0.500** |

The consequence is exactly the mash the grade rewrite exists to prevent. Priced under Winter
2025's own frozen config:

- a level 26 mashed to **824,999** over 112 seconds pays **1,031**
- a level 22 played cleanly to a **AAA** over 120 seconds pays **930**

So barely scraping an A on a 26 beats a clean AAA on a 22, which is the opposite of what §5's
argument for the Phoenix 1 kicker assumes. §5's table carries the real cliff, < 750,000.

**Partial credit for a scraped A is the rule.** The owner settled it on 2026-08-11: the ladder has
priced the A band this way since the grade rewrite landed, several seasons have been run and ranked
under it, and re-pricing a live competitive ladder to match a sentence in a rules doc is not worth
what it costs. §1 is now written to match the code rather than the other way round.

Two things follow for anyone tempted to revisit it:

- **It was never one value.** Raising the floor to 825,000 (`if (score < MinimumScore) return 0`)
  would manufacture a population of zero-point plays, and §1's non-play handling **does not
  exist**: `EFTournamentRepository` sets `ChartsPlayed = session.Entries.Count()` with no score
  filter, and `TournamentSession.CanAdd`'s repeat check scans every entry regardless of what it
  scored. So a zero-point play would inflate chart count and stay blocked from the retry the rules
  promise. That work lives in Slice 4b's submit logic if it is ever wanted; it is not a knob.
- **The edit site would be `MarchOfMurlocsHandler`, never `CreateScoring()`** — see §8 for why the
  shared factory is the wrong place, and §9.5 for the same trap in its original form.

### 2.9 The window check refuses a legal closing song — found 2026-08-11

`TournamentSession.CanAdd` tests `TotalPlayTime + duration > MaxTime`, which requires the final
chart to *finish* inside the window. The rule is that it only has to **start** inside it (§1), so
the check is too strict by up to the length of one song — and it bites exactly the play people
actually make, closing on a six-minute full song.

The correct test is simpler than the current one, and the candidate's own length does not enter
into it:

```
may add  ⟺  sum of every duration already entered  <  MaxTime
```

Every chart is provisionally the last, so this is the only condition at entry time; adding another
after it is what makes the previous one's full length binding, and that is the same check one
chart later. Consequences worth writing down:

- **`TotalPlayTime` can now exceed `MaxTime`**, so derived rest must floor at zero rather than go
  negative. A session that overhangs has *no* rest by construction — it filled the window.
- **The import's hard block moves too**: it is the song time *excluding the final play* that
  cannot exceed the window.
- **Nothing in the existing data changes.** The tightest real session is esi's 42-chart session with
  13:43 of rest, so no stored row is anywhere near the cap and no historical total re-scores.
  This has never bound — it would have bitten the first person to try the Gargoyle close.

---

## 3. Locked decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | **MoM owns its own tables.** No dependence on `scores.Tournament`. | That table carries qualifiers, roles, co-op teams, randomizer micro-tournaments and MoM. Qualifiers leaves next; the table then dies. |
| D2 | **A filtered `UNIQUE(Year, Quarter)` is the real runaway fix.** | Makes a duplicate season *impossible* rather than merely fixed. The cycle becomes stateless — "does the current quarter's season exist?" — which also removes the late-catch-up variant. |
| D3 | **Four boards per season**: Phoenix·Singles, Phoenix·Doubles, Phoenix 2·Singles, Phoenix 2·Doubles. | Phoenix 1 is not dying, it is going offline-only. `MoMBoard` keys on MixId, so another mix is a row, not a migration. |
| D4 | **Phoenix 1 is manual-entry**; Phoenix 2 gets the import flow. | P1 loses its import path soon. |
| D5 | **Verification is deleted** — no photos, no approval queue, no admin notification, no `InPerson`/`Remote` distinction. | Nobody is verifying, and nobody has a reason to cheat yet. Zero photos have *ever* been attached to a MoM session. |
| D6 | **Videos survive as a showcase**, not a validation mechanism. YouTube links embed. | 29 of 62 historical sessions carry one; people want to flex. |
| D7 | **Migrate all legacy seasons.** Copy, never move. | 62 sessions of history. Copying keeps a botched migration recoverable and leaves `PhotoVerification` without orphans. |
| D8 | **Delta-only chart-level snapshots.** | Measured −60%. The equivalence is exact, not approximate (§9.3). |
| D9 | **`MoM*` naming**, not a generic "stamina ladder". | MoM is always going to be the only one. |
| D10 | **Hard block** on duration overflow; **soft warning** on timestamp span. Duration overflow means *everything before the last chart* exceeds the window — the last one may overhang (§1, §2.9). | A session that *cannot* fit is invalid; a session that *looks* stitched together is a judgement call. |
| D11 | **PUMBILITY+ tracks the mix's own rating system.** | See §5. P1 needed heavy correction; P2 does not. |
| D12 | **Phoenix 2 ships admin-gated.** | Scoring gets min/maxed in a dedicated session before players see it. |
| D13 | **Empty ended seasons are pruned** when the next season is created. | Owner call. Keeps the table honest without a cron. |
| D14 | **The targets screen is two tenses of one breakdown** — a Planner before you play and a Session Breakdown after — both inside MoM. | Self-encapsulated; it is a MoM page, not a pumbility page. Owner 2026-08-10: people enter once a season and the event is a competition against yourself, so *understanding your own session* beats ranking against strangers. |
| D15 | **Nothing in MoM ever compares Singles to Doubles.** | Owner 2026-08-10. Fundamentally different approaches and scoring results. Rules out combined totals, cross-type averages, and any breakdown axis spanning both — an "average difficulty 24.1" means nothing unless you know the type. §11.6. |
| D16 | **A player may run a season many times; boards rank sessions, not players.** | Owner 2026-08-10. Three good sessions may hold the top three places — "if someone does 3 of these in 3 months, they deserve being top 3". Kills `UNIQUE (BoardId, UserId)` and makes recorded date a load-bearing board column. |
| D17 | **Draft → published → frozen.** A draft is visible only to its owner and never reaches a board; publishing freezes the session; a correction is delete-and-resubmit. | Owner 2026-08-10. Thirty hand-typed charts must survive a stray navigation, and a published session is a record of a thing that happened. Delete must therefore exist, or a typo is permanent. |
| D18 | **Recorded date is the moment of publication, and is not editable.** | Owner 2026-08-10. Manual entry has nothing to derive a play time from until timestamps land (Slice 3), and a player-supplied date is a field to get wrong. Resubmitting after a delete moves the date — which only affects tie-breaks, which never happen. |
| D19 | **MoM declares its mixes in `MixCapabilities`**, like every other section — Phoenix 1 and 2, never a legacy mix. | Landed on main 2026-08-10: the desktop menu and the phone More sheet are two renderings of one rule set, and two copies of a rule is how the recording form lost its legacy branch. |
| D20 | **A cross-season comparison re-prices the old session under the new season's whole frozen config** — snapshot *and* scoring tables — and reports what moved underneath separately from what the player earned. | Owner 2026-08-10. A raw season-over-season delta silently mixes "I got better" with "the game changed". Measured between MoM 2 and Winter 2025, both moved: 10 charts re-rated **and** every level-23+ rating raised while the grade table was cut. §11.3. |
| D21 | **The session list carries all three sanctioned densities**, with sort behind a **Sort by** popover in Comfortable and Compact, and on the column headers in Table. | Owner 2026-08-10. UX rule 5's three modes, no fourth. A visible button group spent toolbar width on a control that is used once; Table already sorts from its headers, so a popover there would be a second control for one job. §11.3. |
| D22 | **The word is "session", everywhere.** Not run, not attempt, not entry. | Owner, 2026-08-10 and enforced again 2026-08-11 after "run" crept back into every mock. One ubiquitous term for the thing a player records, publishes, breaks down and plans — page copy, headings, Discord cards, localization keys and the `MoMSession` table alike. "Run" reads fine in isolation, which is exactly why it keeps returning. |

### Explicitly rejected

- **Building PUMBILITY2+ on PUMBILITY+'s Phoenix 1 curve.** Rejected 2026-08-09: the quadratic
  base and the 22+ kicker exist to solve a *Phoenix 1* problem (see §5).
- **Lazy season creation** (materialise on first visit). The chart-level snapshot must freeze at
  season start to be fair, and `IChartScoringLevelRepository` is a current-state projection with
  no as-of query — a lazily-taken snapshot would give early and late submitters different balance.
- **A nullable / "combined" `ChartType` on a board.** Not needed: every legacy season maps to a
  single chart type (§7).

---

## 4. The balancing algorithm (Phoenix 1)

Previously undocumented — it existed only in code. **`CalculationType.Avalanche` is a selectable
formula in the enum and MoM does not use it.** MoM runs `CalculationType.Default` (all modifiers
multiplied) over four layers.

**1 — the `PumbilityPlus` base curve.** `BaseRating = 100 + 5(L−10)(L−9)` for L ≥ 10
([`DifficultyLevel.cs:155`](../../ScoreTracker/ScoreTracker.SharedKernel/ValueTypes/DifficultyLevel.cs:155));
levels 1–9 are 0 by default and hand-set to 10…90 in `CreateScoring()`.

**2 — grade modifiers, rewritten for stamina.** `ContinuousLetterGradeScale = true` interpolates
between rungs.

| Grade | Default | PUMBILITY+ |
|---|---|---|
| AAA | 1.10 | **1.00** |
| AA+ | 1.05 | **0.90** |
| AA | 1.00 | **0.75** |
| A+ | 0.90 | **0.50** |
| A and below | 0.4–0.8 | **0** |
| AAA+ … SSS+ | 1.15 … 1.50 | *unchanged* |
| Perfect Game | 1.50 | **1.60** |

**3 — the 22+ kicker**, applied in the MoM handler on top of the base:

| Level | 22 | 23 | 24 | 25 | 26 | 27 | 28 | 29 |
|---|---|---|---|---|---|---|---|---|
| Base | 880 | 1,010 | 1,150 | 1,300 | 1,460 | 1,630 | 1,810 | 2,000 |
| Bonus | +50 | +150 | +300 | +500 | +750 | +1,050 | +1,400 | +1,800 |
| **Effective** | **930** | **1,160** | **1,450** | **1,800** | **2,210** | **2,680** | **3,210** | **3,800** |

**4 — scoring difficulty, frozen per season.** At season creation each chart's effective level is

```
clamp(communityScoringLevel, nominal + 0.5, nominal + 1.5)
```

snapshotted into `TournamentChartLevel`. `GetBaseRating` then interpolates between
`LevelRatings[floor]` and `LevelRatings[ceil]`. The `− .5` offset in that interpolation means
**nominal+0.5 pays exactly the nominal level's rating and nominal+1.5 pays the full next
level's** — so a chart is never worth less than its number, and at most one level more.

Freezing per season is deliberate: mid-season re-rating cannot move the goalposts. **Preserve
this through any rewrite.**

Also set, and easy to miss: `AdjustToTime` is `false` in `CreateScoring()` and set to `true` by
the MoM handler · plates are all `1.0` (no effect) · `MinimumScore` is 0 · `SongTypeModifiers`
all 1.0 · `ChartModifiers` empty · **`StageBreakModifier` is 1.0 — stage break is deliberately
outside the algorithm** (§1), and PUMBILITY+ gets that by composing from a bare
`new ScoringConfiguration()` rather than from `PhoenixPumbilityScoring()` ·
`ChartTypeModifiers[CoOp] = 1.0` in `CreateScoring()` is a **no-op** restating the default, and
CoOp is filtered from the pool anyway.

---

## 5. PUMBILITY2+

**The principle (D11): PUMBILITY+ means *that mix's own pumbility, plus stamina adaptations*.**

Phoenix 1's pumbility rewards level so steeply that mashing high charts beats scoring well, so
PUMBILITY+ had to correct it — that is what the grade rewrite and the 22+ kicker are. **Phoenix 2
already solved scoring-versus-passing**, so its adaptation is nearly empty. Both configs are
then principled; neither is legacy.

```
PUMBILITY2+ = Phoenix2PumbilityScoring
            + ContinuousLetterGradeScale = true
            + AdjustToTime = true            (set by MoM, never in core — §9.5)
            + StageBreakModifier  = 1.0      (RESET — the base config carries 0.0 — §9.2)
            + LevelRatings[1..9]  = 0        (so CanAdd blocks sub-10 — §9.4)
            + non-matching chart types zeroed
            − no grade rewrite, no 22+ kicker, no PG bonus
```

Phoenix 2's base is `130 + 5L + 5·max(0, L−24)` — near-linear where Phoenix 1's is quadratic.

| Level | 10 | 20 | 22 | 25 | 26 | 29 |
|---|---|---|---|---|---|---|
| PUMBILITY+ (with kicker) | 100 | 650 | 930 | 1,800 | 2,210 | 3,800 |
| Phoenix 2 | 180 | 230 | 240 | 260 | 270 | 300 |

**This produces the intended incentive.** The L26/L20 base ratio is 270/230 = 1.174, so climbing
six levels pays whenever `grade(26) > 0.852 × grade(20)`:

| Play | vs a clean AAA level 20 |
|---|---|
| 26 at A+ (1.33 on a Single, 1.35 on a Double) | wins — ratio 0.94 / 0.96 |
| 26 at A (1.28) | wins, barely — 0.91 |
| 26 at F | **scores nothing at all** — P2 excludes an F outright, passed or not |

Phoenix 2 says *go as hard as you can still play competently*. Phoenix 1 with the kicker says
*go as hard as you can survive at all* — even A+ at 0.50 wins by 1.7×.

**Grade floors are identical from AAA (950,000) upward on both mixes.** The tables diverge only
below 950,000, which bounds the behavioural change precisely:

| | Phoenix 1 | Phoenix 2 |
|---|---|---|
| Zero cliff | < 750,000 *(settled — §2.8)* | **< 500,000** — an F scores nothing on P2 |
| 0.50 → 1.00 ramp | 825k → 950k | *n/a — P2 uses its own 1.00 → 1.50* |

Implementation: keep the parameterless `ScoringConfiguration.PumbilityPlus` **exactly as it is**
and add a mix-aware factory beside it that only MoM calls (§9.5). Write P1's corrections as an
explicitly-labelled *Phoenix 1 anti-mash block* so the next reader does not carry them forward
by default.

---

## 6. Target schema

Five tables, replacing dependence on fifteen. Registered through EventCompetition's
`IDbModelContribution`; every table gets a row in
[DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md).

```
scores.MoMSeason
  Id          uniqueidentifier  PK
  Year        int               NULL   -- NULL for off-grid legacy seasons
  Quarter     tinyint           NULL   -- 1..4
  Name        nvarchar(100)            -- "Winter 2026"
  StartsAt    datetimeoffset
  EndsAt      datetimeoffset
  CreatedAt   datetimeoffset

  CREATE UNIQUE INDEX UX_MoMSeason_Quarter ON scores.MoMSeason (Year, Quarter)
      WHERE Quarter IS NOT NULL;          -- D2: the anti-runaway guarantee

scores.MoMBoard
  Id            uniqueidentifier PK
  SeasonId      FK -> MoMSeason
  MixId         uniqueidentifier
  ChartType     tinyint
  ScoringConfig nvarchar(max)            -- frozen serialized ScoringConfiguration
  UNIQUE (SeasonId, MixId, ChartType)

scores.MoMChartLevel                      -- balance snapshot, DELTA ROWS ONLY
  SeasonId, MixId, ChartId  PK
  Level       float

scores.MoMSession
  Id                uniqueidentifier PK
  BoardId           FK -> MoMBoard
  UserId            uniqueidentifier      -- purge key (§9.7)
  PublishedAt       datetimeoffset NULL   -- NULL = draft (D17); once set it is the
                                          --   recorded date (D18) and the tie-break clock
  TotalScore        int
  ChartsPlayed      int                   -- excludes zero-point plays on P1 (§1)
  RestTime          bigint
  AverageDifficulty float                 -- BALANCED level, not the folder number (§11.6)
  AverageGrade      float
  LowestLevel       tinyint
  HighestLevel      tinyint
  VideoUrl          nvarchar(500) NULL    -- D6: showcase, not validation
  CreatedAt         datetimeoffset
  UpdatedAt         datetimeoffset

  CREATE INDEX IX_MoMSession_Board ON scores.MoMSession (BoardId, TotalScore DESC)
      WHERE PublishedAt IS NOT NULL;      -- the board read; drafts are never on it
  -- Deliberately NO unique on (BoardId, UserId): a player may run a season many times (D16).

scores.MoMSessionChart                    -- normalized; replaces the JSON blob
  SessionId, Ordinal  PK
  ChartId     uniqueidentifier
  Score       int
  Plate       nvarchar(20)
  IsBroken    bit
  SessionScore int
  BonusPoints int
  PlayedAt    datetimeoffset NULL         -- §2.5
```

**Everything on `MoMSession` below `PublishedAt` is a derived cache** of its `MoMSessionChart`
rows — recomputed on every save, never edited independently. They exist so a board render and a
Discord card do not have to open thirty chart rows per session; they are not a second truth.

**Why the scoring config is stored, not derived.** MoM's config is fully determined by
(mix, chart type) today, so rebuilding it on read is tempting — but then any tweak to the 22+
table retroactively rescores 2024. Four serialized rows per season is cheap insurance for a
competitive ladder.

**Why `MoMSessionChart` is normalized.** `GetLeaderboardRecords` currently calls `GetSession` per
player, each deserializing JSON and re-fetching charts — a live N+1 on every board render.
Normalized rows also give `PlayedAt` a home.

**Why the snapshot is keyed per (season, mix) rather than per board.** Singles and Doubles charts
are disjoint sets, so the row count is identical either way, but one key is simpler and a third
chart type would then cost nothing.

---

## 7. Migration

**Copy, never move (D7).** The old `Tournament` rows stay in place until that table is archived
wholesale per the never-drop-tables standard.

Eight legacy tournament rows collapse into **five seasons and eight boards** — and every one maps
to a single chart type, so no "combined" board is needed. Verified against prod-synced data
2026-08-14: the cleanup ran, Spring 2026 was pruned (an ended season with zero entries, D13), and
Slice 0's catch-up created Summer 2026 on 2026-08-10, correctly highlighted:

| Legacy tournament | Season | Year/Qtr | Board | Sessions |
|---|---|---|---|---|
| March of Murlocs Practice | Practice | NULL | Phoenix · Double | 2 |
| March of Murlocs | March of Murlocs | NULL | Phoenix · Double | 17 |
| MoM 2 – Singles | \ MoM 2 | NULL | Phoenix · Single | 10 |
| MoM 2 – Doubles | / (same dates) | NULL | Phoenix · Double | 17 |
| Winter 2025 – Singles | \ Winter 2025 | 2025 / 1 | Phoenix · Single | 5 |
| Winter 2025 – Doubles | / | | Phoenix · Double | 11 |
| Summer 2026 – Singles | \ Summer 2026 | 2026 / 3 | Phoenix · Single | live |
| Summer 2026 – Doubles | / | | Phoenix · Double | live |

Notes for whoever writes it:

- **The copy is predicate-driven, never this list.** Prod grows a season per quarter, so the
  script selects on `IsMoM = 1` and excludes the junk signature (`EndDate < StartDate`, §9.1)
  rather than naming rows. Singles/Doubles pairs sharing dates collapse into one season; each
  legacy tournament's Guid becomes its board's Guid, which is what keeps every old URL resolving.
- **Quarters are NULL for everything before Winter 2025.** MoM 2 ran June 8 → Aug 8, straddling
  Q2/Q3; the quarterly cadence only starts at Winter 2025.
- **`PublishedAt` = the season's `EndsAt`** for every migrated session (owner, 2026-08-14).
  `NULL` means draft, so all legacy sessions need a value and the old rows carry no timestamp of
  any kind. The choice is arbitrary but deterministic, and it feeds only tie-breaks, which never
  happen in practice.
- **The frozen config is the `Tournament.Configuration` JSON column** (the serialized
  `TournamentConfigurationJsonEntity`); the copy extracts the scoring portion into
  `MoMBoard.ScoringConfig` verbatim, so historical sessions re-price byte-identically.
- **`BonusPoints` exists only in newer `ChartEntries` JSON** (14 of 62 sessions); older entries
  predate the field and coalesce to 0 — the base/bonus split is unrecoverable for them and
  `SessionScore` stays the stored truth.
- The 2023 "March of Murlocs" contains **one stray Singles play** among 566 Doubles. Copy it
  faithfully inside its entry rather than inventing a rule.
- Verification data is not carried across except `VideoUrl` (D6). Zero photos exist to lose —
  the two `PhotoVerification` rows in prod are orphans pointing at deleted tournaments.

---

## 8. Slices

Ordered so that UI decisions can still move the schema — which they did, so **the back half was
re-ordered after Slice 1** (2026-08-11). What changed and why is under the table.

| Slice | Contents | Depends on |
|---|---|---|
| **0 — Stop the bleeding** ✅ *shipped* | Quarter map replaced with arithmetic; season year derived from the previous season with catch-up to the current quarter; seasons created highlighted. `MarchOfMurlocsHandler` only — no schema change. **Still owner-run: `mom-cleanup.sql`, including the empty-season prune.** | — |
| **1 — Settle on mocks** ✅ *done* | UX/UI pass across the six surfaces. Design only, no code. D14–D22 and §11; the six mocks are checked in beside this doc. | 0 |
| **R — The window predicate** ✅ *shipped inside Slice 2* | §2.9 only. One condition, the rest-time floor it implies, and regression tests. No schema, no UI, no scoring change, no dependency on anything below. **§2.8 is not in R and is not being fixed** — Phoenix 1 MoM scoring is frozen (owner, 2026-08-11). Folded into Slice 2's PR rather than shipped alone (owner, 2026-08-14). | — |
| **2 — Own the data** ✅ | Five tables, model contribution, purge manifest, repository, migrate the seasons, cycle rewritten onto `MoMSeason`. **"Repointed" means the repository is swapped behind the old pages, not that they are rebuilt** — they are deleted in Slice 4 either way, and the point of the swap is to run the new tables under real traffic before the UI depends on them. **D5 (verification removal) rides in this slice** (owner, 2026-08-14): `MoMSession` deliberately has no home for approval state, and dual-storage plumbing for a feature with zero photos ever attached would be built only to be deleted in 4a. Slice R rides here too. | 1 |
| **3 — Timestamps** ✅ *was Slice 5; reshaped 2026-08-24* | §2.5, resolved by subtraction: the journal already stores every observed play's `OccurredAt` (stage breaks included), so MoM's import reads ScoreLedger's `GetPlayerJournalQuery` and the credential-based `GetOfficialRecentScoresQuery` chain is deleted outright. The interim record page repoints onto the journal — functional, not pretty; 4a deletes it. Touches none of MoM's tables. | — |
| **4a — Read surfaces** | Season, Session Breakdown, Past seasons dialog. The Stamina→MoM rename, the old pages deleted. (Verification removal moved up into Slice 2.) | 2 |
| **4b — Write surfaces** | Submit — draft/publish lifecycle, minimal-click entry, the import with gap detection — and the Planner with named saved sets and CSV. | 2, 3 |
| **4c — Discord card** | The fourth `DiscordFeedKind`, the card, `/piu feed mom`. | 2 |
| **5 — Per-mix boards** *was Slice 3* | Four boards live, mix-correct grading and snapshots, PUMBILITY2+, `MixCapabilities` entry (D19), Phoenix 2 ungated. | 2, and the scoring session |
| **separate session** | Phoenix 2 scoring min/max (≈6 rounds, owner estimate). | — |

**Why the back half moved.**

- **Per-mix boards went last because nothing they fix is live.** All 62 MoM sessions ever recorded
  are Phoenix; **zero Phoenix 2 sessions exist**. So §2.3 and §2.4 have never mis-scored anything —
  they are latent, not active. And since D12 ships Phoenix 2 admin-gated until the scoring session,
  building it earlier is speculative work nobody can see or validate.
- **Timestamps moved ahead of the UX build** so the import is built once. The Submit mock is drawn
  against gap detection (§11.4); with timestamps still last, Slice 4 would build the manual
  first/last range picker and then replace it.
- **The UX build split three ways** because it had grown to six surfaces plus a rename plus a
  lifecycle plus a feed. Read, write and Discord are independently shippable against the same
  tables, and 4a is what finally deletes the old pages.
- **R came out of band** because it belongs to no slice: a live defect found while drawing the
  mocks, depending on nothing. It changes no score. §2.9 is eligibility — which charts a session
  may contain — and prices nothing differently; no stored session re-scores, and the tightest real
  session has 13:43 of rest, so nothing recorded is near the cap.

  **R was briefly written as two fixes and is now one.** §2.8 was bundled with it as a "rules call
  the owner may want now"; the owner's answer on 2026-08-11 was that Phoenix 1 MoM scoring does not
  change in any way, so §2.8 is a documented behavior, not a work item. If a future pass ever
  reopens it, two things that were wrong the first time: it is **not one value** (§2.8), and its
  edit site is `MarchOfMurlocsHandler`, **never `CreateScoring()`** — the handler mutates a fresh
  `ScoringConfiguration.PumbilityPlus` per board, which is why `AdjustToTime` is already overridden
  there, whereas the shared factory also backs `PlayerRatingSaga`'s stored `PumbilityPlus` player
  stat and the public v1 API's `PumbilityPlus` column. Neither is frozen; both would move silently.
  Same trap as §9.5.

`mom-cleanup.sql` is dry-run by default, has been tested end to end, and refuses to delete any
season carrying player data.

---

## 9. Traps

### 9.1 `IsMoM = 1` is load-bearing in the cleanup

`BITE 7 - Co-Op` is a **real** tournament that also has `EndDate < StartDate`. Never loosen the
cleanup predicate to inverted-dates alone.

### 9.2 PUMBILITY2+ inherits a stage-break rule MoM does not want

**Reset `StageBreakModifier` to `1.0` when composing PUMBILITY2+.**

Both official pumbility configs — `PhoenixPumbilityScoring` *and* `Phoenix2PumbilityScoring` —
set `StageBreakModifier = 0.0`, which is correct for them: piugame does not count broken plays
in the pumbility pool. MoM's rule is the opposite and deliberate (§1): passing and scoring are
different concepts, a failed SS was not a mash, and a broken play is worth whatever its score is
worth.

Phoenix 1 never hit this because `CreateScoring()` composes from a bare
`new ScoringConfiguration()` (default `1.0`), not from the official config. **PUMBILITY2+ does
compose from the official config** — that is the whole point of D11 — so it inherits `0.0` and
will silently start zeroing broken plays unless the reset is explicit.

Worth a test: a broken play and a passed play at the same score must score identically on both
mixes.

### 9.3 Delta-only snapshots are an exact equivalence, not an approximation

Do not "fix" the missing rows back in. With an override of `L + 0.5`, `GetBaseRating` computes

```
LevelRatings[L] + (LevelRatings[L+1] − LevelRatings[L]) × (L + 0.5 − 0.5 − L)  =  LevelRatings[L]
```

which is byte-identical to the no-entry fallback path. Measured on Spring 2026: 4,425 rows →
1,785 real deltas.

Note also that on Phoenix 2 the snapshot shifts a chart by at most one level, worth ~2% on a
near-linear curve versus ~15% at L22 on Phoenix 1. `MoMChartLevel` is effectively a Phoenix 1
table going forward.

### 9.4 Sub-10 charts are addable-but-worthless on Phoenix 2

P2's zero for `level < 10` is a hard `return 0` inside `GetScore`, **not** in the level table, so
`GetScorelessScore` still returns `Phoenix2BaseRating(5) = 155`. `TournamentSession.CanAdd` gates
on `GetScorelessScore(chart) == 0`, so the picker will happily offer a level 5 that scores
nothing. Fix by setting `LevelRatings[1..9] = 0` in the P2 MoM config so `CanAdd` blocks them
itself.

### 9.5 `AdjustToTime` must stay `false` in core

`ScoringConfiguration.PumbilityPlus` has three consumers and only MoM wants time-adjustment:

- [`MarchOfMurlocsHandler.cs:97`](../../ScoreTracker/ScoreTracker.EventCompetition/Application/MarchOfMurlocsHandler.cs:97) — MoM, sets it true locally
- [`PlayerRatingSaga.cs:179`](../../ScoreTracker/ScoreTracker.PlayerProgress/Application/PlayerRatingSaga.cs:179) — writes the **stored** `PhoenixRecordStats.PumbilityPlus`
- [`PhoenixScoresController.cs:160`](../../ScoreTracker/ScoreTracker/Controllers/Api/PhoenixScoresController.cs:160) — exposes `PumbilityPlus` on the **public v1 API**

Hoisting it into the factory would silently re-value every stored stat and change numbers
partner tools consume. The contract test pins the wire *shape*, not the values, so nothing would
catch it.

### 9.5b A frozen board config drops Phoenix 2's Singles pricing

**Resolve the config for the board's chart type *before* freezing it.**

`ScoringConfiguration` gained `SinglesLetterGradeModifiers` / `SinglesPlateModifiers` on
2026-08-10, because Phoenix 2 prices some grades and plates differently on Singles than on
Doubles. Every read inside the formula goes through `LetterGradeModifierFor` / `PlateModifierFor`,
so the split is honoured everywhere in memory.

It is *not* honoured through persistence. `TournamentConfigurationJsonEntity` maps only the shared
tables, by design — it is what lets a configuration written before the split still deserialize and
score identically. But §6 stores a **frozen serialized config per board**, so a Phoenix 2 · Singles
board would freeze a config with its Singles pricing stripped and quietly score as a Double.

The fix is better than widening the DTO: **every MoM board is exactly one chart type** (D3), so at
freeze time flatten that type's overrides into the shared tables — Singles values on a Singles
board, shared values on a Doubles board. The stored config comes out type-free by construction and
the persistence gap cannot bite. A board carrying a two-type table would be meaningless anyway.

### 9.6 Never cross-wire the official mirror with the tournament formula

`Phoenix2PumbilityScoring` mirrors piugame's own number and must stay discrete-grade /
additive-plate — `/Pumbility` and the reconciliation probes compare it against the live site.
PUMBILITY2+ is a tournament formula. Two configs, never merged. Comment both.

### 9.7 Ratchets that will go red if missed

- `MoMSession` must join the vertical's `UserOwned` purge manifest with `UserId` as the purge key,
  or `AccountPurgeCoverageTests` fails.
- The `IDbModelContribution` must be listed in `VerticalModelContributions.All()`, or scaffolded
  migrations silently drop every MoM table.
- New localization keys land in **all nine locales**, inserted in alphabetical position — and
  `en-ZW` uses only the Murloc alphabet.
- No color literals in `Pages/`/`Components/`; the two ApexChart hex values in
  `StaminaTournament.razor` need tokens.

### 9.8 EventCompetition becomes the MoM vertical

Once qualifiers moves out, what remains is roles, invites, registrations and co-op teams (all
qualifier-side) plus randomizer settings (which belong to `Randomizer`). EventCompetition does
not survive the split as a distinct thing — it *becomes* MoM. Land the tables inside it now and
do any assembly rename after qualifiers leaves, so a mechanical rename never overlaps a schema
change.

---

## 10. Open

1. **Phoenix 2's "A or below" rule** — deferred to the scoring session. `MinimumScore` already
   exists (`if (score < MinimumScore) return 0`) and is a single knob that adds a floor without
   touching grade multipliers.
2. **Perfect Game on Phoenix 2** — left out this pass. Consequence to be aware of: with
   `PgLetterGradeModifier` equal to SSS+'s 1.50, continuous interpolation from 995,000 to
   1,000,000 is **flat**, so the top 5,000 points of score are worth nothing. One value to change
   once the targets screen shows how it plays.
*(Resolved 2026-08-10: the targets-screen shape — it is the Planner and the Session Breakdown,
two tenses of the four-lever model, D14 and §11. **One draft at a time**, whose only two exits are
Discard and Publish, so a draft is never a workspace. And a **deleted session's Discord card is
left to 404** — the card cannot be unsent, but the case is rare enough not to buy a tombstone
page for.)*

---

## 11. The surfaces

Settled with the owner over three workshop rounds, 2026-08-09/10. Mocks build from this section.

### 11.1 The six surfaces

| Surface | Route | Leads with |
|---|---|---|
| **Season** | `/MarchOfMurlocs` | your best session and how many you have played, then the Doubles board, then Singles |
| **Past Seasons** | *a dialog, no route* | a season picker over whatever page you are on (§11.8) |
| **Session Breakdown** | `/MarchOfMurlocs/Session/{id}` | one session's four numbers, its charts, its timeline |
| **Submit** | `/MarchOfMurlocs/Session/{id}/Edit` | draft editing; publishing freezes the session |
| **Planner** | `/MarchOfMurlocs/Planner` | a projected session in the same four numbers |

The old pages are **retired, not ported** — `MarchOfMurlocs.razor` (a directory of eight
tournaments with the live one buried among them), `StaminaTournament.razor`,
`RecordTournamentSession.razor` and `SessionBuilder.razor` all go. The directory shape is what let
Spring 2026 run a full quarter to zero submissions alongside 39 days of garbage.

### 11.2 The Season page

The live season *is* the landing page; there is no tournament directory. Order is: your standing,
then Doubles, then Singles — Doubles first because that is where the event's history lives.

**Which boards you see follows the mix you are on**, exactly as weekly boards now do: a Phoenix 1
player sees Phoenix 1's two boards and never learns Phoenix 2's exist. That plus D19 is the whole
of the four-board question — four boards exist, two are ever on screen, and D15 forbids comparing
even those two.

**Your standing** is your best session and a count — *"3rd — 47,300 · you have played this season 3
times"* — with each session reachable. If you have not, this is the Submit call to action instead.

**Phoenix 2 before the scoring session** (D12) shows the board explaining itself rather than
hiding: *"Phoenix 2 scoring is still being tuned."* That is the current nav philosophy — a link is
hidden only when the thing it points at does not exist for that mix, and everything else shows up
and explains itself on arrival.

**One board at a time, with a Doubles / Singles button group** in the tier lists' own idiom —
joined buttons, active filled, inactive outlined (owner, 2026-08-10). Doubles is the default.
Stacking both boards made the page a scroll; a group makes the pair legible without ever putting
their numbers side by side, which D15 forbids anyway. The two standing cards double as the
switcher, so the card that says you have not played Singles is also how you get to that board.

**Boards rank sessions** (D16), pure score order, so one player may hold several places. Recorded
date is what tells two of their sessions apart, which is why D18 pins it. Rows wear the standard
board skin — `.olb-rank-card`, no card wrapper, no density toggle. The Official Leaderboards
rankings board is the named golden standard for every board on the site; MoM is not an exception.

**A row carries the player, not just a name**: avatar, country flag and name, the `UserLabel`
anatomy with an avatar in front of it (`CommunityLeaderboard` is the precedent, at 26–30px).

**Relationship highlighting is the sitewide utility set, not a MoM invention** — `.is-me`,
`.is-rival`, `.is-community`, `.is-both` from `site.css`: one geometry (a tint plus a bar down
each edge), colour the only variable, precedence **you → both → rival → community**. A legend sits
above the board and every row repeats the relationship in its `title` and `aria-label`, because
the fill is otherwise colour alone.

**Static SSR.** The boards are real crawlable HTML with no circuit — a MoM season board is exactly
the content the SSR migration exists for, and a past season never changes, so it caches perfectly.
The submit affordance is a link, not an island.

### 11.3 Session Breakdown

Its own page, reachable from any board row. Keeps what the old expandable row did well — the chart
cards and the points-per-second timeline — at a much higher visual standard, and adds the four
levers (§11.6) as the thing you actually read first.

Carries **compare**, in two modes. Against another session on **this board** — same board by
construction, so it cannot violate D15, and the most motivating version of the model: *four more
charts, six fewer minutes of downtime, average grade held.* And against **your own past seasons**,
walking the board lineage — same mix, same chart type, back through time. A different chart type
or a different mix is a different sport and is never offered.

Both modes also list **the charts the two sessions have in common**, worst gap first. A total says who
won; this says where. Against yimmythe42, 12 of 김재현's charts overlap and Gargoyle alone cost him
1,892 points — on a board he won by 1,994.

#### The re-rating split (D20)

A season comparison has a confound a same-board comparison does not: **each season freezes its own
chart balance *and* its own scoring tables** at creation, so the same session is worth different totals
in different seasons. A raw delta silently mixes "I got better" with "the game changed."

The fix is a counterfactual. Re-price the old session — *the same charts, the same scores* — under the
new season's whole frozen config, and split what moved:

```
March of Murlocs 2 · Aug 2024                          44,139
    charts re-rated by the community                              +810
    scoring tables re-cut                                       +2,449
  the same session, scored as Winter 2025                  47,923   +3,784  total
Winter 2025 · Feb 2025                                 59,319  +11,396  you
```

Those are 김재현's real numbers, computed from both stored configs. **Two different things moved,
and only one of them is what anyone would have guessed:**

- **10 of his 32 charts were re-rated**, all upward — Galaxy Collapse and Dignity each a full level.
- **The scoring tables were re-cut too.** Every level-23-and-up rating rose (L23 1,110 → 1,160;
  L26 1,710 → 2,210) while the grade table tightened (AAA 1.10 → 1.00, AA+ 1.00 → 0.90, A 0.25 → 0).
  That is the Phoenix-1 anti-mash rewrite of §4 landing *between* the two seasons — and it is worth
  three times the chart re-ratings.

Without the split he reads a +15,180 improvement, and 3,784 of it was the game moving under him.

Two implementation notes. The parts **multiply**, so they sum to less than the total — say so
rather than printing three numbers that appear not to add up. And label the middle line as a
re-pricing, never as a score he achieved: he might not have picked those charts under the new
balance.

This needs no new data — both configs and both snapshots are already stored per season. It is the
§4 arithmetic run four times per chart (old/new snapshot × old/new tables).

#### Densities (D21)

The density control is **three bare icon buttons**, exactly as `/Pumbility` renders it — no
segmented chrome, the active one carrying the mix primary, the name in the tooltip and the
aria-label (`ViewComfy`, `GridView`, `TableRows`).

Comfortable is a card per chart — jacket, grade, plate, score, points, per-second rate — with the
**play affordance on the jacket**. Compact is the tier list's sticker sheet: jacket, difficulty
bubble, the grade in one bottom corner and **one printed value in the other**, styled as
`.tier-chart-card-corner` + `.pmb-corner-gain` — opaque black backdrop, outlined in the mix
primary. **That value is the MoM score, not the PIU score** (owner, 2026-08-10): points are the
currency the page is denominated in, and the grade in the other corner already says how cleanly
the chart was played. When the list is ranked by something the sticker does not print, that value
joins as a second line, so Compact never sorts by a number the reader cannot see. Table wears the pumbility
table's width ladder: points-per-second and length go at 900, the song name at 700 (jacket and
bubble already say which chart it is), and at 500 the numeric score drops so grade art and plate
**stack** rather than shrink.

**The jacket and the play button open the same dialog**; only the play button autoplays the chart
video. That is `ChartDetailsDialog`'s existing contract (`AutoPlay`, as `Pumbility.razor` binds
it), and every density opens it — a sticker tap and a table row click land in the same place.

### 11.4 Submit

Its own page and not a panel on the Season page, for three reasons that agree: thirty charts
entered over several minutes should not live under a board you might scroll away from; the Season
page is static SSR and a form would drag the whole page into a circuit; and a half-entered session
should survive a closed tab. A draft *is* a session (D17), so Submit is the same route family as
the Breakdown and "New session" simply creates a draft.

**Minimising clicks matters more than it looks** — Phoenix 1 goes manual-only in a month, and
manual entry carried the first three seasons before the import route existed. Two things pay for
themselves:

- **The plate selector replaces the Broke checkbox**, in `RecordScoreForm`'s own idiom: one
  dropdown carrying *Broken* and the eight plates, where a plate is a pass and Broken is the
  clean fail with no plate. Plates are worth 1.0 under PUMBILITY+ so they change no Phoenix 1
  score — they are captured because players want to see them, and because Phoenix 2 prices them
  (owner, 2026-08-11, reversing the earlier "hide it on Phoenix 1"). It stays optional, so the
  three-key loop is unaffected, and **Shift+Enter files a run as broken** without reaching for it.
- The picker stays open with focus returned to the chart field, so entry is
  *chart → score → Enter*.

**The 1h45 budget is visible** — a bar filled by song duration with the rest-time remainder
called out. There is no meter today at all; `CanAdd` simply refuses and says nothing.

**The import path stays** (D4 is a month away, not today) and — since Slice 3 — **it reads the
score journal**, not the site: one Import button, no credential fields at all. The 2026-08-10 rule
that this page never stores a password is now vacuous in the strongest way: it never sees one.
Freshness is Import Scores' job — a session missing from the journal means the player runs that
page first (where the remembered credential lives) and comes back; the empty state links there,
so the fallback is a fallback rather than a dead end.

**If Submit ever triggers that import itself rather than linking to it, one standing trap:**
`ScoreImportCompletedEvent` publishes *before* the journal write lands, so reading the journal on
that event reads the pre-import state. Poll the journal or re-query on return — never
consume the event as "journal is ready".

#### The import flow

What the journal returns is an undifferentiated list of recent plays, so the whole problem is
deciding which of them were the session. **Timestamps answer it**: a session is a contiguous
block, and its boundaries are the long gaps either side. The dialog reads the recent plays, splits them wherever
a gap exceeds fifteen minutes, and pre-selects the longest block — everything outside it dims but
stays on screen, with the gap printed between blocks ("3h 42m gap"), so the choice is legible
rather than magic. **Clicking any play moves whichever end is nearer**, which needs no drag
handles and works the same on a phone.

Three checks ride along, and D10 splits them exactly:

- **Song time over 1:45:00 is a hard block** — the Add button disables and says so.
- **Wall-clock span over 1:45:00 is a soft warning**, because it is a judgement call. It names the
  culprit rather than gesturing at it: *"your longest break inside it was 5:20, before GLORIA at
  20:57:17."* Telling someone to trim an end is useless when the break is in the middle, where no
  end reaches it.
- **Unmatched plays are listed by name**, since they have to be added by hand.

Charts already in the draft are skipped on import rather than doubling up — the repeat ban (§1)
applies to the import path as much as the picker.

**Without timestamps there is nothing to detect a gap with** — which is why the pre-Slice-3 page
needed a hand-picked range. Journal plays always carry `OccurredAt` now; the degraded case left
is a play the journal never observed, which is a missing import, not a missing stamp.

### 11.5 Planner

The Planner is the Season's *future* tense: pick a chart type — D15 means it cannot plan across
both — and it solves a run from your record book, reported in the same four numbers, so what you
are chasing and what you posted are described identically. `AutoBuildSessionHandler` is the engine
and already exists: charts in descending points-per-second, taken while the average rest holds up.

**Rest per chart is the only control, and it is the whole feature.** It is what actually decides
how long a run can be, and the plan re-solves as it moves. On 김재현's real Doubles record book:

| Rest per chart | Charts | Projected |
|---|---|---|
| 10s | 54 | 93,077 |
| 35s | 45 | 78,448 |
| 60s | 39 | 67,697 |
| 120s | 29 | 51,161 |

**The plan ends on a closing move**, flagged in the list: once nothing more fits the rest budget
you may still *start* one more (§1), so the run closes on the biggest single chart left. That is
the rule turned into a suggestion.

**Say plainly that it is a ceiling.** It assumes every chart played to your best, which nobody
manages ninety minutes into a stamina session — and the gap is the interesting part, so the page
prints it as a **conversion rate**: 김재현 banked 59,319 against a 78,448 record book, or **76%**.
At 60s rest the plan lands on exactly his 39 charts and projects 67,697, an 88% conversion at
matched volume. Nothing else on the site can tell a player what stamina costs them.

#### The set is a selection, and it leaves with you

**The page opens on Everything with an empty set** (owner, 2026-08-11) — your record book to
browse, filled by hand. Suggesting a set is an offer, not the starting position, and every number
recomputes from what is ticked. One list in the three sanctioned densities — the Session
Breakdown's, verbatim, jackets and all — with a scope toggle leading on **Everything**, so the pool
is the same list rather than a second one.

Ticking in Compact is the sticker itself and in Table a checkbox column. **In Comfortable the
jacket is the video affordance, not the tick** — jacket and play button open the chart dialog, the
button autoplaying, exactly as `/Pumbility` binds `AutoPlay` — so picking lives in the card body
instead, where the two gestures cannot fight over the same pixels.

**Selection reads as an outline, never as dimming** (owner, 2026-08-11): an unpicked chart is not
lesser information, and fading four-fifths of a browsing surface to mark the fifth is the wrong
way round. A picked card takes the mix primary on its border, a picked sticker a ring, a picked
row an inset accent. **Set position is not printed on a tile** either — Compact's second corner
stays the projected grade whether or not the chart is picked, because the grade is what the tile
is telling you and the ordinal is only the CSV's business.

**The rest slider retunes a suggested set live, and never a hand-built one.** A manual tick takes
the set off autopilot; otherwise dragging would silently discard the set someone just chose.

A hand-edited set can stop fitting, so the window check runs on it live and says so — the same
all-but-the-last rule as §2.9.

**Download CSV is the point of the page**: the set walks to a machine as a numbered list of song,
difficulty, length, your best, grade, projected points and rate. It needs **no backend at all** —
a client-side blob, with a BOM so Excel opens Korean and Japanese titles as UTF-8 rather than
mojibake.

**Saved sets are plural and named** (owner, 2026-08-11), so Save asks for a name — prefilled with
something worth keeping (*"Doubles — 33 charts, up to D26"*) — and a Saved sets menu loads or
deletes them.

That still does not need a table. **A JSON list in UiSettings**, keyed per board like the density
preferences, holds `{name, orderedChartIds}` for a handful of sets: no migration, no repository,
and — the deciding argument — **no purge-manifest entry**, because a user's settings are already
deleted with the user. A new `MoMPlan` table would need one (§9.7), and would earn its keep only
if plans ever had to be shared or queried across accounts.

### 11.6 The four numbers

**"The four levers" is the internal name and must never reach the screen** (owner, 2026-08-10 —
it means nothing to a player). The section is headed *"Where the points came from"*, which is the
question it answers and close to how the owner described it unprompted.

Total score is *how many charts × how hard × how well*, capped by time. So a run — planned or
played — is described by four numbers, and a comparison is those four side by side:

| Lever | What it answers |
|---|---|
| **Charts played** | how many you got through |
| **Average difficulty** | what the base rating paid — **balanced, not the folder number** |
| **Average grade** | the multiplier you earned |
| **Downtime** | what capped the count |

**Average difficulty is the season's frozen balanced level** (§4 layer 4), not the chart's folder
number (owner, 2026-08-11). The balanced level is what actually priced the chart, so it is what
the run was worth playing; the folder number is a label. `UserTournamentSession.AverageDifficulty`
stores the nominal average today and has to change with the rest of §6.

It reads roughly half a level above the folder average, because a chart with no override sits at
`nominal + 0.5` — 김재현's Winter 2025 run is **24.22 balanced against 23.67 by folder**. Label it
so that gap does not read as a bug. It also moves the gaps between players: he and tieny are 0.15
apart by folder and **0.04** apart balanced, and his own two seasons flip from *0.09 harder* to
**0.41 easier**, which is the opposite conclusion.

Downtime is the one players talk about most, and it is the only lever that is purely logistical
rather than physical — which is exactly why it is worth showing.

**Plates are a fifth bar on Phoenix 2 only.** They are 1.0 across the board on Phoenix 1, so a
plate axis there would render four bars of nothing.

**D15 binds this section hardest.** Every one of these numbers is meaningless across chart types,
so a comparison is always within one board: your session against your other session, or against
another player on the same board. Never a season total, never a cross-type average.

### 11.7 Discord

**A fourth `DiscordFeedKind`**, beside WeeklyCharts, DailyStep and OfficialLeaderboards — the same
per-mix, community-independent subscription, reached the same way: `/piu feed mom mix:Phoenix`.
A channel opts in once and gets every run on that mix. No per-community filtering; the event is not
popular enough to need it.

**One card per published session** (D17 — a draft never fires one). Because a published run cannot
be edited, no second card ever corrects a first.

Composed as a **`RichBotMessage`** like every other card, so the Discord adapter owns the emoji swap
(`#MIX|…#`, `#DIFFICULTY|…#`, `#LETTERGRADE|…|…#`, `#PLATE|…#`), the Components V2 rendering and the
plain-text fallback:

- **Header** — `### #MIX|…#**{name}** — {total} points`, with `-# March of Murlocs · {season} ·
  {chart type} · {place}`, the player's avatar as the section thumbnail. The board is named because
  a number without a chart type says nothing (D15).
- **Stats** — chart count, average balanced level, lowest → highest difficulty, downtime, average
  grade.
- **Biggest five** — ranked by **points, not raw score**. Points are what the run is made of, and
  the score rides each row anyway; ranked by score this run returns five 21s and 22s, which says
  nothing about it.
- **Footer + link** — the mix line, and *See the run* to the Session Breakdown.

Two things to settle when it is built: **placement (“1st of 11”) is an addition to the owner's
field list** — a result card with no placement reads oddly, but it is one clause to cut. And a
**deleted run leaves its card behind** with a link that 404s (§10), which is accepted.

MoM has never had any Discord presence, which is part of why §2.2 went unnoticed for 39 days.

### 11.8 Past seasons is a dialog, not a page

**There is no `/Seasons` route** (owner, 2026-08-11). Four seasons hold results and the quarterly
cadence adds one a quarter, so this is a picker rather than a body of content — and a page would
buy a route, an empty state and an SSR decision for a list of five rows.

The dialog lists every season newest first: name, dates, and a line per board carrying the run
count, the winner and **how you did on it** — *you won it* / *you were 10th — 35,879* / *you sat
this one out*. The live season sits at the top with a **running now** chip. Picking one opens that
season's page.

Nothing is hidden by this. **The crawlable artifact is each season's own page**, which the sitemap
lists directly. But the dialog gives a crawler no path *between* seasons, so the season page has to
carry **previous / next season** links of its own — cheap, and it is what keeps the archive
reachable without a route. If the list ever passes roughly twenty rows it earns the page then.

### 11.9 The 2026-09-05 redraw — decisions

All seven mocks were redrawn against the design language the site shipped in August (the PUMBILITY
section frame, the Session Spotlight, the Official Leaderboards rows with the country wash, the
stacked score) and reviewed over six rounds. **Nothing below changes a rule or a stored row** — the
owner was explicit: UI/UX only. The rules of §1 and the schema of §6 stand.

| # | Decision | Rationale |
|---|---|---|
| D23 | **Scores are the stacked grade-and-plate everywhere** (`.sbd-score-stack`: grade over plate at 11px, the number beside them spanning both rows), and **the difficulty bubble comes before the song image** in every row. | Owner, 2026-09-05: "everywhere we can … will make song names show a bit more on mobile"; "always put difficulty bubbles before song images (I think we get that wrong in a few places)". Both are sitewide vocabulary, not MoM's; `SessionScoreRow` puts the jacket first today, so 4a brings a small sitewide change. UX-GUIDELINES rule 3 gains both lines in the PR that ships them. |
| D24 | **Words explain only what is not self-explanatory.** | Owner: "there's a lot of words." Ledes, hints and section notes are one clause or gone; what survives is the rules summary, the re-pricing ledger's multiply note, the import checks, and the Planner's warning when a rung cannot fill the window. |
| D25 | **Download image is the site's share-card flow**: the shared `ShareCardSettingsDialog` (same remembered `ShareCard__Options`, same example preview, same filename line), the shared composer and `SkiaShareCardRenderer`, and the card is the **tier-list card with rows as twenty-minute sections of the session**. Session points take the corner chip where the tier-list card prints PUMBILITY. **To Do, Top 50 PUMBILITY and Passed in other mixes are hidden** on a session card. Offered right after publishing and on any published Breakdown. | Owner: "shouldn't download image use the same configuration/flow we do for all the other download images? we might be able to tier list it by 'when the chart landed in session' (break up into 20 minute sections)". Hidden options: owner, "hide them I guess". |
| D26 | **Breakdown order: stats → the session (three densities, pace) → Compare.** | Owner: Compare "should sit beneath your session"; it had sat between "what are my session stats" and "what were my session songs". |
| D27 | **The Planner prices each chart at an Energy** — the PUMBILITY page's own setting (Good · Great · Top of my game, `Pumbility__Energy`): Top of my game is your best on each chart (the ceiling), Great and Good the projector's rungs for your typical score at that level. **Default Good.** | Owner: "I play a lot at ~20s but can push up to 24s on good days … I shouldn't have my session off of 24s." Reuses the projector `PeersSection` already reads; no new pricing model. |
| D28 | **Push caps the level a suggested set draws from, anchored on your last session's average folder level**: Steady = one level below it, Push = the average, All out = no cap. **Default Steady.** With no session, the anchor is the level you play most in recent sessions. Harder charts stay in the list, faded, still tickable. The page says plainly when a rung cannot fill 1:45 from the record book. | Owner: "for a stamina tournament, we should put Steady as a level lower than your session average. Push is your session average, all out just has no filter." |
| D29 | **Rest charts get their own shelf**, and a rest chart passes **five tests against its own folder** (mix · type · level), from the step analysis in `ChartSkillMetric`: (1) steps per second (`tap_rows` ÷ seconds) in the bottom quarter; (2) hold share (chart-identity.md §3.9) in the top quarter; (3) no drills and no anchor runs (`badge_fraction:drill` = `badge_fraction:anchor_run` = 0); (4) hard twists (`badge_fraction:twist_over90` + `twist_far`) covering at most 0.50 of the chart; (5) crux density (`crux_enps`) no higher than the folder's 60th percentile. **"Clean" is AAA or better from the player.** The shelf lists only qualifying charts, each with a tick; **nothing is woven into the set** and no "almost" lines. | Owner's examples (8 6 FULL D23, Altale D23, Scorpion King D23, Ugly Dee D18, Hi Bi D21, Iolite Sky D20) share (1)–(3); NPS is *not* the tell (hold ticks inflate it — Altale sits at the D23 median NPS) and neither is twist share (Scorpion King is twisty). His rejections fixed (4) and (5): V3 D24 is hold-heavy but its hard twists cover 1.43 of the chart ("those twists are INTENSE"); 4NT D24's crux sits at the 73rd percentile of its folder where every accepted chart is at or below the 59th. On 김재현's book the rule yields Slam, Queencard, 8 6 FULL, Pop Sequence and Pneumono at AAA+ — the owner's own list. |
| D30 | **A Finishers shelf**: the player's charts of three minutes or more, by points at the chosen energy, under one line — start one with seconds left, it counts in full. | Owner: "since you can start the last chart at 1 second left, long songs … are HUGE points." The solver's closing-chart pick (§11.5) is the same idea made explicit. |
| D31 | **No autoplay and no play icon anywhere in MoM.** A jacket opens `ChartDetailsDialog` without `AutoPlay`. | Owner: "don't make any of the charts 'autoplay' in these pages." Supersedes the §11.3 sentence about the play button. |
| D32 | **My Sessions gets a one-way on-ramp into MoM.** A quiet *Record as a March of Murlocs session* link sits under every session's title (there is always a season — they auto-cycle). A loud callout appears when the night holds a **window the length of a session (1:45) in which every play is one chart type and the rest inside it is under fifty minutes** — stage breaks ignored, finished-but-broken plays counting toward the type; the session's own start and end do not matter (a night can carry plays a skipped import left behind). Its button opens Submit with those charts filled in; the player still publishes. Once published, one gold chip carries the result and links to the MoM breakdown. **MoM never links back to My Sessions.** | Owner, three rounds: the rule as stated, then "the math is completely off" when the first cut fired on a nine-chart block, then "it should look for ranges of time in the session where the user had <50 minutes rest with specifically one type of chart. The actual session's start and end time doesn't matter." |
| D33 | **The Season page opens on the board you have played** — Doubles when both or neither — and the two standing segments are the only board switcher. | Owner: "yeah that's fine." Your standing leads the page (rule 1). |
| D34 | **No dashboard presence, no winner spotlight.** | Owner: "Nothing right now. Stick to the MoM pages"; "competition is against yourself, 'winners' are more secondary." |
| D35 | **Past seasons is one row per board**: sessions, winner with avatar and points, your own result — no "won by"; on a phone your result drops to its own line. | Owner: the phone view "looks crazy bloated." |
| D36 | **The Discord card keeps its placement line** ("1st of 11") and stays a message card; the image is the tier-list card — siblings, not twins. | Owner: "Sure." |
| D37 | **The mocks are the artifacts in the header, not files in the repo.** | Owner: "if you know where they are and how to reference them, we're good." |

### 11.10 What the mocks are built on

Every figure is production-synced: the real Winter 2025 boards and avatars, 김재현's 39-chart
session and his 150-chart Doubles record book (the August pull had left his full songs out — they
joined at his session scores), his last ten sessions for the Energy and push anchors, DrMurloc's
real 14 August night for the import dialog, and a public player's 35-chart Doubles night of 8
August for the on-ramp. One staged row: tieny's second Doubles session, so a two-session player
exists on a board (D16). Two honesty notes carried in the mocks themselves: the Energy pricing
there uses the player's own recent quantiles per level where the site would read the projector,
and the 14 August night is Phoenix 2 priced under Phoenix 1 because that is the board that exists.

---

## 12. Build scope

The slices of §8 stand: **4a read surfaces → 4b write surfaces → 4c Discord**, then 5. This is what
each costs in code, by layer. No new tables and no new migrations are expected in any of them:
`MoMSession.PublishedAt IS NULL` is already the draft, `MoMSessionChart.PlayedAt` is already the
landing pad for the import's timestamps, saved sets are a UiSettings JSON list (§11.5), and the
share image reuses the tier-list renderer. **EventCompetition is the MoM vertical** (§9.8) and
everything MoM-shaped lands there; the other verticals are read through their published contracts.

### 12.1 Cross-vertical reads (published contracts and ports, no new joins)

| Need | Source |
|---|---|
| The player's record book (bests) for the Planner | ScoreLedger `IScoreReader` |
| The journal for the import dialog and the on-ramp | ScoreLedger `GetPlayerJournalQuery` (already what Slice 3 repointed onto) |
| Energy pricing (Good / Great rungs) | PlayerProgress's projector — the `IScoreProjector` domain service the PUMBILITY page reads; Top of my game is the best attempt, no projector |
| Rest-chart facts | **Catalog** (owner of chart identity and the folder baselines): a new contract `GetRestChartFactsQuery(mix, chartIds)` that computes the five tests inside Catalog against the folder and returns flags — the metrics live in `scores.ChartSkillMetric`, the folder quartiles are a small in-memory pass over ≤ 200 charts, and the rule never leaves the vertical |
| Board tints (you · rival · community) and who may look | Rivals / Communities through the same path the weekly boards use, `IPlayerVisibilityReader` for the session page's visibility |
| Names, avatars, countries on rows | Identity `IUserReader` (UserLabel already does this) |
| Discord subscriptions and delivery | Communities `IDiscordFeedReader` + the Discord adapter in Data via `RichBotMessage` |

### 12.2 Slice 4a — read surfaces

- **Domain (EventCompetition, internal):** the four-lever read model (charts · balanced average ·
  grade average · downtime) as a pure function over a session's chart rows; the season-comparison
  re-pricing (§11.3, the §4 arithmetic run four times per chart over both stored configs); board
  ranking (score order, sessions not players). Nothing in `ScoreTracker.Domain` changes.
- **Application (EventCompetition/Application):** `GetMoMSeasonPageQuery` (live season, both
  boards with rows and the viewer's standing per board), `GetMoMSessionQuery` (hero, four numbers
  with board places, charts, pace), `CompareMoMSessionsQuery` (same-board and past-season modes),
  `GetMoMSeasonsQuery` (the dialog), plus the 301 lookups from a legacy tournament Guid to its
  board. All `IQuery<T>`, all in `Contracts/Queries/`.
- **Infrastructure (EventCompetition/Infrastructure):** reads on `EFMoMRepository` for the
  above — board rows with chart-count and rest, a session with its chart rows, the season list
  with per-board winner and the viewer's placement. No schema change.
- **Presentation (Web):** `Pages/Competition/MarchOfMurlocs/` — `MoMSectionFrame` (eyebrow, frame
  nav chips, the Past-seasons dialog island), **`Season.razor` as static SSR** (§11.2; the
  standing segments are links), `SessionBreakdown.razor` (circuit: densities, compare, the
  Download image dialog), the Past-seasons dialog; components `MoMBoardRow` (`.olb-rank-card` +
  `UserLabel` with the wash), `MoMFourNumbers` (with the one-mark-per-session strips),
  `MoMChartList` (Comfortable = the highlight jacket card, Compact = the sticker, Table = the
  board-skinned table), `MoMPaceChart` (inline SVG); the share card: a `MoMShareCardComposer` in
  Web `Services/` feeding `TierListShareCard` rows by twenty-minute section into the existing
  renderer, and `ShareCardSettingsDialog` gains a parameter to hide the three options (D25);
  routes and 301s (`/Tournaments/MarchOfMurlocs` → `/MarchOfMurlocs`, `/Tournament/Stamina/{id}` →
  the season with that board selected — board Guid = legacy Guid); `ShellNav` / `ShellMoreSheet`
  link to `/MarchOfMurlocs`; the `MixCapabilities` entry (D19); **sitewide**: `ScoreBreakdown`
  gains the stacked variant and `SessionScoreRow` moves the bubble before the jacket (D23), with
  UX-GUIDELINES rule 3 updated in the same PR; localization keys in all nine locales, inserted
  alphabetically; **deletions**: `MarchOfMurlocs.razor` and `StaminaTournament.razor` go now;
  `RecordTournamentSession.razor` and `SessionBuilder.razor` stay reachable behind the new
  page's *Record a session* and *Planner* links until 4b lands, so recording never has a gap.
- **Tests:** unit tests for the lever math and the re-pricing split (`DomainTests`, real numbers
  from §11.3); handler tests with mocked ports (`ApplicationTests`); bUnit for the row, the four
  numbers, the chart list densities and the share composer's sections (`Tests.Components`);
  `Tests.Api` untouched (no public API surface changes); one E2E fact: the season page renders a
  board and a session breakdown opens from a row; the share-card composer's sectioning has a unit
  test on 김재현's real timeline.

### 12.3 Slice 4b — write surfaces

- **Domain (EventCompetition, internal):** `MoMSessionDetector` — the D32 window scan as a pure
  function over (start, duration, chart type, stage-broken) plays, shared by the import dialog's
  block pre-selection and the My Sessions callout; the draft lifecycle rules (draft → published →
  frozen, delete allowed; §1's window predicate already lives in `TournamentSession.CanAdd` after
  Slice R); the Planner solver extends `AutoBuildSessionHandler`'s engine with the energy price,
  the push cap and the closing-chart swap.
- **Application:** commands `CreateMoMDraftCommand`, `AddMoMDraftChartCommand` /
  `RemoveMoMDraftChartCommand` (the per-row `CanAdd` guard of the Slice 3 bug-check is the rule
  here — skip, never throw), `ImportMoMDraftFromJournalCommand` (writes `PlayedAt` from the
  journal's `OccurredAt`, skips charts already in the draft), `PublishMoMSessionCommand`
  (freezes; sets `PublishedAt`; publishes `MoMSessionPublishedEvent` for 4c),
  `DeleteMoMSessionCommand`, `DiscardMoMDraftCommand`; queries `GetMoMDraftQuery`,
  `GetMoMImportCandidatesQuery` (journal → blocks split at fifteen-minute gaps, the three checks
  of §11.4), `BuildMoMPlanQuery` (record book + Energy + push + rest facts + finishers → the
  four numbers and the set), `DetectMoMSessionQuery` (My Sessions' callout), `SaveMoMSetCommand` /
  `GetMoMSetsQuery` over UiSettings.
- **Infrastructure:** `EFMoMRepository` writes for the lifecycle (draft rows, chart rows with
  `PlayedAt`, publish, delete); the derived columns on `MoMSession` recomputed on every save (§6).
  Catalog's `GetRestChartFactsQuery` handler (Catalog/Application + a read over
  `ChartSkillMetric` in Catalog/Infrastructure). No schema change.
- **Presentation:** `Submit.razor` (circuit; the budget bar, the three-key entry with the plate
  selector and Shift+Enter, the import dialog with block selection and checks, the publish
  dialog, the published state with Download image first; page dock on the phone),
  `Planner.razor` (circuit; Energy select reusing `EnergyLabels`, push segments, rest slider,
  Suggest a set, the Rest charts and Finishers shelves, three densities with outline selection,
  saved sets, client-side CSV with a BOM); the My Sessions on-ramp in `SessionHero.razor` (quiet
  link, callout, recorded chip); the `PasswordManagerHints` ratchet is untouched — there is no
  credential field anywhere in MoM any more; **deletions**: `RecordTournamentSession.razor` and
  `SessionBuilder.razor`, and the `GetRandomChartsQuery` note in Known divergences is unaffected.
- **Tests:** the detector and the solver in `DomainTests` on the two real nights (the 8 August
  night qualifies, the 14 August night does not) and on 김재현's book (Steady runs dry, Push fills);
  handler tests for the lifecycle including the skip-not-throw guard; bUnit for the budget bar,
  the entry row, the import dialog, the shelves and the on-ramp states; one E2E fact for the
  whole submit flow (draft → import → publish → on the board) against the WireMock piugame stub
  — the site's critical MoM journey.

### 12.4 Slice 4c — Discord

- **Application:** a fourth `DiscordFeedKind` and `/piu feed mom mix:` in the existing command
  handling; `MoMSessionPublishedEvent` consumer in EventCompetition's saga composing the
  `RichBotMessage` of §11.7 (header with placement, the four numbers, the biggest five by points,
  the link to the breakdown). A draft never fires; a deleted session's card is left to 404 (§10).
- **Infrastructure:** none new — the Discord adapter already renders `RichBotMessage`.
- **Tests:** the composed message pinned in a handler test; the canary is run once in the
  owner's lab channel before merge (see the run-canary standing rule).

### 12.5 Out of scope, on purpose

Phoenix 2 boards (Slice 5, admin-gated until the scoring session), any dashboard widget, any
change to §1's rules or §6's tables, and weaving rest charts into a suggested set.

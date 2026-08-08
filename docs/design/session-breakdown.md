# Session Breakdown (`/Player/{id}/Sessions`) — design

> **Status: BUILT (B1–B10).** Workshopped and built
> 2026-08-01 on `claude/session-highlights-overhaul-35efba`, PR #211. Supersedes the "Recent Scores
> page" section of [discord-rich-score-notifications.md](discord-rich-score-notifications.md),
> which specified the page as the Discord card's link target. That page shipped as a paged grid
> of equal-weight `SessionRoundupCard`s — the card at web resolution. Owner: *"we kind of hacked
> it together before."*
>
> Mock (iteration 1, owner-approved):
> <https://claude.ai/code/artifact/ee31f0e2-0b0b-440b-a07a-44fe58df90e1>

The page stops being a list of equal cards. **The most recent session renders big**; everything
older collapses to a board-skinned table with a date, three counts and a **View** button that
promotes a session into the hero.

---

## 1. Locked decisions

Owner calls from the 2026-08-01 workshop. Dated because several reverse earlier design-doc
positions.

| # | Decision |
|---|---|
| D1 | **Hero + table.** One session rendered big; the rest are rows. The ⭐ Of-Note filter and the sessions-per-page selector **retire** — both existed to make a wall of cards scannable. |
| D2 | **Hero order**: header → ceremony band → titles earned → scores that mattered → community peers → skill focus → all plays. |
| D3 | **The glowy bar is per-folder, one title.** Phoenix uses difficulty titles, Phoenix 2 uses PUMBILITY titles (Singles/Doubles/Total). One bar per level folder played, showing **the title you are working on in that folder**. *"Our old UX that showed multiple titles per folder was bad."* |
| D4 | **Titles earned lists every title**, no collapsing — consistent with the standing card rule (five rungs render as five lines). |
| D5 | **Attempts-to-clear shows only when there was at least one prior attempt.** A first-try pass gets no indicator: *"they're still passes, all new passes are good, they just won't have an indicator of how many attempts it took."* |
| D6 | **No failure section.** The rejected "charts you battled and didn't clear" is replaced by **All plays** — the session's full journal, breaks included, rendered as a neutral log. Failures are never highlighted; the only place one is *counted* is the 🎯 badge on the clear that ended them. |
| D7 | **Community Peers is user-created communities only.** World and the country community are auto-joined system communities — scoping to "all your communities" scopes to everybody. |
| D8 | **Sort by closeness, do not filter.** Every community member with a score on the chart appears, nearest competitive level first. Precision filtering is Rivals' job. |
| D9 | **Community Peers covers every highlighted chart** that has at least one peer score; charts with none are skipped silently. |
| D10 | **Score colour is the competitive cohort, not the community.** ±0.5 competitive is large enough (~90–120 players/chart) for a percentile to mean something; a 7-person club board is not. The standing always prints beside the colour (UX rule 8) — as a **place**, see D27. |
| D27 | **Standing prints as a place, never a percentage** (owner, 2026-08-08). The row shipped "top 94% at your level · 94 in cohort" for a score that beat 94% of its cohort: the wording began as `>99%` and drifted to `top`, which inverts the claim for everyone above the median. Rather than repair the percentage, the row prints `#6 of 94 peers` — the same fact, in the words `CommunitySaga.PeerCaption` was already using, and unreadable backwards. The percentile is untouched underneath and still drives the colour. |
| D28 | **"Peers" means the other people at your level, so counts exclude you — but a place does not.** The captured cohort includes the player (you are trivially within ±0.5 competitive of yourself), so `PeerPgCount`/`PeerCount` drop one when reporting how many *others* did something. A place keeps the whole cohort as its denominator, because a place is a position inside a population you belong to. Both surfaces (row and Discord card) apply this identically; the subtraction is at render, so already-captured rows stay correct. |
| D29 | **The 📊 badge leaves the row, not the model.** It meant "top 10% among comparable players", which the standing line now states outright. `ScoreQuality90` keeps its flag, its captured history, its Discord caption and its hot-streak seeding — only this row stops drawing it. |
| D30 | **A Perfect Game reports who shares it, not its place.** A PG cannot be beaten, only tied, so every PG row is `#1` and the place stops distinguishing anything; `PG · 3 of 93 peers have it` is the fact worth the line. When nobody else holds it the row falls back to the place, because "0 of 93 peers have it" is a clumsy way of saying first. |
| D11 | **No cohort ⇒ no colour and no explanation.** Co-op charts (the cohort is Singles/Doubles only) and charts >5 levels below competitive (capture already skips them) render in plain ink with nothing said. Owner: a "co-op has no competitive cohort" disclaimer *"is going to confuse more than clarify."* |
| D12 | **`ScoreQuality90` (📊) is untouched.** It stays the anonymous ±0.5 percentile flag it has always been, with its captured history and its Discord caption intact. Community Peers is purely additive. *(Amended by D29: the flag is still untouched, but the session row no longer draws its glyph.)* |
| D13 | **Skill focus is composition first**, measured against the folders actually played, with performance as a second line only where evidence supports it. |
| D14 | **Official chart placement**: any placement inside the mirrored board depth (P1 ≤100, P2 300) flags. **No special treatment for #1** — *"the #1 player in the world is so far ahead of everyone else that that seat doesn't change."* |
| D15 | **Official PUMBILITY rank is stored** next to competitive level. **Phoenix has one combined board; Phoenix 2 has three** (All/Singles/Doubles). |
| D15a | **The rank is ESTIMATED, like a chart placement** (owner, 2026-08-01: *"estimated rank then if it's daily"*). We do not read back a mirrored rank — we place **our** computed PUMBILITY value into the last sealed board and count who is above it. This is what makes it move **per import** instead of per sweep, which is what lets it be a session milestone at all. |
| D16 | **No piuscores-computed rank.** *"Hold off on our rank. Official for now. There's BIG plans coming up that change what 'PIU Scores' ranking means."* The estimate in D15a is a position on the **official** board, not a rank among site users — those are different claims. |
| D16a | **Rank movement is a line on the existing card, not a card of its own** (owner, 2026-08-01). It joins the stats block next to PUMBILITY and competitive level. There is no new Discord message, no new fan-out, and therefore no volume question to answer. |
| D17 | **One shared `ChartLeaderboardDialog`**, sibling to `ChartDetailsDialog`, openable from any chart on the page — scopes: World · Region · Community · Competitive Peers. |
| D18 | **"#X of Y" lives in the dialog, not on a row** — it names its scope there. A place on a small board is a fact about attendance, not standing. |
| D19 | **The community picker renders only when the player belongs to more than one** user-created community. |
| D20 | **Rivals is not hinted at anywhere** in this iteration — no disabled chip, no "soon" tag. It is the next project and this branch blocks it. |
| D21 | **Backfill is one session per player** (the most recent), behind an admin button. No historical backfill. |
| D22 | Official placement **rides the Discord card**. |
| D23 | **The journal stays the spine; `ScoreSession` is enrichment** (§2.3). [delete-my-data.md §4](delete-my-data.md) leaves this page on `GetSessionGroups` on purpose and hands the move to "a separate change with its own answer for the back catalogue" — this is that change, and the answer is *don't move, join*. |
| D24 | **No undo affordance on this page.** Owner-settled in the delete work: the undo banner lives on the delete page, *"not on the public Sessions page."* The hero never grows an Undo button. |
| D25 | **This design's new capture reuses `ScoreHighlight` / `PlayerMilestone`, keyed by `SessionId`** — it needs no new shape, and those two are already wired into undo and the purge. A new table is a perfectly ordinary thing to add; it just inherits both duties explicitly (§2.3). |
| D26 | **`OfficialPumbilityRank` mints on improvement only**, matching every other rating milestone — otherwise an undo announces the rank it just cost you. |
| D31 | **⬆ carries the number it was always implying** (owner, 2026-08-08). The flag is unchanged — same rows, same rarity — but it now reads `22.5 (+0.3)`: what the score rates on the competitive scale, and how far above the level the batch opened at. Suppressed under +0.05, silent on co-op. Rejected: printing the diff on *every* positive row. The flag additionally requires that the batch actually raised the level, so "positive" and "flagged" are different sets and neither contains the other — a diff on every positive row would land on most of a good night and retire a mark that is currently rare and earned. |
| D32 | **A chart's PUMBILITY gain is captured, not derived at render** (owner, 2026-08-08: *"real data"*). 👑 says a chart sits in your top 50 — a standing fact a chart can hold all night having gained you nothing. The gain answers the other question. Render-time reconstruction was rejected because it is wrong precisely on the best case: a chart *entering* the pool displaces the fiftieth, so crediting it its whole value overstates by whatever it pushed out, and the displaced value is not knowable at render. |
| D33 | **The gain measures the combined pool only.** Phoenix 2 has three (All/Singles/Doubles) and Phoenix has one; the row reports one number, and the combined pool is what the ceremony band headlines. |
| D34 | **No backfill** (owner, 2026-08-08). Both new captures are forward-only; the maintainer's own most recent session is seeded locally for the field test. The existing `RebuildLatestSessionsCommand` would not have served anyway — it replays the change set through the live pipeline, and a rebuild computes against **today's** stats, so a delta measured old-vs-new comes out zero for a session already applied. |
| D35 | **The Phoenix 1 delta is computed at render, not captured** (owner, 2026-08-08: *"P1 gonna be frozen in a month"*). A column for a number that stops moving is a column for nothing. The consequence is honest and accepted: it reads today's Phoenix 1 best, so a session viewed later reflects Phoenix 1 as it stands then. |
| D36 | **A break is never a score that mattered.** D6 already said failures are never highlighted; the section reached one anyway through its **padding** — it tops itself to six rows with unflagged scores, and on a thin session a stage break can be the highest level present. Breaks are filtered from both halves and live only in All plays. |
| D37 | **Capture in flight gets the patience card** (owner, 2026-08-08). `HighlightCaptureSaga` is a bus consumer, so a page opened right after an import can beat it — which rendered as an empty hero, indistinguishable from a session that earned nothing. The card stands in for the **whole** capture-derived region per UX rule 9; the ceremony band and All plays stay, because they read the stats row and the journal and are true the moment the import lands. |
| D38 | **The pending state is inferred, and the inference expires.** No `ScoreSession` row ⇒ never pending (historical by definition). Otherwise: nothing captured **and** `LastActivityAt` within **2 minutes**. A session of co-op or far-below-competitive charts legitimately captures nothing and looks identical, so the window is what stops the page telling that player to keep waiting. |
| D39 | **The page listens for the event; it never polls and never guesses that capture has finished** (owner, 2026-08-08: *"whatever that second event firing is, that's the one we want"*). `ScoreHighlightsCapturedEvent` is published when capture completes, and `ScoreHighlightsCapturedUiBridge` — a **public** bus consumer in Web, so the host's assembly scan finds it — forwards it to the player's UI topic. Same shape as the randomizer's draw view. Delivery is best-effort by design: a player who was not on the page never receives it and simply reads the finished article on arrival. |
| D39a | **Why polling could not have worked, kept because the shape of the failure is instructive.** Scores are held as a batch for **two minutes past the LATEST of them** (`ScoreBatchPolicy.HoldWindow` — every score pushes the deadline out again), so capture does not begin until well after an import stops writing. Four timers were tried and each failed differently: clearing on the first row (mid-pipeline), on a stable row count (between batches), gating the watch on the card (never ran for the page most likely to need it), and a two-minute window that expired at the exact instant the batch fired. Every one was a guess at another component's schedule. |
| D40 | **⚠ A `HighlightsCapturedAt` stamp on `ScoreSession` is still worth having**, though no longer to drive the card. It would let the page tell *failed* from *unremarkable* — identical today, even though the capture saga swallows each step's exceptions — and would give a page that missed the event something to read on arrival. The test would be `HighlightsCapturedAt >= LastActivityAt`; anything less is per-batch and says nothing about an import as a whole. |

### Deliberately not decided here

Whether the world-rank line belongs in the ceremony band at all — it is the one number there
that cannot move within a session. Parked for field test.

---

## 2. The model

### 2.1 What is already captured (presentation-only work)

`PumbilityGain` / `SinglesPumbilityGain` / `DoublesPumbilityGain` / `SinglesCompetitiveGain` /
`DoublesCompetitiveGain` / `TitleCompleted` / `FolderPassLamp` / `FolderGradeLamp` /
`FolderPlateLamp` / `FolderProgress` / `WeeklyPlacement` milestones, and the six `HighlightFlags`
with their `HighlightDetail`. The ceremony band, titles-earned and the lamp strips need **no new
capture** — only a new rendering.

### 2.2 What must start being captured

Everything below is written at capture time, per the doctrine that made the flags historically
true in the first place. All of it lands on the two existing PlayerProgress tables.

**Title progress deltas.** `TitleSaga.ComputeSessionTitles` already computes
`TitleProgressDelta(Title, OldPercent, NewPercent)` and the design doc deliberately kept it
**event payload only** — *"the Sessions page would drown in gold rows."* That reasoning was about
rendering them as gold strips; as a dedicated band it lapses. They persist as a new
`MilestoneKind.TitleProgress`: `Title` = title name, `OldValue`/`NewValue` = percent,
`Detail` = `"S21|3120|4000"` (folder | current rating | required), packed exactly as
`FolderProgressDetail` already packs its own. **`MilestoneStrip` must not render this kind** —
the hero routes it to the bars, and a session emits several.

> A session envelope spans 8 h while a batch drains after 2 min, so one session emits many
> batches. The page aggregates per title: **min old, max new**.

**Per-score peer percentile.** The colour needs a percentile for *every* score, not only the
ones clearing the 90th. `FlagScoreQuality` already computes `TieInclusivePercentile` for every
changed chart in an eligible folder and throws it away below the threshold. Storing it is nearly
free, but it changes what `ScoreHighlight` means: **a row is written for every change with a
percentile, not only for flagged ones**. Rows with `Flags = None` are ordinary scores carrying a
colour. A 41-play session writes ~41 rows instead of ~6 — fine at this scale, and the Discord
card is unaffected (it renders flags off the event, not the table).

**Attempts before the clear.** Count of journal rows for the chart, same mix, before the passing
row. ⚠ **The data begins 2026-07-30** (`03f0c307`, "journal the plays that were not your best")
and comes from a **single-page** scrape of the site's recently-played list, so coverage tracks
import cadence. True walk-offs are never stored, by policy (`BestAttemptPolicy.IsWalkOff`).
Scoped **session-local** — "your 7th try tonight" — which is both truer and the better feeling.

**Official chart placement.** Count placements above the score on the chart's board in the latest
sealed snapshot, +1. Excludes the player's own existing row where the official player link
exists. Carries `AsOf` and board depth; the UI prints `~` and the date because the sweep is
weekly.

**Estimated official PUMBILITY rank.** Denormalized onto `PlayerStats`, and **estimated exactly
the way a chart placement is** — take the player's freshly recomputed PUMBILITY pool, count the
entries above it on the last sealed board, +1. Not a read-back of a mirrored rank.

That choice is what makes the whole thing work. A mirrored rank only changes when the sweep runs,
so it could never be a session milestone — nothing a player did that night would move it. An
estimate against a fixed board moves the instant **their own** value moves, which is on every
import. So:

- Recomputed in `PlayerRatingSaga.RecalculateCore`, right where the pool is already calculated —
  no extra read beyond the board slice.
- A change mints **`MilestoneKind.OfficialPumbilityRank`** (`OldValue` → `NewValue`,
  `Detail` = board name — Phoenix has one, Phoenix 2 has All/Singles/Doubles), which reaches the
  session snapshot card through `ScoreHighlightsCapturedEvent.Milestones` like every other
  milestone.
- The board slice still refreshes on `OfficialSnapshotSealedEvent`: a new snapshot moves everyone
  else, so estimates go stale in the other direction until they are recomputed.

Two things it is honest about, and both must reach the UI:

1. **It is only as good as our formula match.** The Phoenix formula is exonerated (see the
   ERRLENA investigation — the mismatch was a truncated import, not the maths); Phoenix 2 still
   guesses B-and-below grade multipliers. A `~` is mandatory, never a bare `#`.
2. **The board underneath is up to a week old while your value is current**, so the estimate
   leans slightly optimistic — everyone below you has been improving too. It always prints the
   board's date.

`GetOfficialPlayerStandingQuery` is deliberately not the mechanism here: it resolves a rank by
pulling the whole rankings list per board and scanning it — fine for a profile page, far too
heavy per import, and it answers last Sunday's question rather than tonight's.

### 2.2a The second capture pass (2026-08-08)

Two more numbers a batch computes and discards, both landing on `ScoreHighlight`.

**The competitive baseline.** `FlagCompetitiveImprovers` already compares each changed score
against the competitive level as the batch opened, then drops it. That level is **per-batch** — a
session drains as several batches, each with its own before and after — and `PlayerStats` keeps
only the last, so nothing downstream can recover it. The score's own competitive level stays a
pure function of chart level, score and type (`CalculateFungScore`), so one stored column buys
both halves of `22.5 (+0.3)`.

**The PUMBILITY gain.** Needs the batch's *old* scores, which only the change set carries, so
`CaptureSessionStats` now takes the changes rather than just the ids of what moved. An admin
recalculation has none and therefore claims no gain — pricing every chart as if it had just
arrived would credit a maintenance run with the whole pool.

Attribution lives in `PumbilityAttribution`, a pure function, because the arithmetic is where
this could quietly go wrong:

- A chart that **kept its seat** is worth its whole improvement — nothing had to leave.
- A chart that **took a seat** is worth only what it beat the departure by. The naive reading
  credits an entrant its full value and overstates by the size of whatever it pushed out.
- The pool is a fixed fifty, so entrants and leavers always arrive in equal numbers, and the
  pairing chosen is the order the pool actually falls in: the strongest entrant displaces the
  weakest seat. **The split reconciles exactly with the total the ceremony band prints above it**,
  whatever the pairing — a property the tests assert directly at three pool sizes.

⚠ The old value is priced with the chart's **current plate**. Phoenix never reads the plate, so it
is exact there. On Phoenix 2 it is exact unless the plate improved in the same play that raised
the score, where the old side prices slightly high and the gain reads slightly low. Closing that
means carrying the old plate on `ScoreChange`, which does not have it.

### 2.3 The delete / undo ecosystem

Merged from main 2026-08-01 ([delete-my-data.md](delete-my-data.md)). It lands squarely on this
page and settles four things.

**Session-scoped data now owes undo an answer.** `ScoreSessionUndoneConsumer` (PlayerProgress)
drops the session's `ScoreHighlight` and `PlayerMilestone` rows on `ScoreSessionUndoneEvent` —
*"neither is recomputed from scores, so an undo that left them behind would keep claiming the
player hit a title they no longer hold."* Everything §2.2 adds lives on those two tables keyed by
`SessionId`, so it travels for free.

That is a reason this design reuses them, **not a rule against new tables**. Adding one is
ordinary; it simply inherits two duties that the existing tables already discharge:

1. **If it is session-scoped, delete it on `ScoreSessionUndoneEvent`** — extend
   `IPlayerScoreDataRepository.DeleteForSession` rather than adding a second consumer.
2. **If it carries a user key, declare it** in the owning vertical's
   `EFAccountPurgeRepository.UserOwned`, or write the exemption and its reason.
   `AccountPurgeCoverageTests` fails the build otherwise — which is the point of it.

**The estimated rank recomputes on undo, and must not announce it.** Undo publishes a
`PlayerScoresUpdatedEvent` with an **empty change set**, which still reaches
`CaptureSessionStats` → `RecalculateCore` — exactly where the rank is stamped — so the rank falls
back correctly with the scores. The milestone must therefore mint on **improvement only**, like
`PumbilityGain` and the competitive gains already do (D26). That event also carries **no
`SessionId`**, so anything minted on the undo path is session-less by construction.

**The journal stays the spine, `ScoreSession` joins onto it** (D23). The delete doc keeps this
page on `GetSessionGroups` because *"nothing before this ships has a session row"* and moving the
page to the table *"would erase every historical session from a page players already use"*. The
answer for the back catalogue is not to move but to **join**: group the journal as today, then
enrich each group from `GetScoreSessionsQuery` where a row exists. That buys three things without
costing a day of history:

- **`ScoreCount` / `NewCount` / `UpscoreCount` come denormalized** for the history table on
  post-floor sessions; pre-floor rows fall back to counting journal rows, exactly as now.
- **`AccountTag` and `CardId`** name *which card* an official import pulled from. This belongs on
  the hero header, not just the undo list — the wrong-card case is precisely when a player stares
  at a session and thinks "these aren't my scores."
- **Two clocks become distinguishable.** `StartedAt` / `LastActivityAt` are **wall clock — when
  the scores reached us**; the journal's `OccurredAt` is **the official site's play date**. The
  hero shows one today. Where both exist, wall clock is the honest answer to "when was this
  session"; pre-floor sessions have only the journal clock, which is why the enrichment is
  optional and never load-bearing.

`ScoreSessionRecord.UndoFloor` is `2026-08-01T05:00:00Z`; nothing before it has a session row.

**Sessions can now disappear.** An undo **deletes** the journal rows (§17 of that doc retires the
"append-only" claim). Three consequences this page owns:

1. The hero's "most recent session" can change under a refresh.
2. **A Discord `?session=` deep link can point at an undone session.** The card outlives the
   thing it links to. That needs a stated "this session was undone" state, not an empty hero —
   the one place a 404-shaped hole would read as the page being broken.
3. The **backfill** (§4.4) must tolerate a player whose most recent session no longer exists, and
   must never resurrect an undone one. It reads current journal state, so an undone session
   simply is not there — but "most recent session" being null is a real case, not a defensive one.

**A scoped mix wipe empties this page for that mix.** `PlayerStatsEntity`, `ScoreHighlightEntity`
and `PlayerMilestoneEntity` are all in PlayerProgress's `UserOwned` manifest, so a wipe takes the
capture with it and the page falls to its empty state — which is the correct behaviour and needs
no new work, only a check that the empty state reads well after a deliberate wipe rather than
only before a first import.

### 2.4 Skill focus

The **piucenter badges** (33 keys with measured `badge_fraction:` coverage), never the `Skill`
enum — that rollup is scheduled for deletion, see [nuke-old-skill-categories.md](nuke-old-skill-categories.md).

- **Composition (always renders):** each badge's coverage-weighted share of the session's charts
  minus its share across the (type, level) folders played. The folder baseline is what stops
  "all D24s are runs" reading as a preference. Needs ≥ 8 scored charts.
- **Performance (conditional second line):** deviation from the player's own folder norm on
  charts carrying each badge, per `PlayerSkillDeviations`' method. Badges without enough evidence
  **fade in place rather than vanish** — the ladder-rail rule (UX §6).
  ⚠ **Ordering risk**: `PlayerSkillDeviations` is `Skill`-enum-keyed until the skill nuke's N5
  commit re-keys it to badges. Either sequence behind N5, or compute this line session-locally
  from badge coverage without the shared machinery. Do not add a second `Skill`-keyed consumer.

---

## 3. `ChartLeaderboardDialog`

A shared dialog, sibling to `ChartDetailsDialog`, hosting **one chart's boards at four scopes**.

| Scope | Population | Notes |
|---|---|---|
| 🌍 World | every player on the site with a score | top N + `···` gap + your row pinned, as `ChartLeaderboardSection` already pages |
| 🇺🇸 Region | your country community | a system community; empty is common and normal |
| 👥 Community | one of your communities | picker row **only when you have more than one** (D19) |
| 📊 Competitive Peers | ±0.5 competitive | **this is the cohort behind the score's colour** — opening it makes the colour auditable rather than asserted. Unavailable on co-op; the chip greys with no explanation (D11) |

- **Skin is `LeaderboardDialog`'s** (`.weekly-lb-*`), not a new one: rows, the `--daily-you` /
  `--daily-community` glows, the trust ladder (✔ imported · 📷 photo), and places coloured through
  `ThemeScales.RarityStyle(percentile)`. The scope rail's precedent is that dialog's own
  "Relevant players" switch, which already re-ranks what it shows.
- **Your standing renders in the dialog header**, scoped and denominated (D18).
- **Initial scope is caller-supplied** — a session score row opens on Competitive Peers (the
  cohort its colour came from); the chart page would open on World.
- **Empty scopes name what would fill them** (UX rule 9).

⚠ **`ChartLeaderboardSection` is already the World scope** with community glow and your-row
pinning. Generalize it into the dialog rather than writing a second implementation of the same
board one click apart.

---

## 4. Technical scope

### 4.1 Verticals and layers

| Vertical / layer | Change |
|---|---|
| **PlayerProgress** | Owns the capture. `HighlightFlags` += `OfficialBoardPlacement`. `HighlightDetail` += `PeerPercentile`, `AttemptsBeforeClear`, `OfficialPlace`, `OfficialBoardDepth`, `OfficialAsOf`. `MilestoneKind` += `TitleProgress`, `OfficialPumbilityRank`. `HighlightCaptureSaga` gains the placement + attempts steps and writes un-flagged rows. `PlayerRatingSaga` estimates the official rank against the last sealed board and mints `OfficialPumbilityRank` on a change. New `IConsumer<OfficialSnapshotSealedEvent>` re-estimates for every linked player when a new board lands. New `RebuildLatestSessionCommand` + consumer. |
| **ScoreLedger** | One published contract query for the attempts count over the journal. Nothing else — session paging already exists. |
| **OfficialMirror** | Two published contract queries: a **batch** placement estimate for (chart, score) pairs (batch, not per-chart — a session touches dozens), and a **PUMBILITY board slice** (ordered values + `AsOf`) that PlayerProgress ranks a pool against. Both read the latest sealed snapshot. |
| **Communities** | One published contract query returning member scores + competitive levels for a set of charts, **user-created communities only**. Precedent: `GetPhoenixRecordsForCommunityQuery` already joins membership to scores. |
| **Catalog** | Badge coverage for a set of charts (`GetChartBadgeCoverageQuery` exists) plus a folder-level aggregate for the baseline. |
| **ChartIntelligence** | Only if the performance line reuses `PlayerSkillDeviations` — see the N5 ordering risk above. |
| **Data** | One migration (§4.3). |
| **Web** | The page, its components, the shared dialog, one pure service. |

**No new ports.** Every cross-vertical read is a published contract query — no SQL joins onto
another vertical's tables (ADR-001).

### 4.2 Classes

**PlayerProgress** — `Contracts/`: `HighlightFlags`, `HighlightDetail`, `MilestoneKind`,
new `Contracts/Messages/RebuildLatestSessionCommand`, new
`Contracts/Queries/GetSessionBreakdownQuery` → `SessionBreakdownRecord` (the page's one read;
the hero should not assemble itself from six queries).
`Application/`: `HighlightCaptureSaga`, `PlayerRatingSaga`, `TitleSaga`, new
`OfficialRankRefreshConsumer`, new `RebuildLatestSessionConsumer`.
`Infrastructure/Entities/`: `ScoreHighlightEntity`, `PlayerStatsEntity`.

**Web** — `Pages/Progress/PlayerSessions.razor` rewritten.
New in `Components/Sessions/`: `SessionHero`, `SessionCeremonyBand`, `SessionTitleBar`,
`SessionTitlesEarned`, `SessionScoreRow`, `CommunityPeersSection`, `SessionSkillFocus`,
`SessionAllPlays`, `SessionHistoryTable`. The hero owns an **undone-session state** for a
`?session=` deep link whose session no longer exists (§2.3).
New in `Components/`: `ChartLeaderboardDialog`.
New in `Services/`: `SessionSkillFocus.cs` (pure, the `FolderTitleTrack` pattern).
Retired: `SessionRoundupCard` (the grid dies with it). Kept: `MilestoneStrip`,
`ClassificationChip`. `HighlightRow` is superseded by `SessionScoreRow`.
`Pages/Admin/Admin.razor` gains the backfill button — the `RebuildHighlights` precedent on
`OfficialLeaderboardsAdmin`.

### 4.3 Migration (one)

| Table | Change |
|---|---|
| `scores.ScoreHighlight` | + `PeerPercentile float NULL`, `AttemptsBeforeClear int NULL`, `OfficialPlace int NULL`, `OfficialBoardDepth int NULL`, `OfficialAsOf datetimeoffset NULL` |
| `scores.PlayerStats` | + `EstimatedPumbilityRank int NULL`, `EstimatedSinglesPumbilityRank int NULL`, `EstimatedDoublesPumbilityRank int NULL`, `PumbilityBoardAsOf datetimeoffset NULL` (the snapshot the estimate was taken against). Named for what they are — a bare `PumbilityRank` would be read as authoritative by the next person. |

Both tables live in PlayerProgress's model contribution (already listed in
`VerticalModelContributions.All()`). Rows in [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) in the
same PR. No table is dropped.

⚠ `PlayerStatsRecord` is a **positional record with 18 members** and is constructed in several
places — adding four is mechanical but touches every call site.

### 4.3a Seeding the second capture pass for a field test

The 2026-08-08 captures are forward-only (D34), so a session that already happened renders a bare
⬆ and no gain pill. [`session-row-indicators-seed.sql`](session-row-indicators-seed.sql) fills
both onto one player's most recent session **on a local database** — enough to see the treatments
without pretending capture ran. It seeds gains only on crowned charts, because a chart outside
the pool gained nothing and a badge there would misrepresent the exact thing the feature exists
to get right.

### 4.4 Backfill

An admin button publishing `RebuildLatestSessionCommand`, scoped to **each player's single most
recent session**. Honest caveat to carry in the button's own copy: a rebuild computes against
**today's** state — current top 50, current folder clears, current cohorts, the latest official
snapshot — not the state at session time. Everything captured going forward is write-time truth;
backfilled rows are "as of the press". For a session that just happened the two are nearly the
same, which is exactly why the scope is one session and not history.

### 4.5 Tests

| Suite | Coverage |
|---|---|
| `ScoreTracker.Tests/DomainTests` | placement-estimate math (self-row exclusion, below-depth ⇒ no flag), attempts counting, `SessionSkillFocus` composition + the evidence floor |
| `ScoreTracker.Tests/ApplicationTests` | capture writes un-flagged rows with a percentile; the new flag; title-delta persistence + per-session aggregation (min-old/max-new across batches); rank stamping per mix (P1 combined only); **the rank milestone does not mint on the undo path** (empty change set, value falls); the snapshot-sealed refresh; the rebuild consumer |
| `ScoreTracker.Tests.Components` | hero at each state (fat, thin, no communities, insufficient skill evidence, **undone-session deep link**); dialog scope switching; the single-community picker rule; the history table with and without `ScoreSession` enrichment. ⚠ bUnit takes **one render per test** — `TestContext` refuses new services after the first resolve |
| `ScoreTracker.Tests.Integration` | every new EF read gets a fact — a mocked-repo component test cannot catch an EF translation failure (the lesson from `GetPlayerTimeline`) |
| `ScoreTracker.Tests.E2E` | `PlayerSessionsTests` exists and must be reworked for the new page |

Localization: every new key in **all nine locales**, inserted in alphabetical position, never
appended — including `en-ZW`, whose values use only the Murloc alphabet.

### 4.6 Build order

Each commit green on the fast suites.

| # | Commit | Layer |
|---|---|---|
| B1 | ✅ Migration + entity/record fields + DATABASE-SCHEMA rows | Data / PlayerProgress |
| B2 | ✅ Contract queries: attempts (ScoreLedger), placement estimates (OfficialMirror), community peer scores (Communities) — additive, nothing consumes them yet | verticals |
| B3 | ✅ Capture: per-score percentile, attempts, placement flag, title-delta persistence | PlayerProgress |
| B4 | ✅ Estimated PUMBILITY rank + `OfficialPumbilityRank` milestone + the snapshot-sealed re-estimate consumer | PlayerProgress |
| B5 | ✅ `SessionBreakdownBuilder` — one session assembled, `ScoreSession` joined on as enrichment. **Landed in Web, not PlayerProgress**: its pieces span four verticals and none can reference all four | Web |
| B6 | ✅ `ChartLeaderboardDialog` + generalize `ChartLeaderboardSection` into its World scope | Web |
| B7 | ✅ The page: hero components, history table, skill focus service, l10n ×9 | Web |
| B8 | ✅ Discord card: the placement caption on a score row, the estimated-rank line in the stats block | Communities |
| B9 | ✅ Admin backfill button + `RebuildLatestSessionsConsumer`, which replays through the LIVE pipeline rather than adding a second capture path | Web / PlayerProgress |
| B10 | ✅ E2E rework, docs sweep, this doc | tests / docs |

### 4.7 What the build changed about the design

Three things the reference graph decided rather than the design:

1. **The two capture reads are Domain ports, not contract queries** (§2.3 predicted the undo
   coupling but not this). PlayerProgress sits UPSTREAM of both ScoreLedger and OfficialMirror
   (`OfficialMirror → ScoreLedger → Communities → PlayerProgress`), so a contract query from
   capture into either closes a reference cycle. `IScoreAttemptReader` and
   `IOfficialPlacementReader` are each a thin dispatch onto the owning vertical's own query, so
   the rules still live in one place. Same escape hatch as `IDiscordFeedReader`.
2. **B5 is a Web service, not a vertical query**, for the same reason — its pieces span
   ScoreLedger, PlayerProgress, Communities and Catalog.
3. **A Phoenix difficulty title is scoped to a LEVEL, not a (type, level) folder.**
   `PhoenixDifficultyTitle.CompletionProgress` accepts any chart at the level, single or double.
   So a bar's scope is `21`, never `S21`/`D21` — ⚠ **the approved mock labels them S21/D23 and
   is wrong on this point.**

---

## 5. Responsive

The shell's own ladder, not new breakpoints — [static-shell.md §11](static-shell.md).

- **960** (Mud `Md`, the shell's desktop/mobile switch) stacks the ceremony band.
- **1280** (`Lg`) is the nav's Tier-Lists fold; this page needs nothing at it.
- The squarish rule the More sheet uses — `(min-aspect-ratio: 1/1), (min-width: 700px)` — is a
  **nav** rule this page inherits for free.
- ⚠ **`max-height: 520px` is the rule the width ladder cannot express.** A landscape phone is
  **844 wide × 390 tall**; the width rule stacks the ceremony at 844, which is exactly backwards
  when height is the scarce axis. The height rule compresses the vertical budget **and un-stacks
  the ceremony back to two columns**. It keys on height, *not* aspect ratio: a fold unfolded
  (~900×1000) and a landscape tablet (1024×768) are both squarish or wider and must **not**
  compress.
- The page registers **no page dock** — it is a reading surface, and an undocked page gets the
  unchanged shell (UX rule 10).

## 6. Honesty boundaries

Three things this page must keep saying plainly, because each is a place a confident number
would be a lie:

1. **Attempts data begins 2026-07-30** and is only as complete as the player's import cadence.
2. **Official numbers are estimates and carry a date.** Both the chart placement and the
   PUMBILITY rank are computed by placing a current value into a board that is up to a week old
   — always `~`, never a bare `#`, and always with the board's date. The rank additionally
   depends on our PUMBILITY formula matching theirs: exonerated on Phoenix, still guessing
   B-and-below on Phoenix 2.
3. **The new sections light up going forward.** Older sessions render thinner, and the backfill
   deliberately reaches exactly one session back.
4. **A session can be undone out from under a link.** The Discord card outlives the session it
   points at, so the page says "this session was undone" rather than rendering an empty hero.
5. **The competitive readout and the PUMBILITY gain begin 2026-08-08.** Both are forward-only and
   there is no backfill (D34), so an older row shows a bare ⬆ and no gain pill. That is the
   correct rendering of "we did not measure this", not a gap to paper over.
6. **The Phoenix 1 delta is measured against Phoenix 1 as it stands now** (D35), not as it stood
   during the session. Phoenix is effectively frozen, which is what makes that acceptable rather
   than merely convenient.
7. **"Still calculating" is a guess with an expiry** (D38). The page cannot currently distinguish
   *capture is running*, *capture failed* and *this session earned nothing* — so it only claims
   the first for two minutes, then stops claiming anything.

⚠ **The undo lesson, for anything that rebuilds a record from history.** A returning song carries
one ChartId across Phoenix and Phoenix 2, so `GetChartHistories` is **cross-mix by design** —
reclear detection depends on it. Any replay reconstructing one mix's record must filter by mix
first. `Classify` and `RebuildLatestSessionsConsumer` always did; `UndoScoreSessionHandler` did
not, and wrote players' Phoenix 1 scores in as their Phoenix 2 records. Nothing self-corrected,
because acquisition may only *raise* a record — so the wrong, higher number stuck and every later
import of the real score journalled as not-best and rendered as "Played" (fixed 2026-08-08).

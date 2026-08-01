# Session Breakdown (`/Player/{id}/Sessions`) — design

> **Status: DESIGNED, NOT BUILT** (workshopped 2026-08-01). Supersedes the "Recent Scores
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
| D10 | **Score colour is the competitive cohort, not the community.** ±0.5 competitive is large enough (~90–120 players/chart) for a percentile to mean something; a 7-person club board is not. The percentile always prints beside the colour (UX rule 8). |
| D11 | **No cohort ⇒ no colour and no explanation.** Co-op charts (the cohort is Singles/Doubles only) and charts >5 levels below competitive (capture already skips them) render in plain ink with nothing said. Owner: a "co-op has no competitive cohort" disclaimer *"is going to confuse more than clarify."* |
| D12 | **`ScoreQuality90` (📊) is untouched.** It stays the anonymous ±0.5 percentile flag it has always been, with its captured history and its Discord caption intact. Community Peers is purely additive. |
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

### 2.3 Skill focus

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
`SessionAllPlays`, `SessionHistoryTable`.
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
| `ScoreTracker.Tests/ApplicationTests` | capture writes un-flagged rows with a percentile; the new flag; title-delta persistence + per-session aggregation (min-old/max-new across batches); rank stamping per mix (P1 combined only); the snapshot-sealed refresh; the rebuild consumer |
| `ScoreTracker.Tests.Components` | hero at each state (fat, thin, no communities, insufficient skill evidence); dialog scope switching; the single-community picker rule; the history table. ⚠ bUnit takes **one render per test** — `TestContext` refuses new services after the first resolve |
| `ScoreTracker.Tests.Integration` | every new EF read gets a fact — a mocked-repo component test cannot catch an EF translation failure (the lesson from `GetPlayerTimeline`) |
| `ScoreTracker.Tests.E2E` | `PlayerSessionsTests` exists and must be reworked for the new page |

Localization: every new key in **all nine locales**, inserted in alphabetical position, never
appended — including `en-ZW`, whose values use only the Murloc alphabet.

### 4.6 Build order

Each commit green on the fast suites.

| # | Commit | Layer |
|---|---|---|
| B1 | Migration + entity/record fields + DATABASE-SCHEMA rows | Data / PlayerProgress |
| B2 | Contract queries: attempts (ScoreLedger), placement estimates (OfficialMirror), community peer scores (Communities) — additive, nothing consumes them yet | verticals |
| B3 | Capture: per-score percentile, attempts, placement flag, title-delta persistence | PlayerProgress |
| B4 | Estimated PUMBILITY rank + `OfficialPumbilityRank` milestone + the snapshot-sealed re-estimate consumer | PlayerProgress |
| B5 | `GetSessionBreakdownQuery` — the page's single read | PlayerProgress |
| B6 | `ChartLeaderboardDialog` + generalize `ChartLeaderboardSection` into its World scope | Web |
| B7 | The page: hero components, history table, skill focus service, l10n ×9 | Web |
| B8 | Discord card: the placement caption on a score row, the estimated-rank line in the stats block | Communities |
| B9 | Admin backfill button + consumer | Web / PlayerProgress |
| B10 | E2E rework, docs sweep, this doc flipped to implemented | tests / docs |

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

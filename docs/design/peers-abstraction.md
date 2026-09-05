# Peers — who your scores are measured against, and how that paints them

> **Status: DECIDED and BUILT (2026-09-05).** Workshopped in three rounds on 2026-09-05 (mock:
> <https://claude.ai/code/artifact/d55f9dd3-24d8-432e-b2ef-8209fb2a9c7c>, label "Round 3"), owner
> rulings D1–D24 below, built on `claude/peers-score-coloring-2e210e`.
>
> This document started life (2026-08-16) as a drafting note about an abstraction the site kept
> reinventing. The player-page work left the visibility seam behind (§5); this pass makes the
> *peers* half real: a player chooses who their peers are, once, on `/Account`, and every surface
> that shows **their own scores** colors them by their standing among those peers and says, on
> tap, exactly who those peers were.
>
> Owner's framing, 2026-08-16: *"Rivals is a subset of a repeating system of 'who are my peers'.
> Communities IS a language factor in 'what pool can my peers pull from'. Rivals is a competitive
> pool, Competitive Level peers is a competitive pool, PUMBILITY peers. Then basically there's an
> optional tier of 'reduce to community members'. EVENTUALLY I'm going to make that abstraction a
> reality and let players select what their primary peer group is (which is used for generic
> score comparisons)."*

---

## 1. Two questions the site keeps conflating

Every surface that puts another player's score next to yours is answering one of two questions,
and they are not the same question:

| Question | It is about | Vocabulary |
|---|---|---|
| **Who may I look at?** | consent — grants a player has made, explicitly or by joining something | public profile, shared user-created community, a rival edge onto them, being yourself |
| **Who am I measured against?** | comparison — a *pool* of players whose numbers mean something next to mine | rivals, players near my competitive level, players near my PUMBILITY, my community |

The first is **visibility** — a published Domain port (`IPlayerVisibilityReader`) implemented in
Rivals since the player-page work. The second is **peers**, and it is now a published Domain port
too (`IPeerStandingReader`, §4), implemented in the same place for the same reason: Rivals is the
one vertical that can already see Identity, Communities, PlayerProgress and its own edges.

## 2. Locked decisions

Owner rulings from the 2026-09-05 workshop, in his words where quoted.

| # | Decision |
|---|---|
| D1 | **A player picks their peers on `/Account`, in one dialog, and the choice is the union of what they tick**: Rivals · Competitive level · PUMBILITY peers · any of their communities (regions included, and World). *"Your final peers list is a union."* |
| D2 | **One setting, sitewide** — not per mix, not per chart type. Each source resolves per mix and chart type at read time; a source that is empty on a mix (PUMBILITY peers on Phoenix 1) contributes nothing and the dialog greys it as *Phoenix 2 only*. |
| D3 | **Display logic only.** *"Stuff like personalized tier lists or suggested charts still should use whatever peers they have since we calibrate those to peer group for accuracy. We are only affecting display logic, not calculative logic."* The projection cohorts (`ScoreProjector`), the personalized tier-list blends, the PUMBILITY page's own peer group and the Pumbility Push goal keep their calibrated pools. Only the **standing** that colors a score — and the surfaces that print it — follow the setting. |
| D4 | **Hot Streak follows the selected peers.** Its "beat X% of peers" bar reads the same standing the colors do; it was the one calculative-looking consumer the owner moved to the display side. |
| D5 | **Discord cards and community feeds are untouched**: they keep the ±0.5 competitive cohort captured at import. *"Discord notifications we'll solve in a future session."* `HighlightCaptureSaga`, `HighlightDetail` and `CommunitySaga.PeerCaption` do not change. |
| D6 | **The Sessions page colors live**, against the peers as they stand now, not the cohort captured at import. An old session recolors as your peers change; that is the correct reading of a preference. The captured percentile stays on the row for the Discord card and the ScoreQuality90 flag. |
| D7 | **World is a community you can tick.** Every account belongs to it; ticked, your peers are everyone on the site with a passing score. Regions are your country's community, listed with a *Region* tag. |
| D8 | **Board-only rivals are rivals.** *"Those are, for all intents and purposes, rivals."* A ghost's standing comes from the weekly official board (`RivalScoreReader`), so a ghost counts only on the charts the mirror publishes and only when they placed; the popover marks those lines with the mirror's asterisk and as-of date. No opt-out checkbox: *"just always include them."* |
| D9 | **Only passes enter the ladder.** *"We shouldn't use broken scores from peers for this, should all be passes."* A peer's broken attempt counts them among the ones who have not passed the chart — which the popover prints — never as a score to rank against. Both ledger reads the cohorts use already excluded broken attempts; the new `GetBrokenBests` read is what lets the popover count them. |
| D10 | **You are never your own peer, but a place keeps you in the denominator.** The captured cohort's D27/D28/D30 rules ([session-breakdown.md](session-breakdown.md)) carry over unchanged: `#6 of 94 peers` where 94 = the 93 peers who passed it plus you; a Perfect Game prints how many peers share it; a chart no peer has passed renders in plain ink and says so only in the popover. |
| D11 | **The popover replaces the hover tooltip everywhere a standing shows.** *"Switch the 'tooltip' for X of Y peers to a popover so we can use 'on click' instead of 'on hover'."* Tap the score or the standing text: headline in the score's color, *You beat N% · M more haven't passed it (K broke)*, then one line per source, then the chart. Compact tier cards stay one tap to details. |
| D12 | **Each source line opens that source's own existing board** in the chart details dialog — Rivals, Community (that club), Region, World, Competitive Peers, PUMBILITY peers. **No "your peers" board scope**: *"People can use Rivals if they want to build a better custom leaderboard. Otherwise we're just building a 2nd rivals system."* |
| D13 | **The popover leads with who has not passed it, not with the top score.** *"Get rid of 'top score' in the popover, replace it with a breakdown of how many of your peers haven't even passed the chart."* |
| D14 | **Nine color systems, one radio.** Peer standing · judgement spectrum (today's rarity ramp, the default); Peer standing · classic (the retired Raider.io ladder, hues retuned, **pink on top**); Peer standing · letter-grade metals (below-A green → A copper → AAA silver → S gold → SSS ice → SSS+ at the top 1%); Peer standing · podium (gold #1, silver #2, copper #3, plain below — *"Medals for a place, not a share"*); Peer standing · single hue (the mix primary from dark to bright, ordered by lightness alone); Peer standing · result screen (the judgement colors literally, Miss red at the bottom — the one ladder that starts red, opt-in only); Peer standing · three steps (plain / gold / ice); Actual letter grade; No color. *Actual plate* was proposed and cut: *"no plate."* |
| D15 | **Glow is a threshold signal, not a spectrum.** *"The color is the spectrum."* One radio rule — Perfect Games only · Top N places · Top N% · Off — and one strength for whatever it lights. **Off switches off the Perfect Game glow too**: *"having PG only lets them opt back into PGs."* The three-step glow ramp that used to order the rarity bands retires from score coloring; the printed standing is the second channel (UX rule 8). Default: Top 10%. |
| D16 | **"#1" became "Top N places."** *"Make #1 into configurable 'Top X' (non percentile)."* Ties share a place, so a Top 1 rule lights every tied first. |
| D17 | **The tier-card score is its own tap target.** The Comfortable card's head strip opens details; the score inside it opened details too, and its `ScoreBreakdown` rendered with the tooltip off (the stacked grade/plate layout needs bare children), so the standing was unreachable. The score now stops propagation and opens the popover; the jacket and the name keep opening details. Table density gets the popover on its Better Than cell. |
| D18 | **The Account Stats widget lists your peers**, nearest competitive level first, capped at 25 as today, each row tagged with why they are a peer (RIVAL, community initials, ±0.5, PMB). Board-only rivals close the list with a BOARD tag and no level. *"Match players on"* keeps choosing which level the rows print and sort by; who is on the list comes from the setting. |
| D19 | **Someone else's sessions page colors by the competitive default.** The peer choice is a personal preference and its rivals and communities are the owner's to see; a public player's page, viewed by anyone but them, reads the ±0.5 band with no setting. |
| D20 | **Defaults for a player who never opens the dialog** reproduce today's page: Competitive level ticked alone, judgement spectrum, glow at Top 10%. |
| D21 | **Boards rank passes first.** The chart boards ranked every recorded score, so a broken 990k sat above a passing 950k and a source line's `#2 of 5` disagreed with the six-row board it opened. Passes rank first; broken attempts follow, still drawn with the broken grade. |
| D22 | **The implementation lives in Rivals**, not a new vertical — *"Rivals."* — under the same "until a Peers vertical exists" note as the visibility reader. |
| D23 | **American spelling.** Color, colored, customize. Existing British keys are fixed only where this work already replaces them. |
| D24 | **The `ScoreQuality90` flag, `HighlightDetail.PeerPercentile` and everything captured at import stay as they are.** They are the Discord card's data and the history's truth; the live standing is a separate read. |

## 3. What a peer pool is

A **peer pool** is a rule that, for a player, produces a set of other players to compare against.
Four exist, and D1 makes them tickable together:

| Source | Rule | Read through |
|---|---|---|
| **Rivals** | the players you chose — site accounts and board-only tags | `IRivalRepository` + `RivalSubjectResolver`; ghost standings via `RivalScoreReader` |
| **Competitive level** | players within ±0.5 competitive level of you, per chart type, on the half-level bucket the cohort cache already shares | `IPlayerStatsReader.GetPlayersByCompetitiveRange` |
| **PUMBILITY peers** | Phoenix 2: players whose pool of the type sits within 500 below and 250 above yours, each holding a full pool of it ([pumbility-overhaul.md](pumbility-overhaul.md) D53) | `GetPumbilityPeersQuery` |
| **A community** | its members — a club you joined, your country, or World | `ICommunityReader.GetMembers` |

The calibrated pools the calculative features use (D3) are *not* these — the projection draws its
own band, the tier-list blend its own — and this document does not touch them.

## 4. The model

### 4.1 Domain

- `IPeerStandingReader.GetStandings(userId, mix, chartIds, selection?)` → one `PeerStanding` per
  chart the subject holds a passing score on. A null selection means "the subject's saved one";
  a caller that must not use the subject's preferences passes `PeerSourceSelection.Default` (D19).
- `PeerSourceSelection` — the four flags plus community ids, `Parse`/`Serialize` on the
  `Universal__PeerSources` UI setting (packed like `ShareCardOptions`, unknown tokens ignored),
  `Default` = competitive alone (D20).
- `PeerStanding` — `PeerCount` (the union), `Passed` (peers with a pass), `Better`, `PerfectGames`,
  `Broke`, one `PeerStandingSource` per ticked source (kind, name, members, passed, better, how
  many came off the official board), and the mirror's as-of instant. `Cohort = Passed + 1`,
  `Place = Better + 1`, `Percentile = (Cohort − Better) / Cohort` — tie-inclusive, the established
  `Ranking` semantic, 1.0 = first.
- `IScoreReader.GetBrokenBests(mix, userIds, chartIds)` — the one new ledger read (D9/D13).

### 4.2 Rivals

`PeerStandingReader` implements the port and handles three contract queries:

- `GetPeerStandingsQuery(mix, chartIds, subjectUserId?)` — the rich read every Web surface uses.
  Subject defaults to the viewer; a subject who is not the viewer gets `Default` (D19).
- `GetMyPeerRosterQuery(mix, dimension, take)` — the widget's list (D18): the union, visibility
  through `IPlayerVisibilityReader`, levels through `IPlayerStatsReader.GetStats`, ghosts appended.
- `GetPeerSourceCatalogQuery(mix)` — what the Account dialog lists: every source the viewer could
  tick, with its member sets per chart type, so the dialog's "your peers right now" tally is the
  same union the reader would compute.

`PeerStandingCalculator` is the pure arithmetic (§4.1's rules), unit-tested on its own. Caching:
the competitive band per mix, type and half-level bucket for an hour (shared across viewers, the
2026-07-10 lesson); community members per community for an hour; the peers' scores per viewer,
mix, selection and chart for an hour. The subject's own bests are always read fresh, which is the
split the old ranking saga used and the reason a fresh import recolors immediately.

### 4.3 PlayerProgress

`GetPlayerScoreQualityQuery` and `GetChartScoreRankingsQuery` retired with their handlers;
`ScoreQualitySaga` keeps `GetCompetitivePlayersQuery` (the board's Competitive scope).
`GetCompetitiveNeighborsQuery`, its record and EF handler retired with the widget's old list.
`RecommendedChartsSaga`'s Hot Streak reads the port (D4). `CohortScoreProvider` stays for the recap.

### 4.4 Web

- `ScoreColorSettings` (`Universal__ScoreColors`): the color system and the glow rule with its
  threshold; `ThemeScales.ScoreStyle(standing, isPerfectGame, grade, settings)` is the one place
  band cutoffs and the glow rule live. Two token groups join `MixThemes`: `--classic-1..7`
  (mix-invariant, the retuned Raider.io ladder) and `--hue-1..6` (per mix, six lightness steps of
  the primary). Podium, grade metals, result screen and three steps reuse the plate, judgement and
  rarity tokens.
- `PeerScore` — the one component for "your score, colored by your standing": wraps
  `ScoreBreakdown`, applies color and the single glow class, prints the standing text, opens
  `PeerStandingPopover` on click, and stops the click there. `ScoreBreakdown` no longer takes a
  ranking. Hosts: the Sessions rows and highlight cards, the tier-list card and table, the chart
  details dialog, the chart page's *Your best*, the upload results table.
- `PeersAndColorsDialog` on the Profile tab, with the summary card that opens it.
- The Account Stats widget's roster (D18); the chart boards' passes-first order (D21).

## 5. What the player-page PR left behind, and what this one does with it

- **Visibility stayed a Domain port**, implemented in Rivals; the roster query uses it (D18).
- **The head-to-head engine stays in Rivals**, still earmarked for the Peers vertical.
- **Nothing is named "peer" that isn't one.** The search population and the page gate are
  visibility; *peer* is the union a player ticked.

## 6. Open, on purpose

1. The Discord card's cohort (D5) — a future session.
2. Whether the peer group should ever be per mix (D2 says no for now).
3. A `ScoreTracker.Peers` vertical: the reader, the visibility reader and the head-to-head engine
   would move together. Not scheduled; D22 parks them in Rivals.

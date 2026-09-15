# Hardmode

A second PUMBILITY board, priced by the same formula over a different chart set: the rarest slice of
every folder — the charts almost nobody holds in a top 50. Phoenix 2 only. Lives as a fourth tab in the
PUMBILITY section (`/Pumbility/Hardmode`) while we find out whether the community bites.

The point is not a second skill ranking. It is a **reason to go play the charts nobody plays**, and the
board is shaped so that going and playing them is what moves you up.

---

## 1. What qualifies

Once a weekend, every folder (chart type × level) on Phoenix 2 gives up its rarest charts.

A chart's rarity is a **weighted hold count**: for every player with a full 50-chart pool, a chart scores
`51 − slot` where `slot` is its place in that pool — 50 points at slot 1, 1 point at slot 50. A chart
earns that credit in each of the three pools it can sit in (Combined, Singles, Doubles), and the holder
count beside it counts each player once.

**The cut** (D1): `max(unheld, min(25, ⌊folder ÷ 4⌋))`, where `unheld` is how many of the folder's charts
no full pool holds at all. So a folder gives up 25 charts, or a quarter of itself if that is fewer, or
**every unheld chart** when there are more of those than the cut would otherwise take. A folder of four
charts or fewer gives up **at least one** (D2) — without that, ⌊2 ÷ 4⌋ = 0 silently excluded S26 entirely,
which is to say it excluded *1948*.

D2 was *exactly* one until 2026-09-12, which made it contradict the line above it: a five-chart
folder nobody holds gave up all five, while a four-chart folder nobody holds gave up one and silently
dropped three. It is a floor now, not a cap — at least one, and never fewer than the unheld rule
would take.

The owner's original line on this was "Paradoxx should definitely NOT be in this board", which the
cap was assumed to be protecting. It was not: measured on the prod-synced database, **Paradoxx S26 has
151 scorers** (13 site, 138 board) against **1948 S26's 27** (3 site, 24 board), so 1948 is the rarer
of the two by a wide margin and the cut takes it on merit. The census's own output agrees — 1948 S26
is on the list with 19 holders and Paradoxx is not. Paradoxx cannot be *unheld*, so the revised rule
cannot promote it. The folder this actually reaches is **D28**, where the four hardest doubles charts
in the game have between one and four scorers each.

**Ordering and ties** (D3): weighted points ascending, then **scoring level descending**, then name.
94 doubles charts and 19 singles charts at level 20+ carry no usable `ChartScoringLevel`, so a missing
scoring level stands in as the chart's own nominal level rather than sorting last — a folder *is* one
level, so that is the neutral position, and two unrated charts then fall through to the name. Chart level
is not a tiebreak dimension of its own (it is constant inside a folder); it is only what a missing scoring
level reads as. Exact-points ties at the cut line are rare; this exists so the list is deterministic,
not because the order carries much.

**Every level PUMBILITY pays for is in** (D4, floored by D16). There is no level-20 floor and no
level-17 one; there IS a level-10 one, because Phoenix 2's formula pays **zero** below 10 and a chart
that cannot be worth anything is not an opportunity (owner, 2026-09-12). The census asks the mix's own
scoring configuration rather than testing the number, so the rule stays true if a mix ever pays
differently.

Leaving them in was not a rounding error. Nobody can be credited with holding a chart worth zero, so
every sub-10 folder read **100% unheld**, the cut rule opened all the way, and the board offered whole
folders as the rarest charts in the game — for 0.00 a play. A player who had actually passed one
still saw *No Score Yet*, because the pricing filters the same zero out. The measured figures below
were taken on a level-10-floored universe, which is now what ships.

The census below level 20 is decided by a small group and says so on the
page — see §6.

On the measured 2026-09-06 population the list comes out at **1,211 charts**, 955 of them below level 20.

## 2. Who votes

Both populations, together, weighted identically (D5):

- **PIU Scores accounts** with a full 50-chart pool of the relevant type — 252 on Phoenix 2.
- **Official-board players** whose top 50 can be reconstructed from the mirrored per-chart boards — 1,216,
  deduped against the accounts they are linked to so a linked player votes once, through their site
  records.

A partial pool does not vote (D6): its slot 1 would carry 50 points off three charts. This is why a chart
can read **"held by nobody"** on a page where the viewer is holding it — the caption's tooltip names the
population.

⚠ The mirrored chart boards only exist at level 20 and up, so a board player's reconstructed pool is
level-20-plus by construction. On Phoenix 2 they are the large half of the electorate, which is one of the
two reasons the sub-20 census is thin.

## 3. The number

Your Hardmode PUMBILITY is the same formula (`ScoringConfiguration.PumbilityScoring(Phoenix2)`) over your
best qualifying charts, in three pools exactly as the official boards split them (D7):

| Pool | What it sums |
|---|---|
| Combined | your best 50 qualifying charts, either type |
| Singles | your best 50 qualifying singles |
| Doubles | your best 50 qualifying doubles |

**There is no pool-completeness gate** (D8) — no PUMBILITY board has ever had one. A partial pool simply
scores lower, which on this board is the mechanic rather than a defect: day one the average account holds
**7.6** of the 1,211, and the way up is to go and play more.

Measured consequence worth carrying: once a pool fills, a Hardmode total is **93–96%** of the same
player's real PUMBILITY, so the endgame board is close to the PUMBILITY board. The interesting phase is
the year spent filling pools, during which the board reorders hard — the site's number-one Phoenix 2
player sits at **#10** on day one with 25 qualifying charts played.

## 4. Two boards, not one

"PIU Scores" and "Official Boards" are separate tabs over the same three pools (D9). The top of the
official boards plays everything and would occupy the same places on both, and a board player has no site
profile to open — their rows carry the `*` mark the rivals and chart boards already use for mirrored data.

Board rows read: rank (coloured by percentile through the rarity ramp), player, playstyle chip, **Held**
(how many of that pool's 50 they hold) and one figure column, **PUMBILITY** — which on this page *is*
the Hardmode pool. The grid is `olb-grid-row` with the standard column template, so it sheds Held at
600px and the playstyle chip at 500px.

The board deliberately does **not** print the player's ordinary PUMBILITY beside it (owner,
2026-09-12). It did at first, and setting the two side by side invites reading them as rival measures
of the same player when one is a strict subset of the other: every qualifying chart is a chart ordinary
PUMBILITY already chose its own fifty from, so a Hardmode pool can only ever be **smaller**. Dropping
the column also spares the Official Boards tab a rating-board read on every load.

A pool total is the best **fifty** of that pool and nothing else. The first build of the weekly reprice
summed every qualifying chart an account had scored, which is not a pool but an unbounded count of
effort; it put the top of the board at 45,379 against that player's real PUMBILITY of 15,859 — the
impossibility above, printed. The held count hid it by being capped at fifty rather than measured, so
every inflated row still read "50 / 50". The site half and the page now price identically, and a test
asserts they agree on the same records.

**A private account is on its own board and nobody else's** (D17, owner 2026-09-13). The board
shipped with no visibility filter at all, which on the measured population put **72 of 307** rated
accounts on a list they had opted out of — three of them in the top ten — with their name, avatar,
country and a link to a profile that then refuses to open. Every other roster on the site already
had the rule: the peers roster one page over drops private peers and says how many, the title
drawer keeps "a private profile stays out of the list entirely", the World chart board is the World
community's breakdown and a player who hid their profile is not on it. Hardmode was the outlier.

The viewer's own row is the exception, and deliberately: the board is the page's answer to *where
do I stand*, and a board that hides you from yourself cannot give one. So the read keeps
`IsPublic OR UserId = viewer` — the same predicate for the rows and for the standing strip above
them, which is what makes "#6 of 236" and the list agree by construction rather than by care. The
places renumber over what the viewer may see, so a private player is #6 on their own screen and
absent from everyone else's. Below the board a line says how many accounts are private and not
listed, in the peers roster's own wording — the field shrinking silently is the part that would
otherwise mislead.

The **census** is untouched: a hold count is a cohort statistic, and the site's rule is that
cohorts keep private players, because dropping them disagrees with every count already printed
(the same reasoning that keeps them on Competitive Peers). What is private is who you are, not
that someone holds a chart.

The Official Boards tab never had the leak — `OfficialPoolReader` drops every board player linked
to a site account, so those rows are unlinked piugame tags. One residual was worth closing: a
player who links *after* a Sunday rebuild keeps a stale row whose `UserId` is now set, which lit
`HubPlayerChip`'s "Linked to a site account" tick. That tick is mirrored data a private account
never published — the chart leaderboards already drop it — so the board read resolves the link and
keeps the id only when the account is public.

## 5. Titles

The existing Phoenix 2 ladders, asked of the Hardmode pool instead of the record book (D10) — the
`[P.B]` gem rungs on Combined, `[S]`/`[D]` on the per-type pools, drawn by the Breakdown page's own
`PumbilityTitleRails` with no new thresholds. About 30 of 301 accounts clear `[P.B] BRONZE` today, so
nearly every rail reads *not started* — which is the honest picture of a board nobody has played yet, and
the `pmb-ask` row beside it ("BRONZE asks 200.00 · your charts average 303.57") says exactly what is
missing. Only the ladder for the pool on screen is drawn (owner, 2026-09-12) — all three at once put a
Combined threshold beside a Singles number.

## 6. The sub-20 disclaimer

A Phoenix 2 chart is worth at most `base × 1.52`, and a player can only hold it if their own fiftieth
chart is worth less than that. That ceiling collapses the electorate going down: **1,412** pool holders
could hold an S22, **318** an S17, **ten** an S10.

So every folder at level 17 and below carries a warning in the qualifying-charts list (D11): *few players
hold a chart this low in a top 50, so this folder is decided by a small group — expect the list to change
substantially week to week as players join or their PUMBILITY climbs.* The threshold is the level, not a
computed electorate, because a fixed number is what a reader can check against the folder they are
looking at.

## 7. Card states

Three states in the qualifying list, in the site's owner-locked border language (D12):

| State | Border | Why |
|---|---|---|
| In your Hardmode pool | **solid `--rarity-gold`** | the share card's existing "Top 50 — combined" boundary; the same fact, the same colour |
| Scored, outside your fifty | **solid `--mud-palette-success`** (`tier-chart-card-pass`) | you cleared it and it does not count here |
| No score yet | default | the opportunity |

Gold beats green where both apply, matching the share card's precedence (Top 50 before Pass), and
**To-Do beats both** (owner, 2026-09-12: it "overwrites other boarder colors"). Every other state
reports something the record already knows; To-Do is the one the player put there, so a flag that a
pass or the pool's own gold could hide is a flag that does not work. It sits at the top of
`TierListChartCard.StateClass`, ahead of the custom states as well as the record's. The
qualifying list sections in that order reversed — **Not played yet → Scored, outside your 50 → In your
Hardmode pool** — so the opportunity is on top.

## 8. Shape

The reference graph decides most of this. `ChartIntelligence → Catalog, Data, Domain` only, and the chain
is `OfficialMirror → ScoreLedger → PlayerProgress → ChartIntelligence → Catalog` — so the census sits at
the bottom and cannot see the official board data, and the board half cannot see the census. Two Domain
ports carry both directions rather than adding project references (D13).

| Layer | What |
|---|---|
| **Domain** | `IOfficialPoolReader` (board pools, implemented in OfficialMirror) · `IHardmodeChartReader` (the week's list, implemented in ChartIntelligence) · `HardmodeChartsRebuiltEvent` — in `Domain/Events/` because its consumers live in verticals that cannot reference the publisher |
| **ChartIntelligence** | `HardmodeCut` (the rule) · `HardmodeCensusSaga` (the weekly sweep) · `HardmodeChart` table · `RebuildHardmodeChartsCommand` / `GetHardmodeChartsQuery` |
| **PlayerProgress** | `HardmodeSaga` (site ratings, the weekly sweep AND the per-import reprice) · six `PlayerStats` columns · `GetHardmodePageQuery` / `GetHardmodeBoardQuery` — render-time reads of your own pool and the board |
| **OfficialMirror** | `OfficialPoolReader` · `OfficialHardmodeRating` table + its writer |
| **Communities** | the Discord session card's Hardmode stats lines and per-row mark (§10) |
| **Web** | `/Pumbility/Hardmode` · `HardmodeBoardSection` · `HardmodeQualifyingSection` · the gold card state · the `/Admin` backfill button · the `--hard-mark` token and the bubble glow · the session, feed and Suggested-Charts surfaces of §10 |

Everything except the list and the ratings is computed at render time from the viewer's own records
against the week's frozen list, so your number moves with your imports while the list holds until Sunday.

**The board is live; only the chart list is weekly** (D15, owner 2026-09-12: *"the chart pool
re-calculates every sunday, but the leaderboard itself should be live updated as people import"*).
The stored totals exist because a leaderboard has to be an ordered read, not because the number is
a weekly fact — so `HardmodeSaga.RepriceHardmodePool` reprices ONE account against the current list
as a failure-isolated in-process step of `HighlightCaptureSaga`, beside the rating and title steps.
The page and the board therefore agree the moment an import lands, instead of the page being current
and the board being up to six days stale. The weekly sweep still exists, and is now the thing that
repopulates every account after the LIST changes under them.

**The weekly job** rebuilds the LIST (and reprices everyone against the new one). It is its own
Hangfire cron at `0 18 * * 0`, after the Sunday 16:30 Phoenix 2 import seals (D14). Chaining off `OfficialSnapshotSealedEvent` would be stricter, but that event is
OfficialMirror's contract and ChartIntelligence cannot see it; the cost of strictness is a project
reference, and a one-week-stale board population is acceptable on a board whose whole point is weekly
stability. `/Admin` carries a one-shot button for the first run.

## 10. Announcements

Hardmode is a progress system, so it announces itself everywhere PUMBILITY does. Paragon levels on
Phoenix 1 established that PIU Scores can drive a progression of its own without the official game
minting it (owner, 2026-09-14) — the mirror is therefore deliberate and complete rather than a
partial nod.

**D18 — Hardmode announces on the surfaces PUMBILITY announces on, and skips two.** In: the session
page's milestone strips and score badges, the session history card's headline tag, the Discord
session card's stats block, and both significant-win feeds (the Community Highlights widget and the
Rivals feed). Out: the session page's **ceremony band** and the **Account Stats widget** hero, both of
which answer "what is your number" with one figure — a second headline figure there competes with the
number it is a subset of, rather than adding to it (owner, 2026-09-14).

**D19 — the mark is 💀 in red, and it is one token.** PUMBILITY's crown is gold; Hardmode's skull is
red, and the two co-occur constantly on the same row, so they must separate at a glance without being
read. Every mark paints from `--hard-mark`: badge, gain chip, strip rail, the session rail's gain
segment, feed captions, and the difficulty-bubble glow. **Mix-invariant**, like `--judg-*` and
`--life-*` — a Hardmode chart is a Hardmode chart in every theme, and the mark carries data meaning
rather than brand.

**D20 — all three pools announce.** Combined, Singles and Doubles, exactly as PUMBILITY's three do,
on the session strips and in the Discord stats block alike. *"If pumbility does all 3, we do all 3"*
(owner, 2026-09-14). The same rule settles the board standing the feed rows need: `PlayerRatingSaga`
estimates the official rank on all three boards, so Hardmode reads all three standings.

**D21 — the score badge is the literal twin.** `HardmodeTop50` means "this score sits in your Hardmode
fifty", not "this is a qualifying chart". A pool under fifty displaces nothing, so for most accounts
the badge lights up on every qualifying score — which is the same bootstrap the normal top 50 had when
Phoenix launched and D10s were legitimately in people's fifties (owner, 2026-09-14). It is a feature of
a young pool, not a defect: a player who barely touches these charts barely sees it, and a player who
does fills their fifty fast and watches it tighten.

**D22 — Hardmode rails never sit under "Titles you're working on".** That heading is a claim about
titles the player will actually earn. A Hardmode rail is the same Phoenix 2 ladder priced against a
subset, so a player past RED BERYL would read "33% to BRONZE" with nothing on the bar to say why.
Hardmode rails take their own heading directly beneath, and a dismissable pointer
(`TitleProgressPointer`, parameterized for a second instance) links to `/Pumbility/Hardmode`.

**D23 — Suggested Charts takes Hardmode as a filter, not a goal.** A checkbox narrows whichever goal
is selected to the week's qualifying charts, so Score Push becomes "improve the Hardmode scores you
hold" and Fill Gaps becomes "Hardmode charts you could pass" — five builders reused rather than a
sixth written. The right-hand column prints **rarity** ("held by N"), never a projected gain: with a
pool under fifty every qualifying chart pays its full value, so ranking by gain collapses into ranking
by level.

**D24 — the glow is never suppressed, including on this page's own qualifying list.** The obvious
objection is that a list where every chart qualifies is a wall of glow. That is the intent: *"That
page should feel like a wall of boss charts"* (owner, 2026-09-14). One rule, no scoped exceptions, no
pass-through parameter on `DifficultyBubble`. The glow defaults **on**, signed-out included, and the
account toggle lives on `/Account` → Profile.

**D25 — the private-account note under the board is removed, and its plumbing with it.** D17's
*filtering* stands unchanged — a private account is on its own board and nobody else's, and the
standing counts the same population the rows do. Only the line saying how many were left out goes
(owner, 2026-09-14), along with `HardmodeBoardRecord.PrivateAccounts`, the repository's count query
and its test assertions: a contract field nothing reads grows a second reader eventually.

**D26 — the week's list is cached, because item D24 changed who reads it.** `IHardmodeChartReader` was
an occasional read — once per Hardmode page load. A glow on every difficulty bubble makes it a read on
nearly every page on the site, at ~1,200 rows. The reader memoizes behind a `CacheKeys.Mix(...)` key (a
catalog fact that must never vary by viewer), evicted on `HardmodeChartsRebuiltEvent`, which the census
already publishes. The entry carries an explicit expiration — a two-argument `Set` silently drops the
TTL, which is fatal for a key whose only other eviction is a weekly event.

**D27 — a Hardmode session filling the Highlights section is the feature, not a blowout.** The
skull sets a `HighlightFlags` bit, `IsFlagged` is "any flag at all", and the Highlights section
draws one jacket card per flagged chart — so a twelve-chart Hardmode session shows twelve cards
where an ordinary session shows two or three. That was raised as a defect and is not one (owner,
2026-09-15): *"These are charts people are not naturally playing. By definition… If someone plays
12 charts in this pool, they are looking for hard stuff. They want hard stuff highlighted."*

The census agrees and always did. A qualifying chart gets **about half the plays** of the rest of
its folder and scores lower there (S22 943.5k against 951.9k, S24 919.3k against 932.3k, D24
944.6k against 955.7k), and the average account holds **7.6 of the 1,211** (§3). Playing twelve of
them is not an ordinary session that happens to trip a flag — it is a deliberate trip into the
part of the catalogue nobody refines, which is the whole reason this board exists. A section that
capped it, or a flag that fed the badge but not `IsFlagged`, would be machinery for hiding the
thing the feature is for.

**D28 — the highlight schema bumps on BREAKING changes only.** Adding a `WinKind`, or an optional
field, is additive: a pre-change row is a complete summary under the rules of its day, so it keeps
rendering and the 30-day purge ages it out. A bump is for a payload an old row can no longer be
read as. The two Hardmode kinds were bumped to v4 and reverted the same day (owner, 2026-09-15:
*"can we only bump schema versions on breaking changes? This is additive, not breaking"*) — the
cost is invisible and total, because the repository filters reads on the exact version and nothing
regenerates old rows, so a needless bump empties the Community Highlights widget and the Rivals
feed for every player until each next imports.

### What each surface renders

| Surface | What Hardmode adds |
|---|---|
| Session — milestone strips | Up to three strips (Combined/Singles/Doubles), red rail, `N0` totals and `PumbilityFormat.Gain` for the delta — the same precision the PUMBILITY strip beside it uses |
| Session — score badges | `💀 #N` beside the crown, and the Hardmode gain chip beside the PUMBILITY one on the score line |
| Session — title bars | Its own "Hardmode ladders" group plus the pointer (D22) |
| Session — history cards | One headline tag, **last** in the two-slot priority order, so it only claims a slot on a session where nothing bigger happened |
| Discord — stats block | One `💀` line per pool that moved; `💀` joins `👑` on the per-score rows |
| Feeds (widget + rivals) | Two win kinds — a board placement, and a ladder rung crossed |

## 9. Known limits

- **Phoenix 2 only.** Phoenix 1 would need its own census and has a different, much larger population.
- **The day-one board rewards breadth over difficulty.** With 955 sub-20 charts against 256 at level 20+,
  fifty slots are far easier to fill with level-14 charts, so accounts averaging level 14 outrank accounts
  averaging level 21 until the strong pools fill. Accepted (owner, 2026-09-12): it self-corrects, and the
  alternative was a board with one player on it.
- **A partial pool's rails read empty.** By design — see §5.
- **It is seeded across the site now** (§10): the session page, the Discord card, both highlight feeds,
  Suggested Charts and every difficulty bubble read the list through `IHardmodeChartReader`, which is
  exactly what D13 built it for.

# Peers — the abstraction, drafting

> **Status: DRAFTING. Not scheduled, not scoped, nothing here is a decision.** This doc exists so the
> idea is written down where the code can point at it, and so the work that *does* ship near it —
> the player page and site search ([player-page-and-site-search.md](player-page-and-site-search.md))
> — leaves the right seam behind instead of growing a vertical into something it isn't.
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

| Question | It is about | Today's vocabulary |
|---|---|---|
| **Who may I look at?** | consent — grants a player has made, explicitly or by joining something | public profile, shared user-created community, a rival edge onto them, being yourself |
| **Who am I measured against?** | comparison — a *pool* of players whose numbers mean something next to mine | rivals, players near my competitive level, players near my PUMBILITY, my community |

The first is **visibility**. The second is **peers**. A rival happens to be both — a consent grant
*and* a pool member — which is how the two got tangled. A community is a consent grant *and* a
filter you might apply to a pool. Neither fact makes rivals or communities the abstraction.

The player-page work makes the first question a published Domain port (`IPlayerVisibilityReader`),
implemented in Rivals for now because Rivals is the one vertical that can already see Identity,
Communities and its own edges. That is the seam: when Peers becomes a vertical, **it** implements
that port and nothing that consumes it moves. This doc is about the second question.

## 2. What a peer pool is

A **peer pool** is a rule that, for a viewer, produces a set of players to compare against —
optionally reduced by community. Three pools exist in the wild today, each grown independently
where a feature needed it:

| Pool | Rule | Where it lives now |
|---|---|---|
| **Rivals** | the players you chose (site users, or board tags for the mirror-only ones) | `ScoreTracker.Rivals` — `GetRivalScoresForChartsQuery`, the roster, the Rivals scope on `ChartLeaderboardScopes`, the sessions-page rivals section, the rivals feed |
| **Competitive-level peers** | players within ±*n* competitive level of you, per chart type | `IPlayerStatsReader.GetPlayersByCompetitiveRange` (PlayerProgress publishes it) → `GetCompetitivePlayersQuery` (the "Competitive Peers" leaderboard scope), `Domain/Services/ScoreProjector` (the PUMBILITY projection cohort — ±1 on Phoenix), `EFPhoenixRecordsRepository.GetMeaningfulScoresCount` (±0.5), `RecapPeerMatcher` (±0.25, community-mates at ±0.5) |
| **PUMBILITY peers** | players near your PUMBILITY — on Phoenix 2, gem rung ±3 within the type pool | the Phoenix 2 branch of the projection and the PUMBILITY tier-list lens ([phoenix2-implementation.md](phoenix2-implementation.md), [pumbility-tier-list.md](pumbility-tier-list.md)) |

And one **reduction** that is applied to pools rather than being one:

| Reduction | Rule | Where it lives now |
|---|---|---|
| **Community** | keep only players who share a community with you (World / Region / user-created, and which one) | Communities — `GetCommunityPeerScoresQuery` (sessions "Community Peers"), the World / Region / Community scopes on the chart leaderboard, `RecapPeerMatcher`'s strict community-first tiering, the community leaderboards themselves |

The recap matcher is the clearest existing example of the composed shape: *competitive pool,
reduced-and-prioritised by community*, picked **for** the player. It is also why the recap's
"rivals" were renamed peers internally — a rival is a pool the player builds, a peer is a pool a
rule builds ([rivals.md](rivals.md) D48).

## 3. The abstraction, sketched

Nothing below is decided. It is the shape the owner described, written out so it can be argued
with later.

- **`PeerPool`** — a named rule: `Rivals`, `CompetitiveLevel(window)`, `Pumbility(window)`, and
  whatever comes next. Each resolves, for a viewer and a mix (and possibly a chart type), to a
  set of player identities. Rivals is the only pool with *ghosts* — mirror-only members with no
  site identity — which is a property of that pool, not of the abstraction.
- **`CommunityReduction`** — an optional filter: none / a specific community / any user-created
  community. Applied to a pool, not a pool itself.
- **A player's primary peer group** — a setting: which pool (plus reduction) the site uses when a
  surface asks for "my peers" without saying which. Today every surface hard-codes its answer;
  the setting is what lets a player say *"compare me against my rivals"* or *"against people at
  my level in my community"* once, and have the generic comparisons follow.
- **Consumers** — the generic score comparisons: the leaderboard scope a chart dialog opens on,
  the sessions-page peers section, the home widgets' comparison rows, the recap's peer picks, and
  the pairwise head-to-head (which is the degenerate case: a pool of one). The projection cohorts
  (`ScoreProjector`, the PUMBILITY suggestions) are *statistical* pools tuned for a formula and are
  probably **not** consumers — the owner rules on that when the time comes.

Where it would live: a vertical of its own (`ScoreTracker.Peers`), taking the head-to-head engine
and `PlayerVisibilityReader` with it from Rivals, and reading the pools through the ports the
owning verticals already publish (`IPlayerStatsReader` for the bands, Rivals' contracts for the
edges, Communities' for the reduction). Rivals shrinks back to what it is: the chosen pool — the
edge store, blocks, invite codes, ghost resolution.

## 4. What the player-page PR does about this

Deliberately little, and each item is there so this abstraction can arrive without a rewrite:

- **Visibility is a Domain port**, not a Rivals contract. Consumers (`PlayerProgress`'s profile
  read, `Identity`'s player search, the page) depend on `IPlayerVisibilityReader`; Rivals is the
  implementation of record until Peers exists.
- **The head-to-head engine stays in Rivals and is earmarked.** It gains a user-keyed entry point
  because the player page needs one; it is a pairwise peer comparison and moves with the pool
  abstraction. It was not moved now because a second implementation of its comparable / legacy
  tie-break / margin rules is exactly the drift this codebase keeps paying for.
- **Nothing is named "peer" that isn't one.** The search population and the page gate are
  *visibility*; the word peer is reserved for pools.

## 5. Open, on purpose

Questions for whenever this is picked up — listed so nobody answers them by accident in a smaller
PR:

1. Is the primary peer group one setting or one per mix / per chart type?
2. Which surfaces follow the setting, and which keep a fixed pool (the projection cohorts, the
   recap's picks, the community leaderboards, which are the community *by definition*)?
3. Does the community reduction apply to World and Region, or only to user-created communities
   (visibility already answers "user-created only"; peers may want otherwise)?
4. Ghosts: a rivals pool has them, a competitive band cannot — does the abstraction carry
   mirror-only members, or is that a Rivals-only capability that surfaces gate on, as the
   head-to-head does today with `RivalCapabilities`?
5. Where does "reduce to community" sit relative to the pool window — filter then take the band,
   or take the band then filter (the recap matcher does the second and widens the window to
   compensate)?

## 6. Not this

- Not scheduled. Not a slice of the player-page work.
- Not a rename of the Rivals vertical.
- Not a change to how the PUMBILITY projection picks its cohort.

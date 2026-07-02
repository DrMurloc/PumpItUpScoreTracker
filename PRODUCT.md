# Product Direction

> Status: living document · Last updated 2026-06-11
>
> This is the "why and for whom" layer. For game terms see [DOMAIN.md](DOMAIN.md); for the code as it is, [ARCHITECTURE.md](ARCHITECTURE.md); for the working domain map, [CONTEXTS.md](CONTEXTS.md); for the work plan, [BACKLOG.md](BACKLOG.md) and [PHOENIX2-ROADMAP.md](PHOENIX2-ROADMAP.md).

## Mission

The arcade shows you your score once, then moves on. PIU Scores keeps what the game doesn't: a durable ledger of every score you've recorded, the insight derived from it (player ratings, progress routes, community chart knowledge), and the competition and community built on top. It serves roughly 5,000 unique players a month across ~60 countries.

The ordering matters: **ledger first, insight second, community third.** Without the scores, nothing else here means anything — the ledger was the first thing built, and keeping it good (especially keeping import working across mix transitions) is existential. Each layer feeds the next.

## Audiences

Feature and priority decisions are planned against these segments. Level ranges are rough — "score pusher" vs "pass pusher" vs "stamina player" vs "completionist" make ranges mean different things along different axes of player.

| Segment | Rough levels | Target? | What they want |
|---|---|---|---|
| Ultra casual | < ~10 | No | Light community involvement; not at a level where score analysis helps, and not looking for feedback |
| Casual | ~10–16 | Secondary | To be part of something bigger — comparing progress, belonging, not necessarily pushing themselves |
| **Casual-competitive** | ~17–23 | **Core** | Routes to improve: tier lists, title grinding, completion. This group is why chart tier lists are such a commodity in the community |
| Competitive | ~23–26 | Secondary | Routes to prove themselves against the larger community — competition as a dopamine source, not necessarily "be the best" |
| Ultra competitive | the top handful | No | Nothing from a tracker. They just play, and they know the charts better than any dataset ever will |
| **Tool makers** | n/a (developers) | Yes | Score data without building an importer |

Notes:

- **Casual-competitive is the center of gravity.** When two designs conflict, the one that serves this segment wins by default.
- **Tool makers are a real audience, invisible in page analytics.** Nobody else wants to build the importer — collecting passwords is painful and scraping is worse — so other community tools consume this site's APIs or take webhooks from its import ecosystem. That makes the import pipeline de facto infrastructure for the whole PIU tooling scene, and the API surface a public contract (see *Platform stance*).
- The two non-targets are deliberate. No onboarding features chasing the ultra-casual; no "be the very best" ladders for the top tier.

## Focus: what's core

**Core** — where the differentiating, evolving logic lives; protect and invest:

- **Score ledger & acquisition** — the system of record plus the pipeline that fills it: official-account import, OCR, CSV upload, score batching. The moat is that this pipeline exists and works.
- **Player insight & progression** — Pumbility, competitive level, titles/paragons, history, recommendations ("What Should I Play"). The core audience's improvement loop.
- **Chart intelligence** — tier lists, scoring difficulty, letter-grade difficulty, crowd votes. Community-generated knowledge about charts; the site's most-visited surface and its most-shared artifact.

**Supporting** — necessary and real, but not the differentiator: weekly charts (automated competition for the core audience), organized competition (M.o.M., qualifiers — the competitive segment), communities and Discord notifications (the casual segment's home), the chart/song catalog, the official-site mirror, UCS.

**Generic** — buy/borrow, keep thin and out of the way: identity and auth, email, blob storage, localization plumbing.

## Platform stance

The public API (score submission and import, tier lists, weekly charts, tournaments, random chart draws) is token-authenticated and consumed by third-party community tools. Score-import webhooks feed partner tools directly. Two consequences:

- **Response shapes are a public contract.** Version and deprecate; don't break. Breaking changes break other people's products.
- **The importer is community infrastructure.** Partner tools depend on it; they're also an early-warning channel when the official site changes underneath it.

Open strategic question: whether to expose a player-rating (Pumbility/stats) API. Attribution in partner tools could entrench the rating further — or give the next official copy away for free. Unresolved on purpose.

## Track record

PUMBILITY started life as this site's player-rating formula; the official game later adopted it — name included — as its rating system, and official world rankings followed the site's. We take those wins, and we plan for the pattern to repeat: the derived-insight layer is where this project leads, so the freedom to evolve ratings beyond whatever becomes official (e.g. PUMBILITY+) is worth preserving.

## Direction (as of mid-2026)

1. **Phoenix 2 is the near-term forcing function** ([PHOENIX2-ROADMAP.md](PHOENIX2-ROADMAP.md)). Scores reset at PIU's end; ours don't. Keeping the ledger continuous across the mix transition and getting import working day-one is the highest-stakes work on the books.
2. **After Phoenix 2: the rearchitecture** ([BACKLOG.md](BACKLOG.md#onion--ddd--hexagonal-rearchitecture), [CONTEXTS.md](CONTEXTS.md)) — restructure along subdomain boundaries so the core can evolve without dragging everything with it.
3. **Legacy mixes are preserved, not invested in.** XX-era pages get effectively no traffic; manual entry stays (and may extend to even older mixes over time, for the completionists), but import and analytics investment targets the live mix.

## Usage snapshot (May–June 2026)

Rounded, from 30 days of page analytics (~10k sessions). Two caveats: API traffic doesn't appear at all (the tool-maker audience is invisible here), and the long tail of per-chart and per-user pages is undercounted.

| Surface | Share of sessions |
|---|---|
| Tier lists | ~30% — two-thirds of typed views are **Pass** tier lists; views concentrate on levels 17–23 |
| Home ("What Should I Play") | ~15% |
| Phoenix score upload/import | ~10% — the single biggest page after home |
| Communities | ~10% — country leaderboards dominate (Brazil, France, Italy, …) |
| Account + login | ~15% |
| Chart browse/record | ~7% |
| Weekly charts / progression pages / tournaments / calculators | ~3–4% each — tournament traffic is spiky; nearly all of it was one event's qualifiers |
| Official leaderboard pages | <1% |

Reading it: the three core surfaces carry over half of all traffic; the tier-list difficulty distribution mirrors the audience pyramid above almost exactly; the official mirror is a data supplier, not a destination; and the audience the page data can't see (tool makers) is served by the same import pipeline the #2 page depends on.

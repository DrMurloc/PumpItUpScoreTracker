# Discord rich score notifications — design

> **Status: IMPLEMENTED** (PR #124, 2026-07-05) — all build-plan commits C0–C10 landed:
> fast suite, API wire-shape suite, integration suite, and the Sessions Playwright E2E
> green; the live Discord canary posted both sample cards to the lab channel and read
> them back with components attached. Remaining follow-ups live under "Deferred by
> design" and the legacy-consumer migrations (titles → ratings → weekly → UCS).
>
> **Revision 2 IMPLEMENTED (2026-07-05, same PR): the session snapshot** — supersedes
> the passes/upscores/digest card split below; S1–S4 all landed. The spec below is the
> as-built reference.

## Revision 2 — the session snapshot (locked 2026-07-05)

Owner direction after seeing C0–C10 live: score events were still "a big dump of scores —
unclear what was or wasn't actually meaningful." The card becomes **one message per score
batch**, shaped like the Sessions page roundup: stats that moved → achievements → only the
scores worth reading. Everything else is a count. **This becomes the ONLY score-triggered
community Discord message** — the separate passed/upscored walls and the standalone
ratings / titles / weekly messages all retire (UCS placements are a separate ecosystem,
out of scope).

Locked calls (owner, 2026-07-05):

1. **One message per session batch.** Legacy ratings/titles/weekly messages retire for
   score flows. The `NewTitlesAcquiredEvent` announcement survives ONLY for the
   `TitlesDetectedEvent` path (titles granted by the official site — no session covers
   them). Titles-completed listing caps at **10** names (owner: 3 is too few), then
   "+N more titles".
2. **Title progress = real per-title deltas** ("Expert Lv.4 82% → 86%") when nothing
   completed, computed by the title step from the batch's old→new scores, capped at 3,
   nearest-to-complete first. Not milestones — event payload only (the page has /Titles).
3. **Weekly placements fold into the card** — up to **4**, difficulty descending, and we
   *flex* them (any placement, not just #1). The separate weekly message retires. Stored
   as `WeeklyPlacement` milestones so the Sessions page shows them too.
4. **Noise floors at capture**: Singles/Doubles competitive milestones only mint at
   ≥ **0.01** change (the +0.002 lines were the poster child of the dump). PUMBILITY
   shows on ANY gain — even +1 ("which happens").
5. **💥 big gain**: the session's single biggest upscore gets a row + caption when the
   gain is ≥ **+10,000**.
6. **PUMBILITY = combined only.** "Players don't care about separate. Combined is all
   that matters (the official PUMBILITY)." Singles/Doubles PUMBILITY lines die with the
   legacy message; Singles/Doubles *competitive* stay (subject to the floor).
7. **Co-ops always show** — they can't earn S/D flags, so they get their own rule: up to
   **3** co-op rows, ordered by Pass-tier-list difficulty descending, remainder in the
   count line.

**Card anatomy (top to bottom, sections absent when empty):** header (name, `passed N ·
upscored M`, level span incl. CO-OP, avatar, **mix-brand accent stripe** — owner call
2026-07-05 reversing the earlier grade-accent decision: with parallel mixes the stripe
identifies the MIX at a glance (grades already color every row via emojis). Colors are
sampled from the official mix logos (Phoenix 2 deepened for stripe contrast) and live in `MixEnum.GetAccentColor()`: Phoenix
`#1D9BCC`, Phoenix 2 `#6CA832`, XX `#D49D3B`. The `[Phoenix 2]` textual prefix stays) → ① stats (PUMBILITY combined old→new(+diff); Singles/Doubles competitive past
the floor; never combined competitive) → ② achievements (titles ≤10 named, paragon gains
one line each with the NEW GRADE'S EMOJI — never aggregated into "+N", folder lamps every
boundary, weekly placements ≤4, per-title progress deltas ≤3 as the nothing-completed
fallback) → ③ notable scores (flagged rows first — 👑📊🏅🆕📁⬆💥 — art on the first 5,
individually rendered up to 10, then co-op rows ≤3; song name bold when flagged; `-#`
caption names each flag; everything else is `+N more: D23 ×2 …` grouped by difficulty) →
folder progress line → footer → **"See more"** link button to
`/Player/{id}/Sessions?session={id}` (public players only).

**Pipeline (what makes ① and ② deterministic):** the capture consumer becomes the
session-snapshot orchestrator. After flags + lamps it dispatches, in-process and in
order: the rating step (recalc, improver flags, rating milestones — floors applied at
minting) and the title step (completions, paragon, progress deltas), plus the weekly
placements via a WeeklyChallenge **contract** command (cross-vertical by contract, never
by reference). Only then does it publish the enriched captured event the card renders
from. `PlayerRatingSaga`, `TitleSaga`, and the weekly processor stop consuming the raw
score event — ordering comes from pipeline shape, not racing (ADR-001 doctrine). Every
step is failure-isolated: a failed step publishes with that section absent; the card
never dies.

**Build plan:**

- **S1 — snapshot pipeline (PlayerProgress).** Capture orchestrates rating + title steps
  in-process (internal MediatR requests returning their milestones/flags/deltas); raw-
  event consumers removed from both sagas; competitive milestone floor 0.01; event gains
  stats/titles/progress payload; consumer-registration tripwires updated.
- **S2 — weekly join (WeeklyChallenge).** Weekly processing moves behind a contract
  command returning placements (its raw-event consumer retires to avoid double
  processing); capture stores `WeeklyPlacement` milestones; site UI event unchanged.
- **S3 — the one card (Communities).** Snapshot builder per the anatomy above;
  passes/upscores/digest builders and `DigestThreshold` deleted; ratings/weekly
  consumers retired; titles consumer kept only for the detected-titles path;
  CommunitySagaTests rewritten.
- **S4 — samples + showcase + docs.** Canary/PoC samples to the final shape, the
  real-session showcase updated for the event shape, ARCHITECTURE.md pipeline note,
  Sessions page renders `WeeklyPlacement` milestones.
- **S5 — session backfill (owner call: "everyone has something to start from").** The
  `BackfillRecentSessions` data migration stamps SessionIds onto each player's LAST
  THREE journal clusters per mix at deploy time, using the live Session Batcher's
  8-hour-gap rule over the journal's real OccurredAt timestamps — past-midnight
  sessions stay whole, double visits split, older history keeps day-bucketing.
  Highlights/milestones stay un-backfilled (write-time truths). Ids are materialized
  into temp tables before the update (NEWID() in an expanded CTE re-evaluates per
  row); behavior is asserted by `SessionBackfillMigrationTests` against the exact
  production SQL.

Restructure the community Discord score-update announcements from plain-text messages into
structured cards built on Discord's **Components V2** layout API (containers, sections with
thumbnail accessories, separators, link buttons). This doc records the design and the decisions
behind it. Scope is the `PlayerScoresUpdatedEvent` announcement plus its companion **Recent
Scores page** (the card's link target); the other five notification types (titles, ratings,
weekly, UCS, new members) adopt the same model in later passes.

## Goals

1. Score announcements become visual cards: song art per pass, a grade-colored accent, the
   player's avatar, deep links to chart pages — instead of walls of emoji-prefixed lines.
2. Every existing pipeline invariant holds: dispatch stays behind the `IBotClient` port, Domain
   never references Discord.Net, the `#LETTERGRADE|…#` / `#PLATE|…#` / `#DIFFICULTY|…#` emoji
   token vocabulary keeps working, and channel opt-in flags (`SendNewScores` etc.) are untouched.
3. The new message model is generic enough that titles / Pumbility / weekly / UCS announcements
   can migrate onto it without another port change.
4. Graceful degradation: if structured send fails for a channel, that channel gets the
   plain-text form. An announcement never silently drops because of a rendering problem.
5. Import-sized events (an initial account import can carry ~2,000 changes in one event)
   collapse into a single digest card instead of channel spam — and the digest links out to a
   full feed page rather than trying to enumerate inline.

## Current state (what makes this cheap)

- **Discord.Net 3.18.0 is already in `ScoreTracker.Data`** — Components V2 shipped in 3.17, so
  no package or allowlist change is needed anywhere.
- The port boundary is already clean: `CommunitySaga` composes presentation strings; the
  `DiscordBotClient` adapter owns emoji-token replacement and channel fan-out with
  per-channel/per-message try-catch. The structured form slots into the same seam.
- Song art (`Song.ImagePath`) and player avatars (`User.ProfileImage`) are publicly hosted on
  `piuimages.arroweclip.se` — directly usable as Discord thumbnails, no upload step.
- Masked markdown links from the bot are already proven in production (the qualifier
  announcement links to the leaderboard page).
- Chart deep links exist for every chart: `https://piuscores.arroweclip.se/Chart/{id}` (the
  sitemap already enumerates them).

What's weak today, concretely (from `CommunitySaga.Consume(PlayerScoresUpdatedEvent)`):

- Passes are capped at 10 text lines + "And N others!", chunked around the 2,000-char content
  limit; big import sessions arrive as 2–4 disconnected messages.
- The level-progress stats (`173/210 (82.4%)`) always arrive as a **separate second message**,
  visually orphaned from the passes they belong to.
- No song art, no player avatar, no color, no links — a 997k SSS+ on a D24 renders with the
  same visual weight as an A on an S9.

## Why Components V2, not classic embeds

Both are available in Discord.Net 3.18. Embeds are the older, safer structure (author line,
one thumbnail, 25 fields, footer). Components V2 is the newer layout tree
(`MessageFlags.ComponentsV2`): text displays, sections with a per-section accessory
(thumbnail or button), separators, media galleries, all inside a container with an accent
color.

| | Classic embeds | Components V2 |
|---|---|---|
| Song art | **One** thumbnail per embed | Thumbnail accessory **per section** — art on every pass row |
| Custom emoji (`<:piu_sssplus:…>`) | Render in description/fields only — **not** in title, author, footer | Render in every text display |
| Accent color | Color stripe | Container accent color (same visual) |
| Buttons | Separate action row below the embed | Compose inside the same tree |
| Budgets | 4,096-char description, 25 fields, 6,000 total | 40 components, 4,000 chars total text |
| Constraints | — | `content` and `embeds` must be empty on the message |

The per-row song art is the feature that actually changes how the announcement reads, and the
custom-emoji restriction matters here — this pipeline's entire visual vocabulary *is* custom
emoji, and embeds would exclude it from titles and footers. **Decision: Components V2**, with
the plain-text renderer retained as the fallback path.

Costs accepted: the 40-component/4,000-char budgets (drive the caps below), and V2 messages
carry no `content` — the notification preview derives from the components, and the fallback
text must be sent as a separate message rather than riding along.

## The message design

### Passes card

One card per event (splitting only on budget overflow):

```
┌─ container — accent color = best new grade's color ────────────┐
│  ### JEWEL passed 12 charts                        [avatar]    │  ← header section,
│                                                                │    avatar accessory
│  ──────────────────────────────────────────────── separator    │
│  🎉 D24 **All passed!**                                        │  ← milestone banner band
│  🏆 D24 **All SS or better**                                   │    (folder lamps, off the
│  ──────────────────────────────────────────────── separator    │    captured event)
│  D24 **District 1**                                [song art]  │  ← flagged rows lead, bold
│  **997,821** SSS+ UG                                           │    song name, named subtext
│  -# 👑 PUMBILITY top 50 · 🆕 Folder debut                      │    caption per flag
│  ──────────────────────────────────────────────── separator    │  ← fence: flagged ↑ rest ↓
│  S20 Ugly Dee                                      [song art]  │  ← then unflagged rows in
│  **985,420** SS+ SG                                            │    noteworthy order
│  …(art while the 5 slots last)…                                │
│  +7 more: S19 ×3, S18 ×2, S17 ×2                               │  ← overflow text display,
│                                                                │    grouped difficulty counts
│  ──────────────────────────────────────────────── separator    │
│  D24 173/210 (82.4%) · S20 141/168 (83.9%)                     │  ← level progress folded in
│                                                                │    (no more second message)
│  -# Phoenix · PIU Scores                                       │  ← subdued footer line
│  [ View all recent scores ]                                    │  ← action row, link button
└────────────────────────────────────────────────────────────────┘
```

- **Header**: `### **{name}** passed {n} charts` (markdown heading), avatar as the section
  accessory. The `[Phoenix 2]` mix prefix stays **textual** in the header, exactly as decided
  in the Phoenix 2 plan — the accent color is decoration, not the mix signal.
- **Milestone banner**: folder lamps ride `ScoreHighlightsCapturedEvent.Milestones` (capture
  writes them, then publishes — deterministic, no read-back race) and open the card as their
  own band between separators, capped at 6 lines. Rating and title milestones are written by
  racing consumers and keep their own announcement messages until those migrate onto rich
  cards.
- **Pass rows**: flagged rows lead (they own the art slots and never collapse into the
  overflow line — art while slots last, individual text rows after), fenced from the rest by
  a separator; within each group the universal noteworthy rule applies — difficulty level
  desc, then scoring level desc. Two lines per row: difficulty bubble + song name as a masked
  link (**bold** when flagged); then bold score + grade + plate emoji; flagged rows add a
  `-#` subtext caption naming each flag (`👑 PUMBILITY top 50 · 📊 Top scores among peers ·
  🏅 Title progress · 🆕 Folder debut · 📁 Nearly complete folder · ⬆ Raised competitive
  level` — the Sessions page tooltip vocabulary). Top **5** rows get art (art rows are ~2×
  the height, so 5 keeps the card scannable on mobile); unflagged overflow collapses into one
  grouped line.
- **Level progress**: the same stats currently sent as a second message become a block inside
  the card, joined with `·` separators instead of one line each.
- **Footer**: `-#` subheadline markdown (Discord's small-text) with the mix logo emoji + mix
  name + product name (see "Mix logo emojis" below). V2 has no timestamp concept; the message
  timestamp covers it.
- **Button**: one link button — **"View all recent scores"** — deep-linking to this
  session's roundup on the player's page (section below; the event's `SessionId` rides the
  URL). Rendered only when the player is public (`User.IsPublic`, already loaded by the
  saga); non-public players get the card without the button. (A per-community
  "leaderboard" button stays deferred — the saga sends one identical message to every channel
  across all of the player's communities, and per-channel rendering is a bigger port change.)
- **Accent color**: the letter-grade color of the best new score, matching the site's grade
  palette. Ties the card's frame to the headline achievement.

### Mix logo emojis

A new token kind joins the vocabulary: `#MIX|Phoenix#`, `#MIX|Phoenix2#`, `#MIX|XX#`, mapped
in the adapter's dictionaries exactly like grades/plates/difficulties, used in the card footer
and anywhere else a mix name appears. The official logo assets (pulled from the live sites,
2026-07-05):

| Mix | Source asset | Branding |
|---|---|---|
| Phoenix | `https://phoenix.piugame.com/l_img/logo.png` (1428×667) | Blue wings |
| Phoenix 2 | `https://www.piugame.com/l_img/logo.png` (1642×667) | Green — "PUMP IT UP 2026 PHOENIX 2" |
| XX | `https://phoenix.piugame.com/l_img/quick_xx_logo.png` (112×66) | Pink/magenta |

The 128×128 transparent PNGs were prepared in `Downloads\discord-mix-emojis\` and the owner
uploaded them to the **PIU Scores official server** alongside the existing
grade/plate/difficulty bubbles (2026-07-05) — same management model, same rendering rules
(the bot is a member of that guild; channels that render the current bubbles render these).
The adapter dictionary entries:

```
Phoenix  → <:phoenix_logo:1523325598171398164>
Phoenix2 → <:phoenix2_logo:1523325648976875704>
XX       → <:xx_logo:1523325684259356703>
```

At inline size the
wordmark text is not legible, but the color (blue vs green) is the mix signal — the textual
mix name always sits next to the emoji, so the emoji is color-coding, not the sole carrier.
(Note from the same recon: the Phoenix 2 site also cross-promotes a separate "R!SE — Pump It
Up" product, yellow/orange branding — not a mix, just intel.)

### Upscores card

Upscores are higher-volume, lower-ceremony — compact by default:

- Header section with avatar: `### **{name}** upscored {n} charts`.
- One text display, one line per chart:
  `S20 [Ugly Dee](…) **985,420** (+12,001) SS → SS+ SG` — difficulty bubble, masked link, bold
  new score, delta, old→new grade transition (only when the grade changed, as today), plate.
- Chunked at ~12 rows per card (multiple cards for import-sized bursts), replacing today's
  10-per-message split.
- **Exception**: when the whole event is ≤3 changes (the common "one great sesh" case),
  upscores get art sections too — a single big upscore deserves the same ceremony as a pass.

### Digest card — import-sized events

The batch accumulator (`UpdatePhoenixRecordHandler`) drains one fat event per (user, mix), and
the 2-minute window slides while an import keeps appending — so an initial account import or a
months-of-catch-up dump arrives as a **single event with hundreds to ~2,000 changes**.

What that does to the pipeline **today**:

- Passes render as 10 lines + "And 1,990 others!" — survivable but uninformative.
- The level-progress message iterates **every (type, level) touched**. A veteran's initial
  import touches ~45–50 level groups; at ~43 expanded chars per line that message crosses the
  2,000-char content limit, the send throws, and the per-message catch swallows it — **the
  stats message silently never arrives**. (Latent bug, owner-confirmed as observed behavior;
  **hotfixed 2026-07-05** ahead of this redesign: the stats block now chunks at 10 groups
  like every other list in the saga, and `DiscordMessageSplitter` in `Data` backstops the
  2,000-char cap at the transport so nothing silently drops. The digest below still replaces
  the resulting wall-of-messages UX.) It also fires two queries per level group (~100-query
  burst) — the digest fixes that; the hotfix deliberately doesn't.
- A returning player's upscore dump chunks at 10 per message — a 400-upscore dump is 40
  consecutive messages in every subscribed channel.

The redesign replaces all of that with a threshold: when an event carries more than
**25 changes**, the saga emits **one digest card** instead of pass/upscore cards:

```
┌─ container — accent = best new grade ──────────────────────────┐
│  ### JEWEL passed 1,872 charts · upscored 141      [avatar]    │
│  ──────────────────────────────────────────────────────────    │
│  (top 5 highlights across both sets, art rows,                 │
│   each tagged "new pass" / "upscore")                          │
│  ──────────────────────────────────────────────────────────    │
│  Levels S1–S22 · D4–D24 — highest new pass D24                 │
│  Progress: D24 84/210 (40.0%) · D23 96/141 (68.1%) · S22 …     │  ← top 3 levels only
│  -# Phoenix · PIU Scores                                       │
│  [ View all recent scores ]                                    │
└────────────────────────────────────────────────────────────────┘
```

- Highlights follow the universal noteworthy ordering (level desc, then scoring level desc),
  preferring flagged scores when the capture table has them for the session.
- Exactly **one message** per import per channel, regardless of dump size.
- Level progress is capped at the top 3 levels (by new-pass count) — which also retires the
  overflow bug, since the non-digest card only ever contains a small event's level groups.
- The digest is viable *because* the full list has a home: the "View all recent scores"
  button carries the enumeration the card no longer attempts.
- The threshold is a named constant (tunable later if 25 proves wrong); no config knob until
  real events say otherwise.

### Budget math (why the caps are safe)

Worst-case passes card: container (1) + header section (3: section + text + thumbnail) +
2 separators (2) + 5 art rows (5 × 3 = 15) + overflow (1) + stats (1) + footer (1) + action
row + button (2) = **26 of 40 components**. Text including masked-link URLs (~60 chars each):
≈1,500 of 4,000 chars. The upscores card is text-dominant: 12 rows × ~140 chars ≈ 1,700 chars,
6 components. The saga packs to these caps; the renderer clamps defensively (drop art
accessories first, then truncate) and logs if a clamp ever fires — it shouldn't.

## The seam: port model changes

`IBotClient` lives in `Domain.SecondaryPorts` and cannot see Discord.Net, so the structured
form needs a provider-agnostic model. New records in `ScoreTracker.Domain/Records/`
(`[ExcludeFromCodeCoverage]`, per the Records-folder convention):

```csharp
public sealed record RichBotMessage(
    RichBotSection? Header,              // avatar/title row
    IReadOnlyList<IRichBotBlock> Blocks, // ordered card body
    string? Footer,                      // subdued trailing line
    uint? AccentColor,                   // 0xRRGGBB
    IReadOnlyList<RichBotLink> Links);   // link buttons (≤5)

public interface IRichBotBlock { }       // closed set:
public sealed record RichBotText(string Markdown) : IRichBotBlock;
public sealed record RichBotSection(string Markdown, Uri? Thumbnail) : IRichBotBlock;
public sealed record RichBotDivider : IRichBotBlock;

public sealed record RichBotLink(string Label, Uri Url);
```

Every string field carries the existing emoji-token vocabulary; the adapter keeps sole
ownership of token→emoji replacement. The port gains one method (existing string methods stay
until the other notification types migrate):

```csharp
Task SendRichMessages(IEnumerable<RichBotMessage> messages, IEnumerable<ulong> channelIds,
    CancellationToken cancellationToken = default);
```

**Why a generic block model and not a semantic port method** (`AnnounceScores(…)`):
presentation composition already lives in the sagas — they build the message text today, and
that's the right home for "what does a score announcement say." A semantic port would move
layout into the Data adapter and grow the port by one method per notification type. The block
model keeps one render path, gives the other five notification types a migration target with
no further port changes, and the shapes (text / thumbnailed row / divider / links / accent)
port cleanly to any future rich channel (Slack, Teams, webhook cards).

## Adapter rendering (`ScoreTracker.Data`)

A new internal pure translator alongside the client, so it's unit-testable without a socket:

- `DiscordRichMessageRenderer.Render(RichBotMessage, Func<string,string> replaceTokens)`
  → the V2 component tree **plus** the flattened plain-text fallback string.
- Mapping: header/`RichBotSection` → V2 section with thumbnail accessory (thumbnail-less
  sections render as plain text displays) · `RichBotText` → text display ·
  `RichBotDivider` → separator · `Links` → action row of link buttons · `AccentColor` →
  container accent. Emoji-token replacement runs over every markdown part (extracted from the
  existing `SendMessages` replacement loop into a shared helper — the dictionaries don't move).
- `DiscordBotClient.SendRichMessages` sends with the ComponentsV2 message flag, keeping
  today's per-channel and per-message try-catch semantics; on a send failure it falls back to
  sending that channel the renderer's plain-text string through the existing path, so a guild
  where components misbehave still gets its announcement.
- Discord.Net 3.18 exposes the V2 builders (`ComponentBuilderV2`, container/section/
  text-display/thumbnail/separator builders); exact builder-method names should be confirmed
  against the package at implementation time — the design doesn't depend on their spelling.

## Saga changes (`Communities`)

`Consume(PlayerScoresUpdatedEvent)` keeps all of its data work — best-score lookup, chart
lookup, top-50 crown set, clear-count stats, mix-scoped reads — and swaps string concatenation
for composing `RichBotMessage`s per the layouts above. The level-progress block folds into the
passes card; events over the digest threshold short-circuit to the digest card (and skip the
per-level stats loop entirely beyond the top 3). Message-splitting moves from "10 lines per
2,000-char message" to the component/char budget packer. `MixPrefix` continues to prefix the
header text; the footer carries the `#MIX|…#` emoji token.

## The Recent Scores page (the card's link target)

A public, anonymous-viewable feed of a player's recent score activity — the first delivery of
the score-progression-history feature the journal was built for, trialed here as the Discord
card's CTA before it gets linked from other UI surfaces.

- **Route**: `/Player/{UserId:guid}/Sessions` — the page is named **Sessions**
  (owner-resolved 2026-07-05; supersedes the earlier `RecentScores` route in older mocks).
  Blazor page under `Pages/Progress/`, dispatching via `IMediator` like every page. Stable
  and constructible from the saga, which already has the `UserId`.
- **Privacy**: the page loads the target user; if the user is **not public**
  (`User.IsPublic == false`) it redirects to home (`NavigationManager.NavigateTo("/")`), and
  the query handler independently returns nothing for non-public users — defense in depth, so
  the redirect rule can't be bypassed by dispatching the query some other way. The Discord
  card only renders the button for public players in the first place; the redirect is the
  safety net for stale links and privacy flips.
- **Data**: a new published query on ScoreLedger's contracts —
  `GetRecentScoreEventsQuery(UserId, Mix?, Page, PageSize) : IQuery<…>` — reading the
  append-only `ScoreEventJournal` through a new read method on the vertical-internal
  `IScoreJournalRepository` (today it is append-only). Each row classifies as **New pass** /
  **Upscore** / **Played** by comparing against the player's prior journal state for that
  chart (the `(UserId, ChartId, OccurredAt)` index makes the prior-best lookup cheap).
  Chart/song display data comes from the Catalog read path like other pages.
- **Presentation — session roundup cards, not a table** (owner direction 2026-07-05): the
  page is a newest-first stack of per-session cards. Each card: header (session kind —
  import / play session / CSV — plus when and mix; the kind *is* the source, no per-row
  source chips) → aggregate counts (passes · upscores · highlights) → the gold **milestone
  strip** → **highlights** (flagged scores only, universal noteworthy order, song art —
  visually the same rows as the Discord card) → folder-progress deltas → and, above ~10
  rows, a collapsed "Show all N scores" detail list (the compact row treatment lives inside
  the expander). Small sessions render everything inline, including non-highlight
  PLAYED/BREAK rows. A 2,013-score initial import stays one calm card. **The Discord digest
  and the session roundup are the same concept at two zoom levels** — the card's button
  deep-links to its session's roundup (`?session={id}` anchor) now that the event carries
  `SessionId`. Rows predating capture (null `SessionId`) group by calendar day under an
  "Older scores" strip. Mix filter defaults to Phoenix (Phoenix 2 appears when it goes
  live); paged **by session with a selectable page size**. **Below the roundups sits the
  raw score journal table** — every journal row as stored (time, chart, score, result,
  classification, source), including backfill rows, whose dates are the best attempt's last
  update rather than the original play time and are footnoted as such, plus an **Export
  (CSV) button** — a UI-support controller endpoint (IsPublic-gated, deliberately *not*
  under the pinned `api/*` contract surface). The roundups are the reading view; the table
  is the record.

### Scores of note — flags, milestones, sessions

**Five flag kinds** mark a score row as noteworthy:

1. 👑 **Pumbility contributor** — the chart sits in the player's top 50 at batch time.
2. 🏅 **Title progress** — the score counts toward a not-yet-complete title (the
   `TitleProgress` rules behind `GetTitleProgressQuery`).
3. 📊 **Score Quality ≥ 90th** — the score ranks in the top decile against comparable
   players (the Score Quality machinery: `GetPlayerScoreQualityQuery` →
   `ScoreRankingRecord(Ranking, PlayerCount)`, `ScoreQualitySaga`). Compute the percentile
   **tie-inclusive** (share of cohort scores ≤ yours) so a Perfect Game always lands at the
   100th percentile even when much of the cohort also has the PG.
4. 📁 **Folder completionist** — a new pass in a (type, level) folder whose completion is
   ≥ 90% after the batch (clear count ÷ folder size — the same lookups the card's
   level-progress block uses).
5. ⬆ **Competitive improver** — the batch raised the player's **Singles or Doubles**
   competitive level. Never the combined number: singles and doubles don't compare, so
   combined movement is not notability (it stays computed for the page that shows it; the
   legacy ratings Discord message still posts it — trim that when the consumer migrates to
   `RichBotMessage`). Competitive level is batch-computed, so per-score attribution is a
   heuristic: flag the improved side's rows whose score rating meets the old level; tune at
   implementation.
6. 🆕 **Folder debut** — one of the **first 3 passes ever in a (type, level) folder**,
   S23 and D23 counted separately (owner-added 2026-07-05). When one batch lands more than
   3 passes in a brand-new folder, the top 3 by noteworthy ordering get the flag.

**Write-time capture, not read-time derivation** (supersedes the earlier read-time-badges
design). PlayerProgress already consumes `PlayerScoresUpdatedEvent` to compute ratings,
titles, and score quality; the same processing now also appends **`ScoreHighlight`** rows —
a PlayerProgress-internal table (`UserId`, `MixId`, `ChartId`, `SessionId`, flag kinds,
denormalized `Level` + `ScoringLevel` for ordering). Why write-time wins: the flags are
**historically true** (a read-time crown drifts as the top 50 moves under it), the feed
reads them with zero extra queries, and future surfaces — the import-results page wants
these flags too (owner-flagged, **explicitly deferred, not in this PR**) — read the same
rows. The journal itself stays **append-only**: highlights are a companion table keyed to
journal rows, never journal mutations.

**Pipeline ordering — cards render after capture.** `CommunitySaga` and the capture
consumer both subscribing to `PlayerScoresUpdatedEvent` would race, and cards could never
reliably show flags. So capture publishes a **`ScoreHighlightsCapturedEvent`**
(PlayerProgress contracts) when it finishes — **always, even with zero flags** — carrying
the original change set, the `SessionId`, and the per-chart flags; **Communities' score-card
consumer moves onto that event**. Cards and digests then select and order highlights
deterministically. Every other consumer of the score event is untouched. (In-memory
transport caveat: a crash between the two hops drops the card — the same at-most-once
posture the single hop has today.)

**SessionId — minted by the Session Batcher.** The score batch accumulator
(`IPlayerScoreBatchAccumulator`) already sees every submission from every source in one
place; it grows into the **Session Batcher**: alongside its 2-minute event batches it keeps
a per-`(user, mix, source)` **session envelope** — reuse the open session while activity
continues, close it after an **8 h** gap (owner-set), mint a new id on the next submission.
**Sessions never delay delivery**: the 2-minute event batches drain and Discord fires
exactly as today — the session envelope is identity, not a gate. Several Discord cards can
share one `SessionId`; the page rolls them into one roundup. The command handler asks it for the current session id at journal-append time;
the drained batch event carries the same id. Import and CSV runs may pass an explicit run
id, which the batcher honors as that session's identity ("scores you imported at the same
time"). **Every source hooks in — official import, CSV upload, manual UI entry, API — with
no exceptions**; the only things a non-import session skips are the steps that need the
official site (avatar refresh, recent-scores scan), which live in the import saga anyway.
Being in-memory, an app restart closes open sessions (next submission starts a new one) —
same durability posture as the transport, acceptable. `SessionId` lands as a new nullable
column on journal rows plus an **additive** field on `PlayerScoresUpdatedEvent`
(SchemaVersion stays 1; Mix set the additive precedent). Highlights key on
`(SessionId, ChartId)`, and the page groups the feed into **session roundups** with
milestones interleaved. No backfill (owner-accepted): pre-capture rows have null `SessionId`
and no flags, and render exactly as they do today.

Riding the same change: the **CSV path gets the import's diff guard and a proper
`csv` source tag** (today it submits per-chart bests un-diffed and unlabeled, so re-uploads
journal no-ops stamped `manual`), and its `KeepBestStats` semantics get verified while the
code is open.

**Journal semantics — progress only (owner-resolved 2026-07-05).** The handler currently
journals every submission as received, including no-ops ("play history"). That is bad data
— the import deliberately scrapes a few pages past the presumed cutoff (the official site
gives no dates or watermark), so re-submissions carry no information. The rule becomes:
**a row is journaled iff the submission changed the stored best attempt.** Concretely:

- **Changes that journal**: first entry for a chart (even broken), broken → unbroken,
  score up, plate up, broken-beats-broken (both broken, higher score — the record changed,
  so it journals as a Break improvement), and any deliberate manual overwrite — including
  downward corrections, since manual is authoritative and always writes the record.
  Identical re-entries and no-ops never journal, from any source.
- **Per-source record rules**: **manual/API always overwrite** the record with what was
  typed. **CSV keeps its current broken-front behavior** (passes only today) and gains the
  import's diff guard — improvements/new entries only, so re-uploads neither churn records
  nor journal. **Official import** submits only its diffed set; the *IncludeBroken*
  checkbox governs whether broken plays may touch records (owner describes it as
  opt-in-to-overwrite-passes; exact current behavior gets verified in C1 before any change).
- **Pending owner confirmation (the one open carve-out)**: import-scraped broken plays
  journaling as **journal-only rows** when the checkbox leaves records untouched — they're
  real plays from the recent-scores scrape, so the journal could record them (classified
  Break) while the record keeps the pass. If declined, v1 journals nothing that doesn't
  change the record.

Consequences: the page's PLAYED classification disappears for new rows (legacy no-op rows
already journaled since 2026-06 render as "Played" but no new ones are written), and
classifications reduce to **New pass / Upscore / Break**.

**Milestones** (session-level gold rows). The kinds:

- **Pumbility gains** and **Singles/Doubles competitive gains** — combined competitive is
  excluded here too.
- **Title completions** and **Paragon level gains** (title + old → new paragon level; the
  data already rides `NewTitlesAcquiredEvent.ParagonUpgrades`). Phoenix 2 caveat: paragon
  and title semantics are expected to change with P2 — capture is mix-keyed and the kinds
  may need adjusting when the kit reveals how Andamiro did titles.
- **Folder lamps** — full-folder achievements, detected per touched folder at capture time
  by aggregating the folder's floor: **All Passed** (pass count = folder size), **All ≥
  letter grade** (the folder's minimum grade crosses a boundary), **All ≥ plate** (minimum
  plate crosses). Fire on the transition only (didn't hold before the batch, holds after).
  **Every boundary fires — all letters, all plates, no floor** (owner-resolved 2026-07-05):
  lamping is rare in PIU and the players who do it want every single gain announced.

Captured in the PlayerProgress-internal `PlayerMilestone` table (`Kind` + a compact `Detail`
payload — e.g. `D23`, `D20|SSS`, `S18|UG`, `Expert Lv.2|★3→★4`) by the sagas that already
publish `PlayerRatingsImprovedEvent`/`NewTitlesAcquiredEvent`; none of these facts are
persisted with a timestamp today (`PlayerHistoryEntity` has no Pumbility column,
`UserTitleEntity` has no acquisition date), so capture-now-or-lose-it applies, like the
journal itself.

**Noteworthy ordering — one universal rule**: wherever noteworthy scores are shown *as a
set* — the ⭐ Of-note filter, a session chunk's highlights, the Discord card and digest
highlight rows — order by **difficulty level descending, then scoring level descending**
(ChartIntelligence's `GetChartScoringLevelsQuery`). The chronological feed stays
chronological; the rule governs prioritized views.

**Filter**: a chip row above the feed — **All · Passes · Upscores · Of note** — where
"Of note" keeps flagged score rows and milestone rows only, in noteworthy order.

New tables (`ScoreHighlight`, `PlayerMilestone`) and the journal `SessionId` column each get
their row in DATABASE-SCHEMA.md.
- **Honest boundary**: the journal only reaches back to when journaling shipped — the page is
  "recent activity", not all-time history, and says so in an empty-state/footnote line.
- **Reusable components**: the page is assembled from parameterized components —
  `SessionRoundupCard`, `MilestoneStrip`, `HighlightRow`, `ScoreJournalTable` — that take
  DTOs and dispatch nothing themselves. Owner-flagged future surface: a **community
  "recent sessions" feed** (a snapshot of an active community) would reuse
  `SessionRoundupCard` unchanged with sessions drawn across members; build for that now,
  wire it later.
- **Trial plan**: ships with (or just before) the card so the button has a target. Entry
  points elsewhere in the UI (Progress page, community leaderboard rows, `UserLabel`,
  community recent-sessions) wait until the Discord-driven traffic says the page earns
  them.
- **Localization**: new UI strings get keys in all eight locales in the same PR, per
  convention.

## Verified scores (source of the current best)

Officially imported scores get a durable **verified** marker (owner call, 2026-07-05):
tournaments and similar surfaces often shouldn't trust CSV/manually-entered scores, so the
distinction must live where those reads happen — on the **best-attempt record**, not just
the journal. Mechanism: `PhoenixRecords` gains a `Source` column, updated whenever the best
attempt updates; **verified ⇔ the current best's source is `officialImport`**. The
semantics fall out naturally: a manual entry that beats an imported best flips the record
to unverified; a later import that reconfirms flips it back; a no-op submission leaves it
untouched. Existing rows backfill `NULL` (= unknown/pre-flag, treated as unverified for
gating purposes). Exposing the flag to tournament/qualifier rules is follow-up work — this
PR only captures it.

## Data model summary

The implementation-facing inventory of everything above. **New code types:**

| Vertical | Type | Kind | Notes |
|---|---|---|---|
| Domain (shared) | `RichBotMessage`, `RichBotSection`, `RichBotText`, `RichBotDivider`, `RichBotLink` | new records | `IBotClient.SendRichMessages` port model; strings carry emoji tokens |
| Domain (shared) | `PlayerScoresUpdatedEvent.SessionId` | additive field (`Guid?`) | SchemaVersion stays 1 (Mix precedent); partner webhook safe |
| ScoreLedger | `ScoreJournalEntry.SessionId` | additive field (`Guid?`) | import id / per-CSV-upload / 4 h rolling manual session |
| ScoreLedger | `UpdatePhoenixBestAttemptCommand.SessionId` | additive optional param | stamped by OfficialMirror import saga + CSV upload handler; null ⇒ handler derives the rolling session |
| ScoreLedger | `GetRecentScoreEventsQuery` → `RecentScoreEventRecord`, `ScoreEventClassification` (NewPass / Upscore / Break; legacy pre-guard rows may render Played) | new contracts | the journal's first read path; handler gates on `User.IsPublic` |
| PlayerProgress | `ScoreHighlight` (internal entity + record), `HighlightFlag` [Flags] enum (PumbilityTop50, TitleProgress, ScoreQuality90, FolderCompletion90, CompetitiveImprover, FolderDebut) | new | captured by the PlayerProgress capture consumer of the score event |
| PlayerProgress | `ScoreHighlightsCapturedEvent` | new contracts event | published after capture, always (zero flags included); carries the change set + `SessionId` + per-chart flags; Communities' score-card consumer subscribes to this, not the raw score event |
| PlayerProgress | `PlayerMilestone` (internal entity + record), `MilestoneKind` enum (PumbilityGain, TitleCompleted, ParagonLevelGain, SinglesCompetitiveGain, DoublesCompetitiveGain, FolderPassLamp, FolderGradeLamp, FolderPlateLamp) + compact `Detail` payload | new | appended by `PlayerRatingSaga`/`TitleSaga` + folder-floor checks per touched folder |
| ScoreLedger | `RecordedPhoenixScore.Source` (source of current best) | additive field | verified ⇔ `officialImport`; updated when the best updates; existing rows NULL |
| ScoreLedger | Session Batcher (evolved `IPlayerScoreBatchAccumulator`) | behavior | mints/extends per-(user, mix, source) session envelopes; honors explicit import/CSV run ids; in-memory (restart closes sessions) |
| Web | `SessionRoundupCard`, `MilestoneStrip`, `HighlightRow`, `ScoreJournalTable` | new components | DTO-in, dispatch-free — reusable for the future community recent-sessions feed |
| Web | journal CSV export endpoint | new UI controller action | IsPublic-gated; UI-support surface, not `api/*` |
| PlayerProgress | `GetScoreHighlightsQuery`, `GetPlayerMilestonesQuery` | new contracts | the page merges these with the journal read in memory — no cross-vertical SQL |
| Data | `DiscordRichMessageRenderer` | new pure translator | RichBotMessage → V2 components + fallback text; `DiscordMessageSplitter` already shipped |

**Existing types referenced, unchanged:** `User.IsPublic` (privacy gate), `Chart`/`Song`/`PhoenixScore`/`PhoenixLetterGrade`/`PhoenixPlate` (SharedKernel display), `RecordedPhoenixScore` via `GetTop50ForPlayerQuery` (crown), `TitleProgress` rules (title flag), `ScoreRankingRecord` via the Score Quality machinery (quality flag), `GetChartScoringLevelsQuery` (noteworthy ordering), `Community.ChannelConfiguration` (opt-in flags), `PlayerHistoryEntity`/`UserTitleEntity` (untouched — their gaps are why milestones capture).

**Schema changes (one migration, scaffolded from `Data` with the CompositionRoot startup project):**

| Table | Change | Indexes |
|---|---|---|
| `scores.ScoreEventJournal` | + `SessionId uniqueidentifier NULL` (no backfill) | + `(UserId, MixId, OccurredAt)` for feed paging; + filtered `(SessionId)` for session/import-results reads |
| `scores.ScoreHighlight` **(new)** | `Id` PK, `UserId`, `MixId`, `ChartId`, `SessionId NULL`, `OccurredAt`, `Flags int`, `Level int`, `ScoringLevel decimal NULL` | `(UserId, MixId, OccurredAt)`; filtered `(SessionId)` |
| `scores.PlayerMilestone` **(new)** | `Id` PK, `UserId`, `MixId`, `SessionId NULL`, `OccurredAt`, `Kind`, `OldValue decimal NULL`, `NewValue decimal NULL`, `Title nvarchar NULL`, `Detail nvarchar(64) NULL` | `(UserId, MixId, OccurredAt)` |
| `scores.PhoenixRecords` | + `Source nvarchar(32) NULL` (source of current best; verified ⇔ `officialImport`; existing rows NULL) | none new |

Both new tables live in PlayerProgress's model contribution (already listed in
`VerticalModelContributions.All()`); the journal column rides ScoreLedger's. Each change gets
its row in DATABASE-SCHEMA.md in the same PR. Hangfire, Communities, and Identity tables are
untouched; the Discord work itself needs no schema.

## Testing

- **`CommunitySagaTests`**: the score-update tests currently assert on concatenated strings;
  they're rewritten to `Verify` `SendRichMessages` with `It.Is<>` predicates on structure —
  row count and order, thumbnail URLs, accent color, overflow grouping, the ≤3-changes
  art-for-upscores rule, budget splitting. Strictly stronger assertions than `Contains`.
- **New renderer tests** in `ScoreTracker.Tests` (it already references `Data` for the
  PiuGame parser approval tests): token replacement reaches every markdown part, component/char
  clamps, fallback-text flattening, link-button mapping.
- **E2E**: not in the PR gate — the bot is disabled without a token (`BotHostedService` skips
  startup), and Discord can't be wire-stubbed the way the PIU site is (Discord.Net speaks a
  live WebSocket gateway protocol; WireMocking it is impractical and would prove nothing).
- **Live Discord canary** (uses the private test-lab channel): a small opt-in suite —
  env-gated on a `Discord:CanaryToken`/channel-id pair so it never runs where the secret is
  absent — that starts the real `DiscordBotClient`, sends the sample passes card, digest card,
  and upscores card (each stamped with a run marker GUID) to the lab channel, then reads the
  channel back over REST and asserts the marker arrived with V2 components attached. Runs
  **on manual trigger only** — run it when a change touches Discord or Communities code.
  Not per-PR (a live SaaS in the PR gate means fork PRs can't get the secret, Discord hiccups
  redden unrelated builds, and parallel runs interleave in one channel), and no schedule
  either — the owner hears about real breakage from the communities faster than a scheduled
  run would report it. Messages are deliberately **not** deleted after the assert — the lab
  channel doubles as a human-glanceable visual-regression gallery of exactly what the cards
  looked like on every run. What the canary buys: Discord API contract drift (V2 payload
  rejections), emoji-ID resolution, permission and token validity — the failure modes
  component tests can't see. What it doesn't: composition correctness, which the saga and
  renderer tests own. Complementary always-on signal: the fallback path already logs every
  structured-send failure; production alerting on that log line catches the same class of
  breakage between canary runs.

## Rollout

Owner's call (2026-07-05): the whole scope — message-drop hotfix, Recent Scores page, port
model + renderer + saga cards + digest — lands on **one PR** (#124, this branch), built in
this order so each commit stands alone:

1. *(shipped)* The message-drop hotfix — stats chunking + the transport splitter.
2. The Recent Scores page (the card's button target exists before any card links to it).
   Mix emojis are already uploaded and recorded above.
3. The port model + renderer + saga change (cards + digest) with the automatic per-channel
   fallback. No schema change; channel opt-in flags untouched; partner webhook payloads
   (`PlayerScoresUpdatedEvent` JSON, ADR-001 D3) completely unaffected.
4. A `Discord:RichScoreMessages` config kill-switch (default on) is cheap insurance for a
   community-facing surface — flipping it re-routes to the legacy string path without a
   deploy rollback. Delete after burn-in.

Follow-up passes (later PRs, outside this scope): titles → ratings → weekly → UCS onto
`RichBotMessage`, then delete the string-composition paths and, last, the legacy
`SendMessages` port method once no caller remains.

## Resolved calls (owner, 2026-07-05)

1. **Journal = progress only.** No no-op rows — a submission journals only when it changes
   best-attempt state (first entry even if broken, broken→unbroken, score↑, plate↑).
   PLAYED disappears for new rows; legacy no-op rows render "Played". *(One nuance pending
   owner confirmation: an improved-but-still-broken score counts as state change — see
   remaining questions.)*
2. **Session gap = 8 h.** Sessions never delay Discord delivery — the 2-minute event
   batches fire as today; the envelope is identity only.
3. **Score Quality flag ≥ 90th percentile**, tie-inclusive. ✓
4. **Folder-completion flag ≥ 90%.** ✓
5. **Folder lamps fire on every boundary** — all letters, all plates, plus All Passed. No
   floor: lampers want every gain announced.
6. **Folder-debut flag** — first 3 passes in a (type, level) folder, S/D counted
   separately.
7. **Page = "Sessions"** at `/Player/{id}/Sessions`.
8. **Collapse above ~10 rows; paged by session with a selectable page size; the journal
   table gets a CSV export button.**
9. **Art-row cap 5** (iteration 1; tune via lab-channel posts).
10. **Digest threshold 25** (iteration 1).
11. **P2 accent = grade color + textual prefix + mix emoji** (brand-color override in
    reserve).

**Deferred by design (no decision needed now):** same-burst aggregation of titles/rating
events into one card; surfacing flags on the import results page (owner-deferred); the
community "recent sessions" feed (components built reusable for it); which UI surfaces link
the page after the Discord trial; consuming the verified-source flag in tournament rules.

(Resolved earlier: the card's single CTA is **View all recent scores**, deep-linked to the
session's roundup, rendered only for public players; per-community leaderboard buttons stay
deferred behind per-channel rendering.)

## Build plan (one PR — #124 — sequential commits, each green on the fast suites)

Already shipped on the branch: the message-drop hotfix (stats chunking +
`DiscordMessageSplitter`).

- **C0 — Self-creating system communities.** Owner reports local errors about missing
  communities: nothing seeds "World"/country communities on a fresh database, and
  `CommunitySaga`'s `UserUpdatedEvent` consumer throws `CommunityNotFoundException` joining
  them. Fix at the root: system communities (World + regional/country) auto-create on
  first join — self-healing in every environment, and new countries create their community
  on demand. Verified against the local dev DB.
- **C1 — Journal progress-only guard + CSV alignment.** Journal ⇔ record changed in
  `UpdatePhoenixRecordHandler`; verify the import's IncludeBroken record semantics before
  touching them; CSV keeps its broken-front behavior but gains the import's diff guard +
  `csv` source tag (+ the pending journal-only broken-import carve-out if confirmed).
  Handler tests.
- **C2 — Schema migration.** Journal `SessionId` + `(UserId, MixId, OccurredAt)` +
  filtered `(SessionId)` indexes; `ScoreHighlight`; `PlayerMilestone` (incl. `Detail`);
  `PhoenixRecords.Source`. Entities + model contributions + DATABASE-SCHEMA.md rows.
- **C3 — Session Batcher + stamping + verified source.** Session envelopes (8 h,
  per user/mix/source, explicit run-id support); `SessionId` on the command, journal
  writes, and `PlayerScoresUpdatedEvent` (additive — serialization tests updated); import
  saga and CSV pass run ids; `PhoenixRecords.Source` written on best-attempt updates.
- **C4 — Highlight capture (PlayerProgress).** `HighlightFlag` enum + capture consumer
  computing all six flags; `ScoreHighlightsCapturedEvent` published always (zero flags
  included); `GetScoreHighlightsQuery`. Saga tests per flag.
- **C5 — Milestone capture (PlayerProgress).** `MilestoneKind` + `PlayerMilestone` writes
  from `PlayerRatingSaga`/`TitleSaga` + folder-floor lamp detection (every boundary) +
  paragon gains; `GetPlayerMilestonesQuery`. Saga tests per kind.
- **C6 — Journal read + classification (ScoreLedger).** Repository read,
  `GetRecentScoreEventsQuery` (IsPublic-gated), New pass / Upscore / Break classification
  (legacy rows may render Played). Component + integration tests.
- **C7 — Sessions page.** `/Player/{id}/Sessions` + `SessionRoundupCard` /
  `MilestoneStrip` / `HighlightRow` / `ScoreJournalTable` components (DTO-in,
  dispatch-free), ⭐ Of-note filter, collapse >10 rows, per-session paging with page-size
  selector, `?session=` deep-link anchor, CSV export endpoint (UI controller,
  IsPublic-gated), redirect-home privacy, localization keys in all eight locales, API.md
  note for the export endpoint. **Owner-required: a Playwright E2E test in
  `ScoreTracker.Tests.E2E` proving the page functions** (seeded journal/highlights/
  milestones render as roundups; non-public players redirect home) — Playwright is also
  the UI iteration loop during development.
- **C8 — Rich message port + renderer.** `RichBotMessage` records,
  `IBotClient.SendRichMessages`, `DiscordRichMessageRenderer` (Components V2 tree +
  flattened fallback, budget clamps, emoji tokens incl. the recorded `#MIX|…#` ids),
  `Discord:RichScoreMessages` kill-switch. Renderer unit tests.
- **C9 — Cards + digest (Communities).** Score-card consumer moves onto
  `ScoreHighlightsCapturedEvent`; passes/upscores cards, ≤3-change art exception, digest
  >25, noteworthy ordering via `GetChartScoringLevelsQuery`, per-channel fallback,
  public-only deep-link button, mix prefix + emoji. `CommunitySagaTests` rewritten
  structurally.
- **C10 — Discord canary (opt-in) + docs sweep.** Env-gated manual-run canary posting the
  sample cards to the owner's lab channel (token + channel id from user secrets/env —
  never committed) with REST readback; HOW-TO-TEST note; ARCHITECTURE.md event-flow
  update; design doc flipped to "implemented".

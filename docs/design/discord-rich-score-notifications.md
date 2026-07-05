# Discord rich score notifications — design

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
│  D24 👑 District 1                                 [song art]  │  ← section per pass,
│  **997,821** SSS+ UG                                           │    top 5 by level desc,
│  S20 Ugly Dee                                      [song art]  │    score desc; song name
│  **985,420** SS+ SG                                            │    is a masked link to
│  …(up to 5 art rows)…                                          │    the chart page
│  +7 more: S19 ×3, S18 ×2, S17 ×2                               │  ← overflow text display,
│                                                                │    grouped difficulty counts
│  ──────────────────────────────────────────────── separator    │
│  D24 173/210 (82.4%) · S20 141/168 (83.9%)                     │  ← level progress folded in
│                                                                │    (no more second message)
│  -# Phoenix · PIU Scores                                       │  ← subdued footer line
│  [ View District 1 D24 ]                                       │  ← action row, link button
└────────────────────────────────────────────────────────────────┘
```

- **Header**: `### **{name}** passed {n} charts` (markdown heading), avatar as the section
  accessory. The `[Phoenix 2]` mix prefix stays **textual** in the header, exactly as decided
  in the Phoenix 2 plan — the accent color is decoration, not the mix signal.
- **Pass rows**: same ordering as today (level desc, then score desc). Two lines per row:
  difficulty bubble + crown (top-50) + song name as a masked link; then bold score + grade +
  plate emoji. Top **5** rows get art (down from 10 text lines — art rows are ~2× the height,
  so 5 keeps the card scannable on mobile); the rest collapse into one grouped overflow line.
- **Level progress**: the same stats currently sent as a second message become a block inside
  the card, joined with `·` separators instead of one line each.
- **Footer**: `-#` subheadline markdown (Discord's small-text) with the mix logo emoji + mix
  name + product name (see "Mix logo emojis" below). V2 has no timestamp concept; the message
  timestamp covers it.
- **Button**: one link button — **"View all recent scores"** — to the player's Recent Scores
  page (section below). Rendered only when the player is public (`User.IsPublic`, already
  loaded by the saga); non-public players get the card without the button. (A per-community
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

- **Route**: `/Player/{UserId:guid}/RecentScores` (Blazor page under `Pages/Progress/`,
  dispatching via `IMediator` like every page). Stable and constructible from the saga, which
  already has the `UserId`.
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
- **Presentation**: newest-first rows — time-ago, difficulty bubble, song (linked to
  `/Chart/{id}`), score, grade/plate icons, a pass/upscore/played chip, and the acquisition
  source chip (`manual` / `officialImport` / `csv`). Mix filter defaulting to Phoenix
  (Phoenix 2 appears when it goes live). Paged at 50.

### Scores of note (badges, milestones, and the "Of note" filter)

Two mechanisms with different costs:

- **Badges on score rows — derived at read time, ship with the page.**
  - 👑 *Pumbility contributor*: the row's chart is in the player's current top 50
    (`GetTop50ForPlayerQuery`) — the exact rule the Discord crown already uses, with the
    same "as of now" semantics.
  - 🏅 *Title progress*: the row's level/grade counts toward a not-yet-complete title per
    the existing title rules (the `TitleProgress` machinery behind `GetTitleProgressQuery`).
- **Milestone rows — need write-time capture (small, additive).** Pumbility gains and title
  completions are batch-computed by `PlayerRatingSaga`/`TitleSaga` and published
  (`PlayerRatingsImprovedEvent`, `NewTitlesAcquiredEvent`) but never persisted with
  timestamps: `PlayerHistoryEntity` snapshots competitive level only (no Pumbility), and
  `UserTitleEntity` has no acquisition date. A new PlayerProgress-internal **`PlayerMilestone`**
  table (`UserId`, `MixId`, `OccurredAt`, `Kind`, payload columns), appended by those sagas
  at publish time, plus a published `GetPlayerMilestonesQuery`, lets the feed interleave
  highlighted rows — *"PUMBILITY 8,172 → 8,214 (+42)"*, *"Title completed: Advanced Lv.9"* —
  by timestamp. Ratings and titles compute per import batch, so milestone rows attribute to
  the **session** (they sit between score rows at the batch timestamp), not to a single
  score; the badges are the per-score signal. No backfill is possible — title acquisition
  *dates* before capture simply don't exist (one more capture-now-or-lose-it case, like the
  journal itself). New table = a row in DATABASE-SCHEMA.md.
- **Filter**: a chip row above the feed — **All · Passes · Upscores · Of note** — where
  "Of note" keeps badged score rows and milestone rows only.
- **Honest boundary**: the journal only reaches back to when journaling shipped — the page is
  "recent activity", not all-time history, and says so in an empty-state/footnote line.
- **Trial plan**: ships with (or just before) the card so the button has a target. Entry
  points elsewhere in the UI (Progress page, community leaderboard rows, `UserLabel`) wait
  until the Discord-driven traffic says the page earns them.
- **Localization**: new UI strings get keys in all eight locales in the same PR, per
  convention.

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

## Open questions

1. **Art-row cap**: 5 art rows + grouped overflow is a taste call — confirm the visual
   weight against a real burst in the test-lab channel (the canary posts make this a
   one-glance check).
2. **Digest threshold**: 25 changes is a first guess. Too low and good sessions get
   flattened; too high and channels still get long cards. Tune against real events.
3. **Phoenix 2 accent**: the brand colors are now known (Phoenix = blue, Phoenix 2 = green).
   Current design keeps the grade-colored accent with the textual `[Phoenix 2]` prefix + mix
   emoji; the alternative is overriding the accent to the mix brand color. The mix emoji may
   make the override redundant.
4. **Same-burst aggregation**: titles/rating events that fire from the same import currently
   arrive as separate messages and would become separate cards (a first import also triggers
   a title wave through the same 10-per-message chunking). Merging into one card needs
   cross-event correlation — Phase 2 at the earliest; the titles consumer gets the digest
   treatment when it migrates to `RichBotMessage`.
5. **Recent page follow-through**: which UI surfaces get links if the Discord-driven trial
   performs — Progress page, community leaderboard rows, `UserLabel` popover?

(The earlier "which button?" question is resolved: the single CTA is **View all recent
scores** → the Recent Scores page, rendered only for public players. A per-community
leaderboard button stays deferred behind per-channel rendering.)

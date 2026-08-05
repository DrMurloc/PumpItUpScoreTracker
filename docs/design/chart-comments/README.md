# Chart comments &amp; the details dialog overhaul

The chart details dialog becomes four tabs — **Comments, Leaderboard, Chart Stats, Score History** —
with the video staying above them. Comments are the feature; the rest is a reorganisation of what
the dialog already renders.

**[mock.html](mock.html) is the UI reference.** Open it before building any surface described here:
it carries the real Phoenix palette at true widths (572 px desktop, 390 px mobile) and shows every
state in one place — language badges, threads, the composer, the rules card, the link interstitial,
the report dialog, the moderation queue row.

> **Two parts of this folder are scaffolding and get deleted when the feature ships:**
> **`mock.html`**, and **Part 2 — Technical scope** below. What survives is Part 1, the feature
> requirements. Anything in Part 2 that turns out to be a durable architectural fact belongs in
> `ARCHITECTURE.md` or `CLAUDE.md` instead, moved there rather than left here.

---

# Part 1 — Feature requirements

---

## 1. The dialog

Nineteen call sites render `ChartDetailsDialog`. That number drives most of the structural calls.

**Header (outside the tabs):** video → title row (difficulty bubble, song name, To-Do) → your score
and standing. Three things you want without a click.

**Tab strip is sticky** inside the scroll container, so a tall header scrolls away underneath it
instead of taking the tabs with it. On 390 px the strip **scrolls horizontally** rather than
truncating labels to jargon — the header alone is 274 px, so the fourth tab peeking is honest about
what is off-screen.

| Tab | Contents |
|---|---|
| **Comments** | Scope rail, sort, reader-language control, threads, composer. Count badge on the tab. |
| **Leaderboard** | `ChartLeaderboardScopes` unchanged — a move, not a rewrite. |
| **Chart Stats** | Meta grid, tier placements, skill bars, **similar charts**, PIU Center link. |
| **Score History** | The cross-mix journal, with **manual score edit at the bottom**. |

- **Every tab is lazy.** No queries until selected. The board already gates on `Active="Visible"`;
  the rest follow.
- **Default tab is Leaderboard**, last choice persisted in a `ChartDetails__Tab` UiSetting. The
  comment count badge is what makes anyone open Comments the first time.
- **Score History hides when logged out** — it is your journal plus your score inputs.
- ⚠ **Recording a score became two taps**, and several call sites exist only to record
  (`QuickRecordWidget`, `DailyStepWidget`, Charts' quick record). An **`InitialTab` parameter**
  alongside the existing `BoardScope` lets those callers open straight on Score History, so Round
  11's always-visible-inputs decision survives for the people it was made for.
- **`FocusCommentId`** is the second new parameter: the moderation queue opens the dialog on
  Comments, anchored to the reported one.

### Similar charts stays deliberately dumb

Stored match order from `GetSimilarChartsQuery` at the 0.55 floor. No re-sorting, no difficulty
lens, no filters — that machinery belongs to the chart page's shelf. A compact tile is song image +
difficulty bubble; tapping one **swaps the dialog in place** via an internal chart override with a
"← back to \<song\>" crumb, rather than pushing a chart-change callback out to nineteen hosts.

⚠ The graph is empty until `recalculate-chart-similarity` is triggered once in `/hangfire`. Owner,
post-deploy, still owed from the chart-page overhaul.

---

## 2. Comments

### The scope rail is the audience picker

Same `.cld-chip` vocabulary as the leaderboard's scopes, doing double duty: it filters what you read
*and* decides what you post to. "Which community am I posting to" is answered by where you are
standing, not by a second dropdown, and the composer states it in prose — *Posting to **Public** as
ERRLENA*.

**Audiences are Public + your non-regional communities.** World and country boards are ownerless and
carry no roles, so a comment posted there would have no moderator and would fall to the site admin
by default — the opposite of the deal in §4.

### Threads

Root plus **one level of replies**; replying to a reply targets the root. A reply **inherits its
root's audience as a domain invariant on the aggregate**, not as a field the UI sets — you cannot
move a thread between communities or between community and public.

A removed comment leaves a stub (*Removed by a community admin · 4 days ago*) so the thread keeps
its shape.

### Votes

Thumbs up only, one per user, never on your own. Sort defaults to **Top** (votes desc, then newest)
with a **Newest** toggle.

### Editing

An edit writes a revision row, stamps `EditedAt`, and renders *(edited &lt;time&gt;)*. History is
retained for moderation. Deletion is soft — `DeletedAt`, `DeletedByUserId`, reason — so the row stays
queryable.

⚠ **An edit invalidates the translations** and re-queues the comment. The monthly ceiling is a fuse
against *bugs*; an edit loop is a **user-driven** cost amplifier it cannot see. A per-comment
re-translation cooldown is the mitigation, not a limit on editing.

### Markdown

**Bold, italic, strikethrough, inline code, links, bare-URL autolink, line breaks, bullets.** No
headings, images, tables, or raw HTML. The 500-character cap counts the **raw markdown**.

**The parser lives in the vertical's Domain and emits a token tree.** Web renders tokens as real
Blazor elements and **never touches `MarkupString`** — which is precisely the temptation the
translation spec warns about when link support arrives
([comment-translation.md §5](../comment-translation.md)).

This is also why a WYSIWYG library was rejected. Comments render in five languages from
machine-translated text, so the safe renderer exists **either way**; a rich-text editor does not
replace it, it sits on top of it and adds HTML storage, server-side sanitisation, an HTML↔markdown
bridge, and unmeasured HTML round-tripping through an LLM. Once the parser exists, the editor is a
textarea, six buttons that wrap the selection, and a Write/Preview toggle. 500 characters is a tweet
and a half; GitHub, Reddit and Discord all ship exactly this and none of them ship WYSIWYG.

If the raw split ever needs upgrading, keep markdown on the wire and swap the input widget
(EasyMDE, MIT, ~40 KB). **Storing HTML is the door that does not close again.**

### Link trust

Trusted hosts open straight through with `rel="noopener noreferrer nofollow ugc"`; everything else
gets an interstitial naming the host.

| Trusted | Matching |
|---|---|
| piuscores, youtube / youtu.be, reddit / redd.it, `pumpout2020.anyhowstep.com`, piugame, piucenter | **Dot-boundary suffix** — `host == d \|\| host.EndsWith("." + d)`, so `youtube.com.evil.tld` cannot slip through |
| Every **public** Community Tool's URL | **Exact host only** — a tool at `tools.example.com` must not bless `evil.example.com` |

- Only `http` and `https` parse. `javascript:` and `data:` are rejected at parse time.
- The interstitial shows the **parsed host**, never the link text, which is author-controlled.
- The tool list is data, so the allowlist is dynamic and memory-cached on a short TTL.
- ⚠ **Trust is resolved inside the vertical, not in Web.** Each link node in the token tree ships
  with its `IsTrusted` boolean already decided, so the presentation layer makes no policy call.

### Language

Five renderings: the **en-US pivot** plus **es-ES, fr-FR, ko-KR, pt-BR**. Everything else falls back
to English.

- ja-JP and it-IT are **deliberately out for launch** — a cost decision, revisited if volume proves
  cheap (§3).
- **es-MX** maps to es-ES. Mutually intelligible, the es-MX catalogue is contaminated, and the
  original is always available.
- **en-ZW (Murloc) is never a translation target.** Murloc readers get English.

Display resolution, in order:

1. Reader locale → mapped rendering locale.
2. Comment's source shares that language → show the **original**, no badge. Silence is the common case.
3. A rendering exists → show it, badged *Translated from 한국어*, with a per-comment **Show original**.
4. Not yet translated → show the **original** badged *Queued for translation*. The comment already
   exists in *some* language; an empty box is worse than the author's own words.

⚠ **Never render a comment back into its own language.** The round trip is a measured rewrite —
register raised, community vocabulary flattened, the most contemptuous phrase dropped, `1000%`
corrupted to `100%`. `TranslationTarget.ForSource` already encodes this; **absence of a key is the
instruction to show the original.**

An unsupported source language (Indonesian, say) is detected by stage one — 34/34 across the
corpus — and named for the reader via `CultureInfo.DisplayName`.

### The rules card

Shown before a first comment, following `ToolRulesCard` exactly: bold lead, body, checkbox, every
string through `L[…]`. Copy is in [mock.html](mock.html) §03.

Two consents, collected when they become true:

| Record | Fires |
|---|---|
| `AgreedToTermsAt` + `TermsVersion` | Once, on the first comment of any kind. Versioned, so editing the rules re-prompts. |
| `ConsentedToPublicIdentityAt` | The first time a **private-profile** user posts to **Public**. |

Post to a community first and you see one checkbox; the identity checkbox appears later, when it is
actually true. These are real rows rather than UiSettings keys — an agreement wants a timestamp and
a version, and it should be auditable if a dispute lands.

⚠ A Murloc rendering exists purely to satisfy `LocalizationKeyTests` and
`MurlocValuesUseOnlyTheMurlocAlphabet`. Accepted risk, owner's call: someone browsing in Murloc may
accept text they cannot read.

---

## 3. Translation pipeline

**Sonnet 5, batched, pivot through English**, exactly as measured in
[comment-translation.md](../comment-translation.md). Nightly, oldest-first, with a **rolling monthly
$30 ceiling as the primary gate** and a nightly count (~50) underneath as smoothing.

### Cost

Derived from the workbench's measured $0.0211/comment by splitting on its stated ~60% input share:
~6,330 input + ~844 output tokens per comment. **Intro pricing ($2/$10 per MTok) ends 2026-08-31**;
everything below is standard $3/$15.

| Pipeline | Per comment, batched | 1,000/mo | 2,000/mo |
|---|---|---|---|
| **4 locales, as-built** ← shipping | $0.0158 | $15.83 | $31.66 |
| 6 locales (+ja +it), as-built | $0.0188 | $18.84 | $37.68 |
| 4 locales, glossary split | $0.0130 | $12.98 | $25.96 |
| 6 locales, glossary split | $0.0158 | $15.82 | $31.64 |

$30 buys **~1,900 comments/month** as shipped, against a realistic 100–300 steady state.

**The glossary split is deliberately not taken for launch.** It is the doc's own identified
optimisation (§3, ~30% input reduction) and it exactly pays for the two extra locales — but it means
re-tuning a prompt validated against a real corpus, and that sweep already showed a missing glossary
row producing `las tiradas` instead of `los runs`. It is the lever to pull when volume grows, by
which point there will be real data on which rows each stage uses.

### The batch state machine

The Batch API is asynchronous — most batches finish inside an hour, **maximum 24**, results retained
29 days, and **results return unordered, keyed by `custom_id`**. With two stages that is a four-state
machine per comment:

```
Pending → PivotSubmitted → PivotDone → FanOutSubmitted → Translated
                      ↘  Failed  ↙
```

Two recurring jobs: **Submit** (nightly — builds and submits both stages' batches) and **Collect**
(hourly — polls open batches, writes results, advances state, records usage).

- The **ceiling is checked at submit time**, against rolling 30-day actual usage from completed
  batches plus an estimate for in-flight ones. Spending is not something to discover afterwards.
- A `translated_by` provenance column on every rendering — model and path. It is what makes "why do
  these two hundred read differently?" answerable.
- ⚠ **A rendering whose link set differs from the source's is rejected.** Required by
  [comment-translation.md §5](../comment-translation.md) and non-negotiable: it is the deterministic,
  free defence against the only prompt injection worth an attacker's effort.
- Re-translation after a prompt or glossary change is **admin-triggered only**, and quotes its cost
  first.
- Admin page: backlog depth, oldest pending age, rolling spend against the ceiling, last run,
  failures, **Drain now**.

⚠ **This retires a documented safety property.** Today `ILanguageModelClient` has no implementation
in `Data` and no DI registration, so no shipping code path can spend a token. This feature ends that
on purpose. The ceiling, the nightly count and the submit-time check are what replace it.

---

## 4. Moderation

The deal, in the owner's words: **communities moderate themselves; public comments are the site
admin's; hate, discrimination and threats escalate regardless.**

### Permissions

A new flag on the existing `[Flags]` enum, which was built for exactly this:

```
ModerateComments = 1 << 4        // All: 15 → 31,  DefaultAdminPermissionsSeed: 13 → 29
```

- **Creators need nothing** — `PermissionsOf` returns `All` for the owner at read time.
- ⚠ `DefaultAdminPermissionsSeed` is **13**, not 15 — it deliberately excludes `PromoteAdmins`. So
  there are **two** populations to bump: `13 → 29` (the default kit grew) and `15 → 31` (explicit
  All). A hand-picked subset is left alone.
- ⚠ The backfill must hit **both** `CommunityMembership.Permissions` *and* `Community`'s stored
  `DefaultAdminPermissions`. Miss the second and every *future* admin in an existing club silently
  lacks the power, discovered months later.

### Sanctions

One table, two scopes:

`CommentRestriction(UserId, Scope: Site|Community, CommunityId?, RestrictedByUserId, Reason?, CreatedAt, LiftedAt?)`

- **Site scope** is the owner's content lock — blocks commenting everywhere.
- **Community scope** is the admin's mute — blocks that community only. This is deliberately
  *lighter* than the existing community ban, which ejects someone entirely; you stay in the club and
  lose the mic.
- Both are **prospective**. Existing comments stay unless separately deleted, matching the
  tool-maker ban pattern.
- A community **ban** already blocks commenting for free — no membership, no community comments.
- ⚠ Two Guid `*UserId` columns means `[PurgeKey(nameof(UserId))]` is required, exactly as on
  `CommunityMembership.GrantedByUserId`. Without it account deletion purges the wrong person.

### Reports route by audience

One Report action, a **closed reason vocabulary**, and the reason decides routing — the reporter
never has to work out whose problem it is, and the routing is **not advertised in the UI**.

| Reason | Goes to |
|---|---|
| Spam or advertising · Off topic · Wrong information | Community admins (or the site admin, if public) |
| **Hate or discrimination** · **Threats or harassment** | Community admins **and** the site admin |

⚠ **The report row stamps the rendering the reporter was reading.** Translation launders the thing
being detected, and the language-asymmetry case (benign Korean, hostile Spanish) is *only* a
moderation problem, because a moderator ever sees one language. The moderation view therefore shows
**the original and what the reporter saw**. Without it, an admin reading ko-KR cannot evaluate a
report filed against the es-ES rendering.

### Surfacing

A conditional panel — rendered only when that moderator has open reports — on the community admin
page and on `/Admin`. One row: difficulty bubble → song image → reported user → reporter → **Open**,
which launches the dialog with `InitialTab=Comments` and `FocusCommentId`. Moderation happens in the
surface the comment lives in; there is no second console to build or keep in sync.

The **shield glyph on a comment is the permission** — site admin sees it everywhere, a community
admin only inside their own club, nobody else renders it. Report lives in the `⋯` for everyone
signed in.

---

## 5. Deliberately out of scope

- **A notification system.** Owner, explicit. Accepted consequence: a reply three days later is a
  reply the asker never sees.
- **Comments on the chart page** (`/Charts/{mix}/{song}/{difficulty}`). Dialog only for v1. The page
  is static SSR and would need a second render path; public comments there would be strong
  crawlable content, so this is a likely follow-up rather than a rejection.
- **Cross-chart comment history for moderators.** Owner: avoid the surveillance UX problem for now.
- **A moderation *layer*** (automated flagging). Owner, and the corpus supports it — its three
  heated comments are all about charts, never identity, so an over-flagging layer catches all three
  and teaches everyone to ignore it. If ever added it must read the **original**, never the English
  pivot.
- **A public API surface.** Comments are not on `api/v1` or `api/v2`.

---
---

# Part 2 — Technical scope

> **Delete this entire part when the feature ships.** It describes how the thing gets built, not
> what it does. Durable facts (the vertical's package allowlist row, the retired token-spend
> invariant, the new Domain port) move to `CLAUDE.md` / `ARCHITECTURE.md` as they land, and do not
> survive here.

## 6. Verticals

### `ScoreTracker.ChartComments` — new

Owns comments, revisions, votes, restrictions, reports, consent records, and the renderings.
Standard vertical shape (`WeeklyChallenge` is the template): `Contracts/` and `Wiring/` public,
everything else internal.

Packages: `MediatR`, full `MassTransit` (the `AddChartCommentsConsumers` hook needs
`IRegistrationConfigurator`), `Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Caching.Memory` (the tool-host allowlist cache). **Nothing new to the
solution** — one row added to CLAUDE.md's allowlist table.

### `ScoreTracker.Translations` — grows up

Today it owns no tables and is wired into nothing. It takes the **whole batch pipeline**: prompts,
glossary, batch-tracking tables, the spend ceiling, the admin page, and the
`ILanguageModelBatchClient` interaction. Its public surface becomes:

```
QueueTextForTranslationCommand(sourceKey, text)
    → publishes → TextTranslatedEvent(sourceKey, pivot, translations, provenance)
```

ChartComments publishes the command on post/edit and consumes the event to write renderings onto the
comment.

The rejected alternative was ChartComments building request payloads itself. That leaks
`LanguageModelRequest` shapes across the boundary and makes any prompt change a two-vertical edit.
The split above keeps prompt knowledge where the glossary lives, keeps *comment* data in the
comments vertical, and means community descriptions or tool blurbs can reuse the pipeline later
without a rewrite.

⚠ ARCHITECTURE.md currently describes Translations as *"owns no tables, references no `Data`, and is
wired into nothing."* That sentence dies in the same PR.

### Cross-vertical reads

Three references out of ChartComments, all through contracts, no cycles — nothing referenced here
references back.

| To | For | Via |
|---|---|---|
| `Communities` | Which clubs the reader may read/post to, and whether they hold `ModerateComments` | `GetMyCommunityRolesQuery` — returns `(CommunityName, Role, Permissions)`, both answers in one call |
| `CommunityTools` | Public tool URLs for the link allowlist | the existing public-tools query, memory-cached on a short TTL |
| `Translations` | Queue and consume | the command/event pair above |

Charts are **not** referenced. ChartComments stores a `ChartId` and knows nothing about charts; the
moderation queue row resolves song and difficulty in Web via Catalog, which keeps the boundary
clean for one join that only presentation needs.

## 7. Layers

| Layer | Change |
|---|---|
| **SharedKernel** | One line — `ModerateComments = 1 << 4`, `All` → 31. |
| **Domain** | One new port: `ILanguageModelBatchClient` (submit / status / results), beside `ILanguageModelClient`. `Complete()` is synchronous-per-request and cannot express a 24-hour batch. |
| **Application** | Nothing. It is shrinking by design and this does not reverse that. |
| **Data** | `AnthropicBatchClient : ILanguageModelBatchClient` in `Clients/`, plus migrations. Reflection DI binds it automatically. |
| **Web** | Dialog restructure + `ChartCommentsTab`, `CommentThread`, `CommentComposer`, `CommentMarkdownView`, `CommentRulesCard`, `LinkInterstitialDialog`, `ReportCommentDialog`, `SimilarChartsCompactGrid`, `ChartScoreHistoryTab`, `ReportedCommentsPanel`. One small JS file for the composer's `wrapSelection`, shipped through `@Assets`. |
| **CompositionRoot** | `AddChartComments()`; `ChartCommentsModelContribution` into `VerticalModelContributions.All()`. |

### ChartComments internals

| Folder | Contents |
|---|---|
| `Domain/` | The `Comment` aggregate — audience invariant, edit→revision, soft delete, restriction gate. A `TournamentSession`-class rich model; the rules are dense enough to earn it. Plus `CommentMarkdown` (parser → token tree) and `LinkTrust`, both pure. Vertical-internal repository ports. |
| `Application/` | `CommentSaga`, `CommentModerationSaga`, `CommentTranslationSaga` (consumes `TextTranslatedEvent`), read handlers. |
| `Infrastructure/` | EF entities + repositories on `Set<TEntity>()`, the `UserOwned` purge manifest. |
| `Contracts/` | Commands, queries, records, events. |

### The token tree crosses as a contract type

Each link node ships with its `IsTrusted` boolean **already resolved inside the vertical**. Web
renders nodes as Blazor elements and makes zero policy calls — it never sees raw markdown, never
parses, never consults the allowlist, and `MarkupString` is structurally unreachable rather than
merely discouraged. The parser stays internal.

## 8. Two CLAUDE.md amendments

1. **`Anthropic` enters `ScoreTracker.Data`'s package allowlist**, plus the ChartComments allowlist
   row.
2. ⚠ **A documented safety property is retired on purpose.** Today nothing that ships can spend a
   token, precisely because `ILanguageModelClient` has no implementation in `Data` and no DI
   registration. This feature ends that. What replaces it: the submit-time ceiling check against
   rolling 30-day usage, the nightly count cap, and the admin page. That trade gets written down
   rather than discovered later.

## 9. Ratchets that bite if forgotten

| Miss | Consequence |
|---|---|
| `VerticalModelContributions.All()` | Scaffolded migrations silently drop every ChartComments table |
| `AddChartCommentsConsumers(...)` in `Program.cs` | MassTransit's scan skips internal types — CommunityTools once shipped with all 33 handlers unregistered and every suite green |
| Purge manifest **plus a real integration test** | A mocked purge test cannot catch over-deletion; only the decoy-account test can |
| `[PurgeKey(nameof(UserId))]` on `CommentRestriction` | Two `*UserId` columns — purges the moderator instead of the restricted user |
| resx in all nine locales, alphabetical, no case-collisions | A case collision renders English in every non-English locale while English itself looks perfect |
| `SCHEDULED-JOBS.md` + `DATABASE-SCHEMA.md` rows | Same-PR requirement |

## 10. Build order

Four slices. Each ships something usable; the expensive and irreversible parts land last, on
foundations that already work.

| | Slice | Tabs | New tables | Spends money |
|---|---|---|---|---|
| **1** | Dialog re-shape — sticky tab strip, `InitialTab`, Chart Stats absorbs the meta/skills/similar charts, Score History tab with the manual score edit | **3** | none | no |
| **2** | Comments — vertical, aggregate, parser, link trust, threads, votes, composer, rules card, `FocusCommentId` | **4** | yes | no |
| **3** | Moderation — permission flag + backfill, restrictions, reports, queue panels | 4 | yes | no |
| **4** | Translation — batch client, the four-state pipeline, ceiling, admin page | 4 | yes | **yes** |

⚠ **Slice 1 ships three tabs.** A Comments tab with nothing behind it is not shippable, and
`FocusCommentId` has nothing to focus. The fourth tab arrives with the feature — which also means
the 390 px tab-strip overflow gets measured twice, at three and again at four.

⚠ **Do not announce comments until slice 3 is deployed.** A UGC surface with no delete button is a
bad week. Slices 2 and 3 may be separate PRs provided they go out together.

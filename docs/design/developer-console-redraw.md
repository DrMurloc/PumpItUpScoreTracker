# Developer console and tool directory redraw

A follow-on to [toolmaker-requirements.md](toolmaker-requirements.md), same release. `/Developers`
had grown to eight stacked panels and roughly 1,300 words of inline prose with no hierarchy — one
scroll, nothing leading. This pass turns it into a navigable section, moves the admin list off it,
rebuilds the player directory around browsing, and replaces `docs/INTEGRATING.md` with an on-site
Code page.

Mocks: [wizard](https://claude.ai/code/artifact/fe22de59-e22c-4491-9ef2-d451e70ceefd) ·
[console](https://claude.ai/code/artifact/36e61ac8-e9b4-4367-bbcc-d20ad9683717) ·
[directory + admin](https://claude.ai/code/artifact/5c605c5c-86b3-49ab-a64f-0adc9aed7395)

> **Status, 2026-08-03: built.** Ten commits, all suites green at each. Three things landed
> differently from the plan below:
>
> - **ToolKeyPanel and ToolWebhookPanel kept their files.** §3 had them folded into their sections.
>   Transcribing six hundred correct, tested lines by hand is a slip waiting to happen with no
>   behavioural gain; they lost their card wrapper and their own heading instead, which is what made
>   the console read as a stack of equally-loud panels.
> - **51 keys had a call site and no resx entry at all**, five of them predating this pass — the
>   ratchets check that every locale covers en-US and nothing checks that en-US covers the call
>   sites. All added.
> - **Two Murloc coinages** were needed and are in the glossary: `grogl` (hour) and `Grglmrg`
>   (GameTag).

---

## 1. What changes, and why

| | Change | Reason |
|---|---|---|
| **Wizard** | Screen 1 is the rules alone; screen 2 creates the tool. Both under `Register` | A maker decides whether to build *at all* from the rules — reading them after naming the thing is backwards |
| **Wizard** | An **integrated / listing-only** fork on screen 2 | A tool like Pumpout is a directory entry pointing at a site. Making it mint an API key it will never use is theatre |
| **Console** | Six routed sections instead of one scroll | "How do I hook up the webhook" should be a URL, not a scroll-hunt |
| **Console** | Stat groups appear only when the thing they measure exists | A listing-only tool has no key and no endpoint and is **not unfinished**. The old readiness framing would nag it forever |
| **Console** | Every inline "why" becomes a `?` disclosure | Nothing deleted; moved one click off the reading path. ~1,300 words → under 300 |
| **Console** | `Keys` → `API` | It is the section about the API, of which keys are one part |
| **Console** | Invite rows show the **code**, not the URL, with a copy control | 60 characters of which 24 differ. Copy takes the whole thing |
| **Console** | The admin "All tools" list **leaves** `/Developers` | A personal console with an admin list bolted to its top is two pages stapled together |
| **Directory** | Browsing leads; the privacy answer becomes a one-line strip | Owner, 2026-08-03: "the average person is going there to be like *I wonder what tools are available*" |
| **Directory** | Two row shapes — `Connect` vs `Visit ↗` | There is nothing to grant a listing-only tool |
| **Docs** | `docs/INTEGRATING.md` is deleted | Owner: it was never what was asked for. A **Code** page with real snippets replaces it |

### The reversal worth recording

[api-v2-community-tools.md §12](api-v2-community-tools.md) says `/CommunityTools` "leads with *who
can read my scores*, not the catalogue: the page's job is the answer, and browsing is secondary".
That is now reversed — browsing leads. The privacy answer is not lost, it is a permanent strip at
the top of the page carrying either state ("3 tools can read your scores" / "No tools can read your
scores"), so the question is still answered above the fold without owning the page.

### A correction to the previous scope

The last pass concluded listing-only needed **no new column**, on the grounds the console could
derive it from "has no keys". That holds for the console and fails for the directory: a
brand-new *integrated* tool also has no keys, for the thirty seconds before its maker mints one.
Deriving would chip it *Site only* and offer players a `Visit` button for a tool that reads scores.
It also throws away the maker's stated intent for no gain. **One column.**

---

## 2. Routing

The site's navigation model is full document loads, and `OfficialSectionFrame` already runs five
routed pages behind one chrome. The console follows it rather than inventing in-page tabs — which
also makes every section deep-linkable, and a bookmarked webhook page is exactly the thing a
returning maker wants.

| Route | Now | After |
|---|---|---|
| `/Developers` | The whole console + admin list | Tool picker / empty state; redirects to your first tool |
| `/Developers/{toolId}` | — | **Tool** — identity, source, handle, listing, delete |
| `/Developers/{toolId}/api` | — | Keys |
| `/Developers/{toolId}/players` | — | Invite links + connected |
| `/Developers/{toolId}/webhooks` | — | Mode, URL, header, secret, verify, test |
| `/Developers/{toolId}/code` | — | API + Webhook snippets, four languages |
| `/Developers/{toolId}/insights` | — | Directory / API / webhook figures, recent deliveries, latest |
| `/Developers/{id}/Console` | Activity log | **301** → `/Developers/{id}/insights` |
| `/Developers/{id}/Debug` | Test + replay | **301** → `/Developers/{id}/webhooks` |
| `/Admin/CommunityTools` | Review queue | Review queue **+ All tools** |

Nav order is **Tool · API · Players · Webhooks · Code · Insights**. Tool holds the root route
because a maker opening their console is nearly always here to change something; Insights is last
because checking on a number is not what brings anyone here the first time. A listing-only tool
shows only Tool and Insights — it has no key and no endpoint, and that is not a half-built state.

The two retired routes split rather than both landing on Webhooks: `/Console` *was* the activity
log, and the log now lives on Insights, so sending an old bookmark to Webhooks would land it on a
page that no longer holds what it pointed at. `/Debug` still goes to Webhooks, where the test
button is.

⚠ **This forces an authorization decision.** `ToolConsoleSaga` (activity log) and `ToolDebugSaga`
(test delivery, replay) are **owner-only today** — neither consults `IsAdmin`, unlike
`ToolManagementSaga.Manageable` and `ToolKeySaga`. Absorbing them into sections an admin reaches
via **Manage** means either widening both to admin, or the admin seeing sections that render empty.
The ban dialog already promises "you can look at what happened", which today an admin cannot do for
someone else's tool. **Owner's call; recommend widening the activity log and leaving Debug
owner-only** — reading a log is inspection, firing a real delivery at a maker's production endpoint
is not.

---

## 3. Technical scope

### ScoreTracker.CommunityTools — the only vertical that changes

| Layer | File | Change |
|---|---|---|
| **Contracts** *(public)* | `ToolEnums.cs` | **new** `ToolKind { Integrated, ListingOnly }`; `ToolActivityKind` **+`DirectoryClicked`** |
| | `ToolRecords.cs` | `ToolRecord` **+`Kind`** **+`HasKeys`** **+`WebhookConfigured`** (the console's group visibility); `PublicToolRecord` **+`Kind`**; `ToolActivitySummary` **+`Calls`** **+`Clicks`** |
| | `Commands/ToolCommands.cs` | `CreateToolCommand` **+`Kind`**; **new** `RecordToolClickCommand(Guid ToolId)` |
| | `Queries/ToolQueries.cs` | **new** `GetToolCodeSamplesQuery(Guid ToolId)` — the values a snippet needs (key tail, webhook URL, header name, subscribed mixes), so Web never assembles them |
| | **new** `CodeSampleRecords.cs` | `ToolCodeContext(string KeyTail, string? WebhookUrl, string? HeaderName, IReadOnlyList<MixEnum> Mixes)` |
| **Domain** *(internal)* | `Tool.cs` | **+`Kind`**; `Create` takes it; `Rehydrate` widened. `RequestListing()` gains: a listing-only tool must have a `Url` (a directory entry pointing nowhere is the one thing it cannot be) |
| | `IToolActivityRepository.cs` | **+`RecordClick`** (hourly roll-up, same shape as `KeyUsed`) |
| | `IToolRepository.cs` | **+`CountKeysFor(IReadOnlyCollection<Guid>)`** — batch, so the directory and console resolve group visibility in one round trip rather than per row |
| **Application** *(internal)* | `ToolManagementSaga.cs` | `CreateToolCommand` stores `Kind`; `Project` fills `Kind`/`HasKeys`/`WebhookConfigured` |
| | `ToolAccessSaga.cs` | `GetPublicToolsQuery` carries `Kind` |
| | `ToolConsoleSaga.cs` | `GetToolActivitySummaryQuery` sums `KeyUsed` → `Calls` and `DirectoryClicked` → `Clicks`; ⚠ owner-only guard — see §2 |
| | **new** `ToolCodeSampleSaga.cs` | `GetToolCodeSamplesQuery` |
| | **new** `ToolMetricsSaga.cs` | `RecordToolClickCommand` |
| **Infrastructure** *(internal)* | `Entities/CommunityToolEntities.cs` | `ToolEntity` **+`Kind`** (`nvarchar(20)`, enum **name**, matching `Visibility`) |
| | `EFToolRepository.cs` | map `Kind`; **new** `CountKeysFor` |
| | `EFToolActivityRepository.cs` | **new** `RecordClick` |
| **Wiring** | — | No new registration. `ToolCodeSampleSaga`/`ToolMetricsSaga` are MediatR handlers, found by the host scan |

### ScoreTracker.Data

One migration, `ToolKind`: `scores.Tool` **+`Kind`**, backfilled `'Integrated'` for every existing
row — including PIU Tracker, which reads scores. Nullable is wrong here; the column has a correct
default for every row that exists.

### ScoreTracker.Web — most of the work

| File | Change |
|---|---|
| `Components/CommunityTools/ToolConsoleFrame.razor` | **new** — the shared chrome: tool switcher, section nav, admin banner. Mirrors `OfficialSectionFrame` |
| `Pages/CommunityTools/Developers.razor` | Guts it: picker + empty state, redirect to first tool |
| `Pages/CommunityTools/ConsoleTool.razor` | **new** — the landing route: identity, source, handle, listing, delete |
| `Pages/CommunityTools/ConsoleApi.razor` | **new** — keys, live ones only (was `ToolKeyPanel`) |
| `Pages/CommunityTools/ConsolePlayers.razor` | **new** — invite codes + copy + connected (was `ToolInvitePanel`) |
| `Pages/CommunityTools/ConsoleWebhooks.razor` | **new** — config + verify + test (was `ToolWebhookPanel` + `ToolDebug`) |
| `Pages/CommunityTools/ConsoleInsights.razor` | **new** — conditional stat groups, recent deliveries, latest (was `ToolConsole`) |
| `Pages/CommunityTools/ConsoleCode.razor` | **new** — API/Webhook segmented, C#/Java/Python/TypeScript |
| `Components/CommunityTools/CodeSample.razor` | **new** — one snippet: language tabs, copy, token substitution |
| `Components/CommunityTools/ToolSetupWizard.razor` | Screen split; the kind fork; the crumb rail becomes kind-dependent |
| `Pages/CommunityTools/CommunityToolsDirectory.razor` | Rebuilt: strip, monogram rows, two shapes, Dev Console button |
| `Pages/Admin/CommunityToolsReview.razor` | **+ All tools** section with Manage / Ban |
| `Pages/Admin/Admin.razor` | Link to it |
| `Components/CommunityTools/ToolRulesCard.razor` | Drop the "governs" footer + its `MudLink` |
| `Pages/CommunityTools/ToolConsole.razor`, `ToolDebug.razor` | **Deleted**, replaced by 301 routes |
| `Components/CommunityTools/ToolKeyPanel.razor`, `ToolInvitePanel.razor`, `ToolWebhookPanel.razor` | **Deleted** — their content becomes the sections |
| `wwwroot/css/site.css` | `ct-console-*` (nav, groups, stat tiles, disclosure), `ct-dir-*` (monogram, rows). `ct-wiz-*` mostly survives |
| `Resources/App.*.resx` ×9 | New keys; **~40 retired** — the deleted prose is the point of the exercise |

### Docs

- **`docs/INTEGRATING.md` deleted.** Links in `ToolRulesCard`, `ToolSetupWizard`, `Developers.razor`
  Help, `README.md` and `CLAUDE.md`'s doc index all go.
- `docs/API.md` — note that maker-facing examples live on `/Developers/{id}/code`.
- `api-v2-community-tools.md` §12 — record the browse-first reversal.
- `DATABASE-SCHEMA.md` — `Tool.Kind`.
- **This file** — status → built.

### Tests

| Suite | What |
|---|---|
| `DomainTests/ToolTests` | a listing-only tool cannot request listing without a `Url`; `Kind` survives rehydrate |
| `ApplicationTests` | `Project` reports `HasKeys`/`WebhookConfigured`; summary sums `Calls`/`Clicks`; a click records one roll-up row not one per call |
| `Tests.Components` | **the load-bearing suite here.** Wizard: rules gate Continue, the fork changes the crumb rail, listing-only skips the key. Console: groups hidden when absent, nav renders per kind. Directory: `Visit` vs `Connect` by kind. Webhooks: the record-reload seam below, at both panel and page level |
| `Tests.E2E` | none new — no critical whole-workflow path changes |
| `ArchitectureTests` | `RenderModeDeclarationTests` covers the six new pages automatically; `UiColorTokenTests` covers the new CSS |

### The record-reload seam

Sections save one of two ways, and the split is not cosmetic. Most save and let `NavManager.Refresh()`
reload the document. **Webhooks cannot**: Verify puts a result on screen — a remote's status code, a
failure reason — and a document reload throws away the thing the maker pressed the button for. So it
saves through commands and asks the frame to re-read the record in place (`ToolConsoleFrame.Reload`,
wired through the panel's `OnChanged`).

That callback is load-bearing and silently optional, which is how it shipped unwired: the panel
behaved perfectly on its own, and every control gated on the *saved* record — Verify above all —
stayed gated on the record it replaced. Nothing failed, nothing said why, and no suite noticed,
because no suite rendered the page and the panel together. `ConsoleWebhooksPageTests` now does.

Two rules follow for anything that saves without a page load:

- **The record is the parent's, so the parent has to be told.** A panel that mutates through commands
  and renders from a `ToolRecord` parameter is reading a snapshot, and it goes stale the moment it
  saves.
- **The URL is compared as a URL.** The saved value round-trips through `Uri`, which gives a bare host
  the trailing slash the maker never typed. Compared as text that reads as a pending edit, and Verify
  is disabled forever for anyone whose endpoint is a bare host.

---

## 4. Commit order

Green at every commit.

| # | Commit | Notes |
|---|---|---|
| 1 | `docs(tools): the console redraw design` | this file |
| 2 | `feat(tools): a tool says whether it reads scores` | `ToolKind` + migration + contracts + `RequestListing` rule. Nothing reads it yet |
| 3 | `feat(tools): count what a listed tool actually gets` | `DirectoryClicked`, `RecordClick`, summary `Calls`/`Clicks` |
| 4 | `feat(tools): the console becomes a section` | `ToolConsoleFrame` + six routed pages + 301s. The big one |
| 5 | `refactor(tools): retire the panels the sections replaced` | delete `ToolKeyPanel`/`ToolInvitePanel`/`ToolWebhookPanel`/`ToolConsole`/`ToolDebug` + ~40 resx keys ×9 |
| 6 | `feat(tools): code samples on the site` | `ConsoleCode` + `CodeSample` + `GetToolCodeSamplesQuery` |
| 7 | `feat(tools): registration asks the rules first, then the tool` | wizard split + fork |
| 8 | `feat(tools): the directory is for browsing` | strip, monogram rows, two shapes, Dev Console button, click recording |
| 9 | `feat(tools): every tool, on the admin page` | All tools moves off `/Developers` |
| 10 | `docs(tools): retire the integration guide` | delete + unlink + doc updates |

**5 after 4** — the panels have to still exist while the sections are being built against them.
**6 after 4** — Code is one of the sections. **10 last** — the Help links live in files commits 4
and 7 rewrite, so deleting the doc earlier means touching them twice.

---

## 5. Deferred

- **Directory views.** Cheap (`ToolActivity` already rolls up hourly) but near-identical for every
  listed tool — no paging, no ranking, so everyone gets an impression on every load. It measures how
  busy the directory was, not how interesting one tool is, and a number that reads the same for
  everyone invites makers to compare it and conclude nothing. Clicks and connects ship; views become
  worth having the day there is a click-through *rate* to divide by.
- **Middle-click clicks.** Recording happens in the row's click handler, so a middle-click or
  copy-link is not counted. A redirect endpoint would catch those and would make the link opaque on
  hover — a worse trade on a row whose whole point is "go and read this person's source".
- **Per-key usage.** Roll-ups are keyed to the tool, not the key. Each key's `LastUsedAt` is what
  tells you an old one is safe to revoke.

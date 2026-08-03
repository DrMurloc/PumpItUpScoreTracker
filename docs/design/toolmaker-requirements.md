# Toolmaker requirements

A follow-on pass to [api-v2-community-tools.md](api-v2-community-tools.md), landed before that
feature's first release. It adds three things a tool must carry before it touches anyone else's
data, the published rules for being listed, and a way to stop a maker who breaks them.

> **Status, 2026-08-03: built.** Nine commits, all four suites green at each. Three things landed
> differently from the plan below and the reasons are worth keeping:
>
> - **The ban disables rather than deletes, and its effect is computed.** §4 planned a ban that
>   revoked shares. Writing the effect into the tools would have made Lift restore an empty shell,
>   so the ban is one row and every read resolves against it. The tests bans, asserts nobody can
>   read, lifts, and asserts the deliberate grant came back.
> - **`GetToolIdsReading` needed the gate too.** §2 named `GetReadablePlayerIds` as the one
>   enforcement point. There is a second query building the same pool from its own SQL, and the
>   first integration assertion passed only because re-review had de-listed the tool. Both now run
>   `Tool.Shareable`.
> - **One string in the voice sweep had no resx entry in any locale**, including `en-US`, so it fell
>   back to its key and rendered English everywhere since #212. Fixed in passing.

---

## 1. What this adds

Three fields, one gate, one sanction:

| | What | Why |
|---|---|---|
| **Source repository** | A public git URL, checked to resolve | A listed tool links to it so players can read what they are connecting to |
| **Discord handle** | The maker's, visible only to DrMurloc | Rule 3's actual purpose — a way to reach the maker when something breaks |
| **Agreement** | A checkbox at registration, timestamped | Removing a tool under rule 2 is a shorter conversation with a date attached |
| **Maker ban** | A user-level block on making tools | Rule 2 threatens it; nothing in the software could do it |

## 2. The one rule

Everything above collapses into a single predicate on `Tool`:

```
CanBeSharedWithOthers =
       RepositoryUrl is not null
   AND RepositoryCheckedAt is not null
   AND DiscordHandle is not null
```

**A tool that fails it can read exactly one player: its maker.** It still works — keys mint,
webhooks fire, the maker is auto-connected to their own tool as before, and everything in
`/Dev/Populate`-style local development is unaffected. What it cannot do is acquire a second player.

That covers four entry points, and the distinction between them matters:

| Entry point | Layer | What it does |
|---|---|---|
| `ConnectToolCommand` | Application | Throws `ToolRepositoryRequiredException`, so the player sees a sentence |
| Invite redemption | Application | Same throw — an invite is a connect with a code |
| `RequestListing()` | Domain | Refuses, alongside the existing description rule |
| `GetReadablePlayerIds` | Infrastructure | **Excludes the tool from the all-tools pool** |
| `GetToolIdsReading` | Infrastructure | The same pool, built by a second query — both run `Tool.Shareable` |

The last one is the enforcement; the first three are the error messages. `EFToolRepository`
([EFToolRepository.cs:243](../../ScoreTracker/ScoreTracker.CommunityTools/Infrastructure/EFToolRepository.cs))
is where effective access is actually computed — a handler-level check alone would leave the blanket
pool wide open, because nothing routes through a handler to grant it. This is the same lesson
`AccountPurgeTests` exists for: a mocked port cannot catch an over-permissive SQL predicate.

### The grandfather

PIU Tracker is Public, session-mode, and carries 653 migrated players, and its maker has not
supplied a repository. It is exempted **by tool id**, not by a date — `SeedPiuTrackerTool` stamps
`SYSDATETIMEOFFSET()`, so its `CreatedAt` lands at deploy time and any `CreatedAt` cutoff is a coin
flip against the real makers registering the same week.

The id already lives in `PiuTrackerSessionShape.ToolId` and two migrations with a test pinning them
together; it moves to a Domain constant that all three reference, so the exemption sits beside the
rule it bends. No column, no admin screen, no player-visible marker — the directory simply shows no
**Source** link on that row, which is true of any tool without one.

> Owner, 2026-08-03: implicit, not an admin-set exemption flag. We are not planning around TUSA
> answering in time. When he does, the URL is filled in and the constant goes.

### Reachability

A configured URL is a claim; one that answered is proof. Same argument, same shape, as
`WebhookUrlVerifiedAt` ([Tool.cs:69](../../ScoreTracker/ScoreTracker.CommunityTools/Domain/Tool.cs)) —
a private GitHub repo 404s anonymously and is indistinguishable from a typo, so an unchecked URL
would satisfy the letter of the requirement while defeating its entire purpose.

- Anonymous `GET`, short timeout, no credentials, response body discarded.
- **A changed URL is an unchecked URL** — `SetRepository` clears `RepositoryCheckedAt`, exactly as
  `SetWebhook` clears the webhook proof. Without it, check once and swap to anything.
- The **owner** is parsed from the first path segment and stored for the admin list. It holds for
  GitHub, GitLab, Codeberg and gitea; it does not for sourcehut's `~user` or a nested GitLab
  subgroup. **Stored and displayed, never enforced on** — it is there so a maker who pasted a repo
  they did not write is visible at a glance, which is a judgement only a human makes.
- What this does **not** prove: that the repo holds code, that the code is what is deployed, or that
  it is a git repo at all. It catches dead links, typos, and private repos, which is the failure
  surface that actually occurs.

## 3. The rules, verbatim

Rendered as the first card of the maker wizard, above registration. Localized in all nine locales.
The quoted server rule stays English everywhere, the way the Murloc glossary exempts proper nouns.

> ### DrMurloc's Rules for Integrated Toolmakers
>
> *PIU Scores was built on the principle that we, Pump It Up players, are all one community, divided
> only by physical distance. Aim to connect that community.*
>
> **1. No money in it.** Free to use and free of ads. A tip jar or community fund covering your
> hosting is fine — a supporter tier that unlocks features is not. Anything built to turn a profit
> gets removed.
>
> **2. Built for the community.** Tools that help players understand the game — score distributions,
> progress, analysis anyone can use. A tool built for one person's edge, like scouting opponents
> before a tournament, gets removed, and its maker does not get to make more.
>
> **3. Stay reachable.** Toolmakers stay in the PIU Scores Discord, so I can message you when
> something goes wrong.
>
> **4. Stay in good standing.** The server rule is "Don't Be An Asshole", and it covers toolmakers
> too. I can remove a tool for any reason — including anything discriminatory, or aimed at excluding
> or antagonising any Pump It Up community members.

148 words. The canonical copy is [INTEGRATING.md](../INTEGRATING.md), in English, and governs where
a translation and it disagree — stated in the card's footer.

**Voice.** First person throughout: these are one person's rules, and the site is a one-person team.
That decision is not confined to this card — see §6.

## 4. The ban

A user-level block, reached from the admin tool list on `/Developers`.

- **Disables, never deletes.** `DeleteTool` hard-deletes across eight tables including the activity
  log and every delivery record — the exact evidence a disputed ban needs. So the ban is one row and
  every effect is **computed from it at read time**: their tools read nobody, leave the directory and
  stop minting, while shares, keys and history stay untouched. That is what makes Lift restore a
  working tool rather than an empty shell. Delete remains its own button.
- **Blocks `CreateToolCommand`** for that user id.
- **Confirmation dialog**, deliberately plain, plus a freeform **notes** field that is editable
  afterwards from the tool list. Nobody but an admin ever sees it.
- **`ToolMakerBanEntity` is purge-exempt.** It carries a `UserId`, so `AccountPurgeCoverageTests`
  forces the choice; purging it would mean delete-and-recreate clears a ban, which is the one
  outcome the feature exists to prevent. The exemption carries that sentence as its reason.

## 5. Technical scope

### ScoreTracker.CommunityTools — the only vertical that changes

| Layer | File | Change |
|---|---|---|
| **Contracts** *(public)* | `ToolRecords.cs` | `ToolRecord` +`RepositoryUrl` +`RepositoryOwner` +`RepositoryCheckedAt` +`DiscordHandle` +`AgreedToTermsAt`; `PublicToolRecord` +`RepositoryUrl`; new `ToolMakerBanRecord` |
| | `Commands/ToolCommands.cs` | `UpdateToolCommand` +`RepositoryUrl` +`DiscordHandle`; new `CheckToolRepositoryCommand`, `BanToolMakerCommand`, `LiftToolMakerBanCommand`, `SetToolMakerBanNotesCommand`; `CreateToolCommand` +`AgreedToRules` |
| | `Queries/ToolQueries.cs` | new `GetToolMakerBansQuery`, `IsToolMakerBannedQuery` |
| **Domain** *(internal)* | `Tool.cs` | five properties; `Create`/`Rehydrate` widened; `SetRepository` (clears the check), `MarkRepositoryReachable`, `SetDiscordHandle`; `CanBeSharedWithOthers`; repo joins `Describe`'s re-review set; `RequestListing` gains the gate |
| | `ToolMakerBan.cs` *(new)* | `UserId`, `BannedAt`, `BannedByUserId`, `Notes` |
| | `GrandfatheredTools.cs` *(new)* | the PIU Tracker id, referenced by `PiuTrackerSessionShape` and the gate |
| | `IToolMakerBanRepository.cs` *(new)* | port |
| | `IRepositoryReachabilityClient.cs` *(new)* | port — vertical-local, like `IWebhookDeliveryClient` |
| | `ToolExceptions.cs` | `ToolRepositoryRequiredException`, `ToolMakerBannedException` |
| **Application** *(internal)* | `ToolManagementSaga.cs` | `UpdateToolCommand` writes repo + handle; `CreateToolCommand` guards on ban and stamps agreement; new `CheckToolRepositoryCommand` handler; projection carries the new fields |
| | `ToolMakerBanSaga.cs` *(new)* | ban / lift / notes / queries |
| | `ToolAccessSaga.cs` | `ConnectToolCommand` throws when ungated |
| **Infrastructure** *(internal)* | `Entities/CommunityToolEntities.cs` | five columns on `ToolEntity`; new `ToolMakerBanEntity` |
| | `EFToolRepository.cs` | column mapping; **`GetReadablePlayerIds` excludes ungated tools from the pool** |
| | `EFToolMakerBanRepository.cs` *(new)* | |
| | `RepositoryReachabilityClient.cs` *(new)* | typed `HttpClient`; the vertical already carries `Microsoft.Extensions.Http` for its webhook client |
| | `EFAccountPurgeRepository.cs` | `ToolMakerBanEntity` deliberately absent from `UserOwned` |
| **Wiring** | `CommunityToolsModelContribution.cs` | register `ToolMakerBanEntity` |
| | `CommunityToolsRegistrationExtensions.cs` | reachability client + ban repository |

### ScoreTracker.Data

One migration, `ToolmakerRequirements`: five columns on `scores.Tool`, new `scores.ToolMakerBan`.
All five columns nullable — PIU Tracker predates the requirement and the gate, not the schema, is
what enforces it. No table is dropped, so no `archive` transfer applies.

### ScoreTracker.Web

| File | Change |
|---|---|
| `Components/CommunityTools/ToolSetupWizard.razor` | step 1 becomes the rules card + register card; crumb relabelled |
| `Pages/CommunityTools/Developers.razor` | repo + handle fields in settings; admin rows carry owner, check state and handle; ban entry point |
| `Components/CommunityTools/ToolMakerBanDialog.razor` *(new)* | confirmation + notes |
| `Pages/CommunityTools/CommunityToolsDirectory.razor` | **Source** link per row where one exists |
| `Pages/Admin/CommunityToolsReview.razor` | repo, parsed owner, check state and handle on the review card |
| `Resources/App.*.resx` ×9 | new keys, plus the §6 sweep |

No controller changes: `PublicToolRecord` is consumed only by the directory page, so no `Tests.Api`
golden moves and [API.md](../API.md) is untouched.

### Tests

| Suite | What |
|---|---|
| `DomainTests/ToolTests.cs` | the gate predicate; a changed repo clears its check; the grandfathered id passes ungated; `Describe` re-reviews on a repo change |
| `ApplicationTests/` | a banned maker cannot create; connect throws when ungated; agreement is stamped once |
| `Tests.Integration/` | **`GetReadablePlayerIds` excludes an ungated tool from the all-tools pool** — the assertion a mock cannot make |
| `ArchitectureTests/AccountPurgeCoverageTests.cs` | `ToolMakerBanEntity` exemption + reason |

## 6. The voice sweep

The rules are first person, and the tool screens shipped saying "we" 23 times. Left alone, step 1 of
the wizard says *I* and step 4 says *We POST the scores*.

> Owner, 2026-08-03: "There is no We. I like people remembering I'm a one person team."

Because a resx key **is** its English text, all 23 keys move alphabetical position in all nine files
— 207 values touched. The values themselves need judgement rather than replacement:

- **"I" where a person decides or promises** — "I store a hash, not the key", "I don't send reminder
  emails", "it can do anything you can do there, and I can't limit it."
- **"PIU Scores", or no subject, for mechanical HTTP** — "We POST the scores themselves, 100 per
  delivery" becomes "Scores arrive 100 per delivery". *"I POST the scores"* reads like it is done by
  hand.
- es/it/fr/pt mark this in verb conjugation (`Almacenamos` → `Almaceno`); ja/ko frequently do not
  mark it at all, so those values are unchanged while the row still moves; en-ZW stays inside its
  nine-letter alphabet.

Scope is the CommunityTools screens only. The other ~37 first-person-plural strings site-wide are a
separate pass, not this one.

## 7. Commit order

Green at every commit, and the voice lands early so nothing written after it has to be rewritten.

| # | Commit | Why here |
|---|---|---|
| 1 | `docs(tools): the toolmaker requirements design` | this file; nothing to break |
| 2 | `refactor(tools): the site speaks as one person` | the 23-key sweep, no logic — settles the voice before any new copy is written |
| 3 | `feat(tools): a tool records its source, its maker, and their agreement` | domain fields + entities + migration + mapping. Nothing reads them yet, so behaviour is unchanged |
| 4 | `feat(tools): prove the repository resolves` | reachability client, check command, owner parse |
| 5 | `feat(tools): only a tool with a source and a maker can reach other players` | the gate — repository predicate and handlers together. The one behavioural commit |
| 6 | `feat(tools): registration asks for the rules, the source, and a handle` | wizard cards, settings fields, new resx keys ×9 |
| 7 | `feat(tools): ban a maker` | entity, repository, commands, dialog, purge exemption |
| 8 | `feat(tools): players can read the source` | directory Source links, review-queue fields |
| 9 | `docs(tools): the integration guide carries the rules` | INTEGRATING.md, DATABASE-SCHEMA.md, api-v2 cross-link |

Ordering constraints, explicitly: **3 before 5** — the gate cannot read columns that do not exist.
**2 before 6 and 8** — otherwise new copy is written in a voice the rest of the file has not adopted,
and the sweep then collides with every UI commit. **7 after 5** — banning revokes shares, which is
the gate's own machinery.

## 8. Docs updated in this pass

- **This file** — new.
- [api-v2-community-tools.md](api-v2-community-tools.md) — cross-link, and a line in §16 recording
  that registration grew requirements after that pass was settled.
- [INTEGRATING.md](../INTEGRATING.md) — the rules verbatim as canonical English, the registration
  requirements, and the same voice sweep (23 instances, English-only, no translation cost).
- [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) — `ToolMakerBan` row; `Tool`'s new columns.
- [CLAUDE.md](../../CLAUDE.md) — untouched. No new package, no new layer rule, no new ratchet
  category; the purge exemption is an entry in a list that already exists.

## 9. Deferred

- **Re-review does not fire on monetization.** A tool approved clean can add ads the following week;
  the re-review triggers are name, description, URL and now repository, none of which an ad rollout
  touches. Rule 1 is enforced by someone noticing. A player-facing **report this tool** control is
  the cheap lever and is not in this pass.
- **Terms versioning.** Rejected as overkill (owner, 2026-08-03) — `AgreedToTermsAt` records when,
  not which text.
- **Discord membership is not verified.** Rule 3 asks makers to stay in the server; we store a handle
  so DrMurloc can reach them, and check nothing. A guild-membership gate would need a bot lookup and
  would still fail every maker who signed in with Google.
- **The site-wide voice sweep** — ~37 further first-person-plural strings outside CommunityTools.

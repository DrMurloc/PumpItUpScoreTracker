# Stage 5 - the design docs go away

The owner's decision: "we want to switch to specs. designs are going away." The code cites design docs in comments
(`(docs/design/seasons.md D27)`, `rise.md §12`, `[...](design/rise.md)`), measured on 2026-11-22 in the scan's own forms
at about 1,340 path, 250 link, 110 bare-name and 1,410 token items in about 970 tracked files. This
stage repoints each one to the requirement that now holds the cited rule, or drops it when the rule became no
requirement; deletes `docs/design/` (its history stays in git); and re-aims the citation ratchet at "no tracked text
cites `docs/design/`". It is a re-aim, not a removal: ratchets here are never removed.

Run it after stage 3, when every requirement ID is final.

## Steps

1. **Inventory:** `python -I <wd>/tooling/design_cites.py inventory . <wd>`. Compare the counts with the numbers
   above; a big drop means the scan missed a form. It also records the design docs' file names, so the final check
   still knows them once the folder is gone.
2. **The map:** `python -I <wd>/tooling/design_cites.py map <wd>` reads the writers' `## Design rows` lines, drops IDs
   that are no requirement, and writes `design/gaps.json`: the cited rows no writer mapped.
3. **Fill the gaps:** `bake.py <wd> designmap --model sonnet`, Workflow `{scriptPath: "<wd>/tooling/designmap-baked.js"}`
   (one agent per design doc with gaps), save the output, `design_cites.py merge <wd> <output>`, then `map` again.
   Repeat once if gaps remain; settle the last few by hand in `design/map-extra.json` (a list of `{doc, token, ids,
   spec, none}`). Row tokens a gap agent could not place are mapped to `none` with the reason.
4. **Non-markdown files under `docs/design/`** (SQL seeds, HTML mocks): the inventory lists every reference to
   them. A file something still uses (a dev harness, a test, a how-to) moves to where its user lives, in this
   stage's commit; one nothing uses goes with the folder. List each call in the run log.
5. **Repoint:** `bake.py <wd> repoint --model sonnet` (batches of 40 files), Workflow
   `{scriptPath: "<wd>/tooling/repoint-baked.js"}`. Each agent edits only comments and documentation prose in its
   own files. Save the output; collect every `left` item into the run log and settle them by hand.
6. **The hand-edited files** the bake leaves out, because their sentences change, not only their citations:
   - `CLAUDE.md`: the UI conventions' design-doc links become requirement IDs; the documentation-set lines about
     design docs and `DesignDocCitationTests` say what is true after this stage (see step 8).
   - `README.md`, `docs/SPECS.md`, `docs/specs/README.md`: the "until it is extracted, its design doc is its
     record" sentences go; SPECS.md's "as code cites a design doc's D-rows today" becomes "as code cites a
     requirement ID".
   - `.claude/skills/**` (outside this skill): repoint as for code.
   - `ScoreTracker/ScoreTracker.Tests/ArchitectureTests/DesignDocCitationTests.cs`: step 8.
7. **Delete the folder:** `git rm -r docs/design`.
8. **Re-aim the ratchet** (`DesignDocCitationTests`, same file and class):
   - `EveryCitedDesignDocExists` becomes **`NoTrackedTextCitesTheDesignDocs`**: through `SourceTree.Files()`, every
     text file (the same extensions) is read and none may hold `docs/design/` or `docs\design\`; inside `docs/`,
     no markdown link may start with `design/` or `../design/`. The failure message lists each `file:line` and says
     to cite the requirement that holds the rule (docs/SPECS.md). A floor: the scan read at least one file of each
     kind it reads.
   - The test's own source must not trip it: build the needle (`"docs/" + "design/"`) and the test data without the
     literal, and rewrite `TheCitationPatternFindsDesignDocPaths` as a `[Theory]` over constructed strings that the
     needle finds or ignores.
   - `EveryRelativeMarkdownLinkInsideDocsResolves` stays as it is: it now also catches a link to a deleted doc.
   - `TheWalkFindsAKnownFileAndSkipsBuildOutput` asserts `docs/SPECS.md` instead of `docs/design/rivals.md`; the
     `IsSkipped` theory's `docs/design/rivals.md` row becomes `docs/specs/README.md`.
   - The XML doc says the rule now: the design docs are retired (their history is in git), so a citation of one
     points at nothing; code cites the requirement that holds the rule.
   - CLAUDE.md's line about `DesignDocCitationTests` is rewritten to match, in the same commit.
9. **Check:** `python -I <wd>/tooling/design_cites.py check . <wd>` exits 0 (no path, link or bare-name citation
   left outside this skill's folder); `dotnet build` (a comment edit can break an XML doc); `dotnet test
   ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj` green.
10. **Commits,** in this order:
    - `docs: code cites the requirement that holds a rule, not a design doc` (the repoint, steps 5-6; split by area
      if the diff is large: one commit per vertical is fine);
    - `docs: the design docs are retired; their history stays in git` (step 4's moves and step 7);
    - `test(architecture): no tracked text cites the retired design docs` (step 8).
    Push.

## What the owner sees

Every comment that cited `docs/design/x.md D12` now cites `WEEKLY-004` (or the spec file, for a whole-doc
citation), and a reader finds the rule with the index or `--filter "Verifies=WEEKLY-004"`. A rule that became no
requirement lost its citation; its reason is in `design/map.json` (working folder) if he asks.

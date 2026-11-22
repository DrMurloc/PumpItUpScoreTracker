# Stage 7 - wrap up

## The docs say what is true now

In one commit, `docs: specs are the record of what each feature does`:

- **docs/SPECS.md:** the design docs are gone, so the "until a feature's design doc is extracted" paragraph and the
  "design rationale stays in docs/design/" line go; "as code cites a design doc's D-rows today" becomes "as code
  cites a requirement ID"; **What holds it** lists the checks by class (stage 6) instead of "Nothing yet"; **Every
  requirement has a test** gains one sentence: a new spec joins the coverage check's held set in the pull request
  that adds it, and a spec joins it once every requirement in it is named.
- **CLAUDE.md:** the documentation-set entries for SPECS.md and the spec index lose the design-doc clause; in the
  copied-skill table, "the spec ratchets" now map to `ScoreTracker.Tests/ArchitectureTests/Specs/` and
  `RequirementGapInventory.cs` maps to "none: the coverage check holds the specs in `RequirementCoverageTests`'
  held set"; the citation-ratchet line matches stage 5.
- **README.md** and any doc that pointed readers at the design docs point at the spec index.
- `python -I <wd>/tooling/design_cites.py check . <wd>` and `speclint.py .` are still clean afterwards.

## Before the pull request

1. **Sweep the triage once more:** every item ruled after a spec was written (`added_in` later than the spec's
   batch) is checked against that spec's text, by hand or with one reviewer agent per affected spec.
2. **The private findings:** `python -I <wd>/tooling/findings_md.py <wd>` writes `Bugs.md` and `Security.md` to the
   findings folder. Nothing from them goes into a commit, a spec, the pull request or an issue.
3. **All five suites:** `ScoreTracker.Tests`, `.Tests.Api` and `.Tests.Components`, then `.Tests.Integration` and
   `.Tests.E2E` (Docker). The exploration workbench is not run.
4. **The AI review** of the whole branch with the repo's `ai-review` skill, then fix what it sends back.
5. **The cost:** the final `sum_usage.py` totals go in the run log.

## The pull request

`gh pr create` (draft until the owner says otherwise), titled `Specs extracted from the code; the design docs
retired`. The body, written from a file:

- what landed: the number of folders, specs and requirements; the traits added and how many specs the coverage
  check holds; the design-doc citations repointed and the docs retired; the checks;
- how to review: approving a spec confirms it (docs/SPECS.md); review folder by folder from the index; a rule that
  looks wrong is a finding, reported to the owner, not a reason to change the code in this pull request;
- the commits, in order.

**Never in the body:** a finding, a security item, a count of either, a hostname, a player's name. The findings
reach the owner as files, not as public text.

## The report to the owner

A few lines: the pull request link; specs and requirements; the specs the coverage check holds and the count on
`untagged.md`; the citations repointed; the total cost against the estimate; the private files (full paths) -
`Bugs.md`, `Security.md`, `security-triage.md`, `untagged.md`; then the calls the run took, and a numbered list of
anything waiting on him.

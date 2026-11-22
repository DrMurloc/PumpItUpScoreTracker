# Stage 6 - the spec checks land with the specs

CLAUDE.md: a new ratchet lands at zero - the pull request that adds it fixes every violation, or the ratchet covers
only what already follows its rule, and it never lands with a list of offenders to shrink later. The extraction is
the pull request that gives the checks something to hold, so they land in it. This design was written for PIU from
Astrid's spec ratchets, which paid twice for their grammar (the first reader was replaced 13 days after it was
written); it keeps what survived and leaves out what PIU has no use for yet.

The main session writes these, not a workflow: it is one coherent piece of code with a build-and-test loop. About
800 to 1,000 lines with their tests, all test-only.

## Where they live

`ScoreTracker/ScoreTracker.Tests/ArchitectureTests/Specs/`, beside the other ratchets, each class explaining its rule
in its XML doc (CLAUDE.md: the classes are the ratchet map). They walk the repo through **`SourceTree`**
(`ArchitectureTests/SourceTree.cs`, already there): `RepoRoot`, `Files()`, repo-relative paths, folders skipped on
the repo-relative path. No new package: the front matter is hand-parsed, which keeps the test project's
`xunit + Moq` allowlist and is stricter than YAML.

## The day-one set

| # | Check | Class and facts | speclint counterpart |
|---|---|---|---|
| C1 | **The reader and the shape.** Every `docs/specs/<context>/<feature>.md` reads, and has the shape docs/SPECS.md gives | `SpecReader` (internal), `SpecLint` (internal static, the rules as functions returning violations), `SpecRatchetTests.EverySpecHasTheShapeSpecsMdGives` | `lint_file` shape rules |
| C2 | **Public-repo safety.** No email outside the example domains, no URL with a real host, no hostname, no key or token (`sk-ant-`, GitHub, AWS, Google, Slack, Discord bot tokens, JWTs, private keys, password or key assignments), no connection string, no Discord webhook URL, no 17-20 digit number, no pull request, issue or commit reference | `SpecRatchetTests.NoSpecCarriesWhatAPublicRepoMustNot` | `safety()` |
| C3 | **IDs, prefixes and live tags.** Every ID is unique and takes its spec's prefix; every prefix is declared by one spec, is two to eight capitals or digits starting with a letter, and is never reserved (ADR, SHA, AES, OAEP, RFC, ISO, UTF, CVE) or retired; no removed ID is used again; every claimed surface is claimed once; an ID-shaped token whose prefix a spec declares names a live requirement; every `[Trait("Verifies", "<ID>")]` in a CI suite names a live requirement | `SpecRatchetTests.EveryRequirementIdIsUniqueAndTakesItsSpecsPrefix`, `.EveryPrefixIsDeclaredOnce`, `.NoRemovedIdentifierIsUsedAgain`, `.EverySurfaceIsClaimedOnce`, `.EveryVerifiesTraitNamesALiveRequirement` | IDs, prefixes, claims, mentions; `tags.py gaps` |
| C5 | **Coverage, for the specs whose tests are tagged.** Every requirement of a held spec has at least one test naming it | `RequirementCoverageTests` with a `HeldSpecs` set | `tags.py gaps` |
| C7 | **The index.** `docs/specs/README.md` links exactly the specs on disk, each line showing its prefix; it has a `## Removed` register; nothing but the README and `<context>/<feature>.md` files sits under `docs/specs/` | `SpecRatchetTests.TheIndexListsExactlyTheSpecsOnDisk`, `.OnlySpecsAndTheIndexSitUnderDocsSpecs` | `lint_index`, `lint_layout` |
| C8 | **One repo-file helper.** | `SourceTree` - exists; use it, add nothing | - |

(C4, a custom `[Verifies]` attribute, is settled: docs/SPECS.md uses xUnit's built-in `[Trait("Verifies", ...)]`.
C6, every `paths` glob matches a file, waits for `paths` to come back. Both keep their numbers so the design reads
against Astrid's.)

## How each part is built

- **Reading, BOM- and CRLF-safe.** `File.ReadAllText` (it drops a UTF-8 BOM), then `Replace("\r\n", "\n")` before
  any regex: the fast suites run on Windows checkouts with CRLF, the Docker suites on Linux with LF, and a `(?m)$`
  anchored before the normalization matches differently on each.
- **Front matter, hand-parsed** exactly as `speclint.py` does: `prefix: X`; `claims:` with indented
  `kind: [a, b]` lines or `- item` block lists; kinds `routes`, `api`, `jobs`, `discord`, `events`; any other key
  (`paths`, `Status`, `owner`, `visibility`) is a violation; quotes are stripped; a comment line is a violation.
- **The body:** one `# ` title; `## Purpose` (not empty) then `## Requirements`, nothing else at level 2;
  `### Requirement: <ID> <name>`; the statement is the first paragraph and carries `MUST|SHALL` and no
  `SHOULD|MAY`; at least one `#### Scenario:`; each scenario's lines are `- **GIVEN|WHEN|THEN|AND|BUT** ...`
  with exactly one WHEN and at least one THEN; an examples table only at a scenario's end; no `Status` or `Out`
  lines; at most 30 requirements. Fenced code blocks are skipped.
- **The Verifies scan** reads `.cs` files whose repo-relative path starts with `ScoreTracker/ScoreTracker.Tests/` or
  `ScoreTracker/ScoreTracker.Tests.` (so `ScoreTracker.ExplorationTests` never counts). It matches the attribute
  form only, never comments, so prose that mentions an ID never bites. Any `Trait("Verifies", ...)` or
  `Trait("verifies", ...)` whose value is not a plain `"<PREFIX>-<NNN>"` literal is a violation: a misspelt key or
  a constant would otherwise make a requirement look untested, or tested when it is not.
- **The coverage check's scope** is `HeldSpecs`, a set of spec paths seeded from `<wd>/tags/held.json` on landing:
  the specs whose every requirement a test names that day. It lands green by construction. It only grows: a spec
  joins it in the pull request that names its last requirement, and a new spec joins it in the pull request that
  adds it (docs/SPECS.md gets that sentence in stage 7). Its XML doc says so. It is the check's scope, not a list of
  offenders: nothing on it is waiting to be fixed. The untagged requirements stay in `<wd>/untagged.md`, outside the
  repo.
- **Floors**, so a broken scan cannot pass: the tree holds at least one spec and the reader found at least as many
  requirements as specs; the Verifies scan read test files and found at least one trait; every `HeldSpecs` entry is a
  spec on disk.
- **The scanner is pinned:** `SpecLintTests`, a `[Theory]` over one synthetic spec (an inline string, in PIU's
  words, `example.com` addresses) in three encodings - LF, CRLF, and CRLF with a BOM - where the clean spec has no
  violation, and one `[Theory]` row per mutation, each producing its violation: a `paths` key, a Status line, SHOULD
  in a statement, a statement with no MUST, two WHENs, no THEN, a duplicate ID, an ID with another prefix, a reserved
  prefix, a real email, a hostname, a real URL, an `sk-ant-` key, a Discord webhook URL, a 18-digit number, a
  connection string, `(#388)`, a GitHub pull request link, an Out line, a malformed requirement heading. The same
  rows exist as speclint's synthetic cases; keep the two in step.

## Landing at zero

1. `python -I <wd>/tooling/speclint.py .` reports no ERROR and `tags.py gaps` exits 0.
2. Write the checks; `dotnet build`; `dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj`.
3. A check that fails where speclint passed is a parity bug: decide which side is wrong against docs/SPECS.md, fix
   it, and fix the other side to match. Never an allowance.
4. Commit `test(architecture): the spec checks - shape, public-repo safety, IDs, live tags, coverage and the index`.
   Push.

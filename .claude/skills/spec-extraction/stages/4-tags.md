# Stage 4 - tests name what they verify

docs/SPECS.md: a test that proves a requirement carries `[Trait("Verifies", "<ID>")]`, one trait per ID, in any
suite CI runs, never in the exploration workbench. The writers listed, per requirement, the tests that plainly prove
it (they would fail if the rule broke); the reviewers checked those lines. This stage turns them into traits with a
script, so no agent edits a test file.

1. `python -I <wd>/tooling/tags.py plan <wd>`: resolves every `Tests:` line to a `[Fact]` or `[Theory]` method.
   Read the unresolved list: a renamed method or a wrong path is fixed in the evidence note and planned again; an
   exploration test or a helper is dropped.
2. `python -I <wd>/tooling/tags.py apply <wd>`: adds the traits (idempotent; keeps BOMs and line endings).
3. `dotnet build ScoreTracker/ScoreTracker.sln -c Debug`, then prove the traits are discovered on one ID per suite
   touched: `dotnet test <suite csproj> --filter "Verifies=<ID>"` runs the tagged tests and only them.
4. `python -I <wd>/tooling/tags.py gaps <wd>`: exit 0 means every trait names a live requirement as a plain literal.
   It writes `tags/held.json` (the specs whose every requirement is named: stage 6's held set) and `untagged.md`
   (the requirements no test names yet). **`untagged.md` stays in the working folder**: it is the owner's private
   to-do list, never a gap list in the repo.
5. Commit `test: tests name the requirements they verify`. Push.

Do not write new tests in this stage. A requirement with no test stays on `untagged.md`; the coverage check holds
only fully named specs (stage 6), and every later change brings its own test (docs/SPECS.md).

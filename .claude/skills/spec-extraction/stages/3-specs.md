# Stage 3 - the specs

One writer per spec writes the spec and its evidence note, then runs a skeptic pass over its own work. When every
writer in a folder group (at most six specs) is done, one reviewer re-derives the group from the code, fixes it in
place and lints it. Groups move through independently (a pipeline), shared-rule owners first.

## Batches

Run the specs in batches so the meter can be read between them, and so the first batch calibrates the rest:

1. **The first batch** is the folders that own the most-cited shared rules (the shape's `order` puts them first):
   `bake.py <wd> specs --folders <a>,<b> --effort medium`. When it lands, read two of its specs yourself against
   the code. Fold every systemic mistake into `calls.md` under Lessons (a rule writers keep breaking), and add the
   two best reviewed specs to `general.json`'s `styleRefs`.
2. **Then the rest**, a few folders or `--limit 20` at a time: `bake.py <wd> specs --limit 20 --effort medium`
   (written specs are skipped).

## Each batch

1. `python -I <wd>/tooling/bake.py <wd> specs ...`, then Workflow `{scriptPath: "<wd>/tooling/extract-lean-baked.js"}`.
   Record the run id. Stopped early: the same scriptPath with `resumeFromRunId`.
2. Save the output to `<wd>/runs/specs-<n>-out.json`; `python -I <wd>/tooling/after_run.py <wd> specs-<n>
   <wd>/runs/specs-<n>-out.json`. It saves refused writes, records the written specs, and adds NEW security items to
   the triage as keep-out.
3. **Rule each NEW security item** under the policy the owner approved (no check-in), in `shape/triage.json`, and
   note it in the run log for the owner. Earlier specs are swept against items ruled later in stage 7.
4. **Fix `left_for_others`** by hand (the orchestrator is the only one editing across groups). Re-bake any `failed`
   spec with `--specs <context>/<feature>`.
5. `python -I <wd>/tooling/gen_index.py . <wd>/general.json`, then `python -I <wd>/tooling/speclint.py .` until it
   reports no ERROR.
6. **Commit** only this batch's files: `git add docs/specs/<folder>/ docs/specs/README.md`, message
   `docs(specs): the <folder> specs, extracted from the code`. Push. Add the running total to the run log.

## After the last batch

- **Pending see-alsos:** `grep -l "Pending see-alsos" <wd>/evidence -r`; resolve each now that every owner exists
  (cite `(see <ID>)` in the relying spec), lint, commit `docs(specs): rules cite the requirement they rely on`.
- **One word per concept across folders:** read the index and the `## Words` sections; fix drift by hand.
- **The whole tree lints clean** and the index lists every spec.

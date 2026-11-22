# Stage 2 - the shape

The shape decides the tree before anything is written: folders, specs, prefixes, claims, which spec owns each shared
rule, which spec holds each design doc's rules, the security triage, and the questions the specs must answer.

## Run it

1. `python -I <wd>/tooling/bake.py <wd> shape`, then Workflow `{scriptPath: "<wd>/tooling/shape-baked.js"}`. Record
   the run id in the run log.
2. Save the Workflow's output to `<wd>/runs/shape.json`, then `python -I <wd>/tooling/shape_out.py <wd>
   <wd>/runs/shape.json`. It writes `shape/` and the private `security-triage.md` in the findings folder, and prints
   the open coverage problems.
3. **Close every coverage problem before stage 3:**
   - an inventory item the code proves wrong, missing or misnamed: a corrections file (`--corrections`, see the
     script's usage), each correction proven in the code;
   - a tree problem (an unclaimed route, a doubly claimed job, a design doc with no home): edit
     `shape/shape-run.json`'s `final` by hand and run `shape_out.py <wd> <wd>/shape/shape-run.json` again.
   A design doc with no home is not a nuisance: stage 5 repoints every citation of it from this attachment.
4. Add the running total to the run log (`sum_usage.py`).

## The check-in (the one the run always makes)

A short message, then a numbered list of what the owner decides:

- **The tree:** folders, specs with prefixes and estimated sizes, the shared-rule owners, the design docs no feature
  holds (`shape/shape.md`). Point at the file; summarize the counts.
- **The security policy calls**, each with its recommendation. The detail stays in the private
  `security-triage.md`; the chat names an item by its short id and what kind of thing it is, never its mechanics.
- **The shape's open questions,** with recommendations.
- **The estimate again**, from the shape's actual size (specs times the per-spec cost above). If it moved past the
  agreed number, say so here.

When the owner answers:

- In `shape/triage.json`, set each ruled item's `class` and `spec_wording` and `ruled: true`; add a `ruling_note`.
- Put the rulings and the shared-rule owners in `calls.md`, and the rulings in `general.json`'s `decisions` too
  (dated, his words).
- Re-run `shape_out.py` on `shape/shape-run.json` if the tree changed; it keeps the rulings.

Nothing to commit in this stage.

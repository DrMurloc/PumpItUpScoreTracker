"""After the shape workflow: save its output where later stages read it, and print the check-in material.

usage: python -I shape_out.py <wd> <workflow output file> [--corrections <json>]

<workflow output file> is the Workflow tool's saved output: JSON holding the script's return value, bare or under
"result" (as a value or a JSON string).

--corrections applies the orchestrator's inventory corrections, each proven in the code, and re-runs the coverage
check (shape.js's coverage(), ported): {"why": "...", "add": {"<kind>": [{"id", "file", "note"}]},
"rename": {"<kind>": {"<old id>": "<new id>"}}, "drop": {"<kind>": ["<id>"]}}.

Writes, under <wd>/shape/:
- shape-run.json   everything the run returned: leads for later stages, never proof
- shape.json       {"final": ..., "coverage": ...}: the plan bake.py reads
- shape.md         the plan for people: tree, prefixes, sizes, owners, design docs, order, open questions
- triage.json      the security triage, every item ruled false until the owner rules at the check-in
- questions.json   the routed question set
and the triage with its detail to <findingsDir>/security-triage.md (outside the repo; never commit or paste it).
Prints the open coverage problems: nothing is extracted while one is open.
"""
import json, os, re, sys

KINDS = ("routes", "api", "jobs", "discord", "events")
RESERVED = {"ADR", "SHA", "AES", "OAEP", "RFC", "ISO", "UTF", "CVE"}


def recompute_coverage(final, inventory):
    norm = lambda s: str(s).strip().lower()
    js = lambda o: json.dumps(o, ensure_ascii=False, separators=(",", ":"))
    parked = norm(js(final.get("not_spec") or []) + js(final.get("dead_code") or []))
    feats = [dict(f, at=c["folder"] + "/" + f["feature"]) for c in final["contexts"] for f in c["features"]]
    out = {}
    for k in KINDS:
        claimed = {}
        for f in feats:
            for i in f.get(k) or []:
                claimed.setdefault(norm(i), []).append(f["at"])
        inv = [norm(x["id"]) for x in inventory.get(k) or []]
        out[k] = {"unclaimed": [i for i in inv if i not in claimed and i not in parked],
                  "double_claimed": [f"{i} -> {', '.join(v)}" for i, v in claimed.items() if len(v) > 1],
                  "not_in_inventory": [i for i in claimed if i not in inv]}
    for k in ("flags", "cookies"):
        placed = {norm(i) for f in feats for i in f.get(k) or []}
        inv = [norm(x["id"]) for x in inventory.get(k) or []]
        out[k] = {"unplaced": [i for i in inv if i not in placed and i not in parked],
                  "not_in_inventory": [i for i in placed if i not in inv]}
    attached = {norm(d) for f in feats for d in f.get("design_docs") or []}
    retired = {norm(r["doc"]) for r in final.get("retired_design_docs") or []}
    docs = [norm(x["id"]) for x in inventory.get("design_docs") or [] if norm(x["id"]).endswith(".md")]
    out["design_docs"] = {"unplaced": [d for d in docs if d not in attached and d not in retired],
                          "not_in_inventory": [d for d in attached if d not in docs]}
    owners = {}
    for f in feats:
        for t in f.get("owns_shared_rules") or []:
            owners.setdefault(norm(t), []).append(f["at"])
    cited = []
    for f in feats:
        for t in f.get("cites") or []:
            if norm(t) not in owners and norm(t) not in cited:
                cited.append(norm(t))
    out["shared_rules"] = {
        "several_owners": [f"{t} -> {', '.join(v)}" for t, v in owners.items() if len(v) > 1],
        "cited_without_owner": cited,
        "owner_cites_itself": [f"{f['at']}: {t}" for f in feats for t in f.get("cites") or [] if f["at"] in owners.get(norm(t), [])],
    }
    prefixes = {}
    for f in feats:
        prefixes.setdefault(f["prefix"], []).append(f["at"])
    out["prefix_problems"] = [f"{p} -> {', '.join(v)}" for p, v in prefixes.items()
                              if len(v) > 1 or not re.fullmatch(r"[A-Z][A-Z0-9]{1,7}", p) or p in RESERVED]
    out["sizes"] = ", ".join(f"{f['at']}={f.get('est_requirements')}" for f in feats)
    return out


def apply_corrections(inventory, corr):
    for kind, items in (corr.get("add") or {}).items():
        have = {x["id"] for x in inventory.setdefault(kind, [])}
        inventory[kind] += [x for x in items if x["id"] not in have]
    for kind, pairs in (corr.get("rename") or {}).items():
        for x in inventory.get(kind) or []:
            if x["id"] in pairs:
                x["id"] = pairs[x["id"]]
    for kind, ids in (corr.get("drop") or {}).items():
        inventory[kind] = [x for x in inventory.get(kind) or [] if x["id"] not in ids]


def load_result(path):
    raw = json.load(open(path, encoding="utf-8"))
    r = raw.get("result", raw) if isinstance(raw, dict) else raw
    if isinstance(r, str):
        r = json.loads(r)
    if not isinstance(r, dict) or "final" not in r:
        sys.exit(f"{path} does not hold the shape's return value (no 'final')")
    return r


def coverage_problems(cov):
    out = []
    for k in KINDS:
        for label in ("unclaimed", "double_claimed", "not_in_inventory"):
            out += [f"{k} {label}: {x}" for x in (cov.get(k) or {}).get(label) or []]
    for k in ("flags", "cookies", "design_docs"):
        for label in ("unplaced", "not_in_inventory"):
            out += [f"{k} {label}: {x}" for x in (cov.get(k) or {}).get(label) or []]
    for label in ("several_owners", "cited_without_owner", "owner_cites_itself"):
        out += [f"shared rule {label}: {x}" for x in (cov.get("shared_rules") or {}).get(label) or []]
    out += [f"prefix: {x}" for x in cov.get("prefix_problems") or []]
    return out


def render_md(final, problems):
    L = ["# The shape", "", "Generated by shape_out.py from the shape workflow's output. A map, not proof.", ""]
    for c in final["contexts"]:
        L += [f"## {c['folder']}/ - {c['name']}", "", c.get("basis", ""), ""]
        L += ["| Spec | Prefix | Est. rules | Owns | Design docs |", "|---|---|---|---|---|"]
        for f in c["features"]:
            docs = "; ".join(d.replace("docs/design/", "") for d in f.get("design_docs") or [])
            L.append(f"| {f['feature']}.md - {f['title']} | {f['prefix']} | {f.get('est_requirements', '?')} | "
                     f"{'; '.join(f.get('owns_shared_rules') or [])} | {docs} |")
        L.append("")
    L += ["## Order", "", ", ".join(final.get("order") or []), ""]
    if final.get("retired_design_docs"):
        L += ["## Design docs no feature holds", ""] + [f"- {r['doc']}: {r['why']}" for r in final["retired_design_docs"]] + [""]
    if final.get("sensitive_contexts"):
        L += ["## Sensitive contexts", ""] + [f"- {s['context']}: {s['why']}" for s in final["sensitive_contexts"]] + [""]
    if final.get("open_questions"):
        L += ["## Open questions", ""] + [f"- {q['question']} Recommendation: {q['recommendation']}" for q in final["open_questions"]] + [""]
    if final.get("coined_words"):
        L += ["## Words that look coined", ""] + [f"- {w['word']} ({w['where']})" for w in final["coined_words"]] + [""]
    L += ["## Coverage", "", f"{len(problems)} open problem(s)." if problems else "No open problems.", ""]
    L += [f"- {p}" for p in problems]
    return "\n".join(L) + "\n"


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    wd, out_file = sys.argv[1], sys.argv[2]
    r = load_result(out_file)
    if "--corrections" in sys.argv:
        corr = json.load(open(sys.argv[sys.argv.index("--corrections") + 1], encoding="utf-8"))
        apply_corrections(r.setdefault("inventory", {}), corr)
        r.setdefault("inventory_corrections", []).append(corr)
        r["coverage_workflow"] = r.get("coverage_workflow") or r.get("coverage")
        print("corrections applied")
    r["coverage"] = recompute_coverage(r["final"], r.get("inventory") or {})
    final, cov = r["final"], r["coverage"]
    shape_dir = os.path.join(wd, "shape")
    os.makedirs(shape_dir, exist_ok=True)

    def dump(name, obj):
        with open(os.path.join(shape_dir, name), "w", encoding="utf-8", newline="\n") as f:
            json.dump(obj, f, ensure_ascii=False, indent=1)

    dump("shape-run.json", r)
    dump("shape.json", {"final": final, "coverage": cov})
    old = {t["id"]: t for t in json.load(open(os.path.join(shape_dir, "triage.json"), encoding="utf-8"))} \
        if os.path.exists(os.path.join(shape_dir, "triage.json")) else {}
    triage = []
    source = final.get("triage")
    if not source:
        # The revise step failed: fall back on the security inventory's proposed classes, all unruled.
        source = [{"id": s["id"], "title": s["title"], "where": s["where"], "what_happens": s["what_happens"],
                   "class": s["proposed_class"], "spec_wording": s["proposed_spec_wording"],
                   "affected_spec": s.get("affected_feature", ""), "why": s["why"]}
                  for s in (r.get("security") or {}).get("items") or []]
    for item in source:
        item = dict(item)
        prev = old.get(item["id"], {})
        item["added_in"] = prev.get("added_in", "shape")
        item["ruled"] = prev.get("ruled", False)
        if prev.get("ruled"):
            item["class"], item["spec_wording"] = prev["class"], prev["spec_wording"]
        triage.append(item)
    triage += [t for i, t in old.items() if i not in {x["id"] for x in triage}]
    dump("triage.json", triage)
    dump("questions.json", final.get("question_set") or [])
    problems = coverage_problems(cov)
    with open(os.path.join(shape_dir, "shape.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write(render_md(final, problems))

    g = json.load(open(os.path.join(wd, "general.json"), encoding="utf-8"))
    findings = g.get("findingsDir")
    repo = g["codebase"]["repo"]
    if findings and os.path.commonpath([os.path.abspath(repo), os.path.abspath(findings)]) != os.path.abspath(repo):
        os.makedirs(findings, exist_ok=True)
        P = ["# Private: the security triage from the shape", "",
             "Never commit, paste or share this file. It holds the detail the specs leave out.", ""]
        for t in triage:
            P += [f"## {t['id']} - {t['title']} ({t['class']}{'' if t.get('ruled') else ', not ruled yet'})", "",
                  f"- Where: {t['where']}", f"- What happens: {t['what_happens']}",
                  f"- A spec may say: {t['spec_wording']}", f"- Spec: {t.get('affected_spec', '')}", f"- Why: {t['why']}", ""]
        for p in final.get("policy_calls") or []:
            P += [f"## Policy call {p['id']}", "", p["question"], f"Recommendation: {p['recommendation']}",
                  f"Items: {', '.join(p.get('items') or [])}", ""]
        with open(os.path.join(findings, "security-triage.md"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(P) + "\n")
        print(f"private triage written to {os.path.join(findings, 'security-triage.md')}")
    else:
        print("findingsDir is unset or inside the repo: the private triage was NOT written (fix general.json)")

    specs = sum(len(c["features"]) for c in final["contexts"])
    est = sum(f.get("est_requirements") or 0 for c in final["contexts"] for f in c["features"])
    by_class = {}
    for t in triage:
        by_class[t["class"]] = by_class.get(t["class"], 0) + 1
    print(f"shape: {len(final['contexts'])} folders, {specs} specs, about {est} requirements; triage {len(triage)} {by_class}; "
          f"questions {len(final.get('question_set') or [])}; policy calls {len(final.get('policy_calls') or [])}; "
          f"open questions {len(final.get('open_questions') or [])}; design docs retired {len(final.get('retired_design_docs') or [])}")
    for c in final["contexts"]:
        print(f"  {c['folder']}/: " + ", ".join(f"{f['feature']} ({f['prefix']}, ~{f.get('est_requirements', '?')})" for f in c["features"]))
    print(f"coverage: {len(problems)} open problem(s)" + (" - nothing is extracted until they are closed" if problems else ""))
    for p in problems[:60]:
        print("  " + p)
    if len(problems) > 60:
        print(f"  ... and {len(problems) - 60} more (see shape/shape.md)")


if __name__ == "__main__":
    main()

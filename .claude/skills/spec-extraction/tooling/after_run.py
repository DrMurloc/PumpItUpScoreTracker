"""After a spec-writing run: save its result and what the orchestrator handles next.

usage: python -I after_run.py <wd> <run-name> <workflow output file>

- Saves the result to <wd>/runs/<run>.json, and writes any file the harness refused a writer ("unwritten"), but
  only under the repo's docs/specs/ or <wd>/evidence/.
- <wd>/runs/written.json gains this run's specs that exist on disk (bake.py skips them next time).
- NEW security items (the writers' and reviewers' reports, and "NEW:" lines in the evidence notes' ## Security)
  join <wd>/shape/triage.json as keep-out, ruled false, until the orchestrator rules them under the owner's
  approved policy; they are also listed in <wd>/runs/<run>-new-security.json.
- Prints the failed specs, the groups not reviewed or not lint-clean, the problems reviewers left for other
  groups, and the files to commit (docs/specs only).
"""
import json, os, re, subprocess, sys


def main():
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    wd, run, out_file = sys.argv[1:4]
    j = lambda *p: os.path.join(wd, *p)
    load = lambda p, default: json.load(open(p, encoding="utf-8")) if os.path.exists(p) else default

    def dump(p, obj):
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            json.dump(obj, f, ensure_ascii=False, indent=1)

    raw = json.load(open(out_file, encoding="utf-8"))
    r = raw.get("result", raw) if isinstance(raw, dict) else raw
    if isinstance(r, str):
        r = json.loads(r)
    os.makedirs(j("runs"), exist_ok=True)
    dump(j("runs", f"{run}.json"), r)
    repo = load(j("general.json"), {})["codebase"]["repo"]

    allowed = [os.path.abspath(os.path.join(repo, "docs", "specs")), os.path.abspath(j("evidence"))]
    for u in r.get("unwritten") or []:
        p = os.path.abspath(u["path"])
        if any(os.path.commonpath([a, p]) == a for a in allowed):
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, "w", encoding="utf-8", newline="\n") as f:
                f.write(u["content"])
            print(f"saved a refused write: {p}")
        else:
            print(f"NOT saved (outside docs/specs and the evidence folder): {p}")

    specs = sorted({s for g in r.get("groups") or [] for s in g.get("specs") or []})
    written = set(load(j("runs", "written.json"), []))
    written |= {s for s in specs if os.path.exists(os.path.join(repo, "docs", "specs", s + ".md"))}
    dump(j("runs", "written.json"), sorted(written))

    triage = load(j("shape", "triage.json"), [])
    have = {(t["id"], t.get("affected_spec")) for t in triage}
    ids = {t["id"] for t in triage}
    raw_items = [{"spec": p["spec"], "text": t} for p in r.get("per_spec") or [] for t in p.get("new_security") or []]
    for g in r.get("groups") or []:
        rv = g.get("review") or {}
        raw_items += [{"spec": (g.get("specs") or [""])[0], "text": t} for t in rv.get("new_security") or []]
    for s in specs:
        ev = j("evidence", s + ".md")
        if os.path.exists(ev):
            body = open(ev, encoding="utf-8-sig").read()
            sec = body.split("## Security", 1)[1].split("\n## ", 1)[0] if "## Security" in body else ""
            for line in sec.splitlines():
                m = re.match(r"\s*-?\s*NEW:?\s*(.+)$", line)
                if m:
                    raw_items.append({"spec": s, "text": m.group(1).strip()})
    seen, added = set(), []
    for n in raw_items:
        m = re.match(r"([A-Za-z0-9][A-Za-z0-9_.-]{2,60}):\s*(.+)$", n["text"])
        nid, rest = (m.group(1), m.group(2)) if m else (None, n["text"])
        if nid and (nid, n["spec"]) in have:
            continue
        if nid and nid in ids:
            nid = f"{nid}-{n['spec'].split('/')[-1]}"
        nid = nid or f"new-{run}-{len(added) + 1}"
        key = (n["spec"], rest[:100])
        if key in seen:
            continue
        seen.add(key)
        item = {"id": nid, "title": rest[:100], "where": (re.findall(r"[\w./-]+\.\w+:\d[\d,-]*", rest) or [""])[0],
                "what_happens": rest, "class": "keep-out",
                "spec_wording": "Until ruled: nothing about this; state only what is true for every caller.",
                "affected_spec": n["spec"], "why": f"NEW in run {run}; keep-out until ruled under the approved policy.",
                "added_in": run, "ruled": False}
        triage.append(item)
        have.add((nid, n["spec"]))
        ids.add(nid)
        added.append(item)
    dump(j("shape", "triage.json"), triage)
    dump(j("runs", f"{run}-new-security.json"), added)

    s = r.get("summary") or {}
    print(f"run {run}: {s.get('written')} of {s.get('specs')} specs written, about {s.get('requirements_after_review')} requirements; "
          f"NEW security items {len(added)} (keep-out until ruled)")
    for k in ("failed", "groups_not_reviewed", "lint_not_clean"):
        if s.get(k):
            print(f"{k}: {', '.join(s[k])}")
    left = [x for g in r.get("groups") or [] for x in ((g.get("review") or {}).get("left_for_others") or [])]
    if left:
        print("left for other groups (fix by hand or in the next group's run):")
        for x in left:
            print("  " + x)
    status = subprocess.run(["git", "-C", repo, "status", "--porcelain", "--", "docs/specs"], capture_output=True, text=True).stdout
    print("files to commit (docs/specs only):")
    for line in status.splitlines():
        print("  " + line)


if __name__ == "__main__":
    main()

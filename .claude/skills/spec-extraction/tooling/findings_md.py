"""Write the run's findings as two private Markdown files, most serious first: Bugs.md and Security.md.

usage: python -I findings_md.py <wd>

Writes to general.json's "findingsDir", which must be outside the repo: the detail is private and never goes into a
commit, a pull request, an issue or a spec. Sources: <wd>/shape/triage.json (the security triage, with the owner's
rulings), the shape's dead code, the evidence notes' ## Findings lines (bugs) and ## Security lines marked NEW.
Rebuilt from the sources on every run.
"""
import json, os, re, sys

LEVEL = {"high": 3, "medium": 2, "low": 1}


def evidence_items(wd):
    bugs, sec = [], []
    root = os.path.join(wd, "evidence")
    for dirpath, _, files in os.walk(root):
        for name in sorted(files):
            if not name.endswith(".md"):
                continue
            spec = os.path.relpath(os.path.join(dirpath, name), root).replace("\\", "/")[:-3]
            body = open(os.path.join(dirpath, name), encoding="utf-8-sig").read().replace("\r\n", "\n")
            for part in re.split(r"\n## ", "\n" + body):
                head, _, text = part.partition("\n")
                if head.startswith("Findings"):
                    for line in text.splitlines():
                        m = re.match(r"\s*-\s*(?:(high|medium|low)\s*:\s*)?(.+)$", line, re.I)
                        if m and m.group(2).strip():
                            bugs.append({"spec": spec, "level": (m.group(1) or "medium").lower(), "text": m.group(2).strip()})
                elif head.startswith("Security"):
                    for line in text.splitlines():
                        m = re.match(r"\s*-?\s*NEW:?\s*(.+)$", line)
                        if m:
                            sec.append({"spec": spec, "text": m.group(1).strip()})
    return bugs, sec


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    wd = sys.argv[1]
    g = json.load(open(os.path.join(wd, "general.json"), encoding="utf-8"))
    repo, out = os.path.abspath(g["codebase"]["repo"]), g.get("findingsDir")
    if not out or os.path.commonpath([repo, os.path.abspath(out)]) == repo:
        sys.exit("general.json's findingsDir must be set to a folder outside the repo")
    os.makedirs(out, exist_ok=True)
    triage = json.load(open(os.path.join(wd, "shape", "triage.json"), encoding="utf-8"))
    dead = json.load(open(os.path.join(wd, "shape", "shape.json"), encoding="utf-8"))["final"].get("dead_code") or []
    ev_bugs, ev_sec = evidence_items(wd)
    base = g.get("baseline", "")

    rank = {"keep-out": 0, "state-generically": 1, "state-as-is": 2}
    sec_items = sorted([t for t in triage if t["class"] != "state-as-is"], key=lambda t: (rank[t["class"]], t["id"]))
    S = ["# PIU Scores: security findings from the spec extraction (private)", "",
         f"Baseline {base}. Found by reading the code, not by testing a running site; nothing here has been re-read by "
         "a person unless it says so. Never commit, paste or post this file; the public repo's SECURITY.md is how a "
         "report is handled.", ""]
    for cls in ("keep-out", "state-generically"):
        items = [t for t in sec_items if t["class"] == cls]
        if items:
            S += [f"## {cls} ({len(items)})", ""]
            for t in items:
                S += [f"### {t['title']}", "", f"- What happens: {t['what_happens']}", f"- Where: {t['where']}",
                      f"- What the spec says: {t['spec_wording']}", f"- Spec: {t.get('affected_spec', '')}",
                      f"- Ruled: {'yes' if t.get('ruled') else 'not yet'}", ""]
    if ev_sec:
        S += [f"## Found while writing, not yet ruled ({len(ev_sec)})", ""] + [f"- {v['text']} (spec: {v['spec']})" for v in ev_sec] + [""]
    with open(os.path.join(out, "Security.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(S) + "\n")

    rows = [(LEVEL["medium"], f"**{t['title']}**: {t['what_happens']} Where: {t['where']}.", t.get("affected_spec", ""))
            for t in triage if t["class"] == "state-as-is"]
    rows += [(LEVEL.get(b["level"], 2), b["text"], b["spec"]) for b in ev_bugs]
    rows += [(LEVEL["low"], f"**Dead code: {d['item']}** ({d['where']}): {d['why']}", "") for d in dead]
    B = ["# PIU Scores: bugs from the spec extraction (private)", "",
         f"Baseline {base}. Behavior that looks wrong, dead code, and docs that describe the code wrongly; most "
         "serious first. Security findings are in Security.md. The specs state what the code does today; each item "
         "ends with the code fixed, the spec changed through a reviewed spec change, or the item rejected.", ""]
    for level, name in ((3, "High"), (2, "Medium"), (1, "Low")):
        items = [x for x in rows if x[0] == level]
        if items:
            B += [f"## {name} ({len(items)})", ""] + [f"- {text}" + (f" (spec: {spec})" if spec else "") for _, text, spec in items] + [""]
    with open(os.path.join(out, "Bugs.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(B) + "\n")
    print(f"{out}: Security.md {len(sec_items)} from the triage + {len(ev_sec)} found while writing; "
          f"Bugs.md {len(rows)} ({len(ev_bugs)} from evidence notes, {len(dead)} dead code)")


if __name__ == "__main__":
    main()

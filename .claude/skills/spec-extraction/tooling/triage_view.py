"""Print the security triage items a writer or reviewer must follow. Writers and reviewers run it for their spec.

usage: python -I triage_view.py <wd> --spec <context>/<feature>
       python -I triage_view.py <wd> --context <context>
       python -I triage_view.py <wd> --ids <id>[,<id>...]
       python -I triage_view.py <wd> --full

Reads <wd>/shape/triage.json. --spec prints the items whose affected_spec names that spec, then the items that name
no spec (check whether they touch yours). Each item prints its class and spec_wording, the most a spec may say; an
item with ruled false is keep-out whatever its class says. The detail (where, what happens) is for writers and
reviewers only and never reaches a spec.
"""
import json, os, sys


def fmt(t):
    cls = t["class"] if t.get("ruled", False) else f"keep-out (not ruled yet; proposed {t['class']})"
    lines = [f"## {t['id']}: {t['title']}", f"- Class: {cls}", f"- The most a spec may say: {t['spec_wording']}",
             f"- Spec: {t.get('affected_spec') or '(none named)'}", f"- Where: {t['where']}",
             f"- What happens (never in a spec): {t['what_happens']}"]
    if t.get("ruling_note"):
        lines.append(f"- Ruling: {t['ruling_note']}")
    if t.get("added_in"):
        lines.append(f"- Added in: {t['added_in']}")
    return "\n".join(lines)


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    wd, opts = sys.argv[1], sys.argv[2:]
    tri = json.load(open(os.path.join(wd, "shape", "triage.json"), encoding="utf-8"))
    named = lambda t: str(t.get("affected_spec") or "")
    arg = lambda flag: opts[opts.index(flag) + 1] if flag in opts else None
    unnamed = []
    if "--full" in opts:
        pick, head = tri, "Every triage item"
    elif arg("--spec"):
        s = arg("--spec").strip("/").removesuffix(".md")
        pick = [t for t in tri if s in named(t)]
        unnamed = [t for t in tri if not named(t).strip()]
        head = f"Triage items for {s}"
    elif arg("--context"):
        c = arg("--context").strip("/") + "/"
        pick, head = [t for t in tri if c in named(t)], f"Triage items for the {c} folder"
    elif arg("--ids"):
        ids = set(arg("--ids").split(","))
        pick, head = [t for t in tri if t["id"] in ids], "Triage items " + arg("--ids")
    else:
        sys.exit(__doc__)
    print(f"# {head}: {len(pick)} (of {len(tri)})\n")
    for t in pick:
        print(fmt(t) + "\n")
    if unnamed:
        print(f"# Items that name no spec ({len(unnamed)}): check whether they touch yours\n")
        for t in unnamed:
            print(fmt(t) + "\n")


if __name__ == "__main__":
    main()

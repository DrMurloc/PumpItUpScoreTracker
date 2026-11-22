"""Tag the tests that prove a requirement, and list the requirements no test names yet.

usage: python -I tags.py plan <wd>
       python -I tags.py apply <wd>
       python -I tags.py gaps <wd>

plan   Reads each evidence note's `- Tests:` lines (`<repo-relative path>::<method>`, rule 20) and resolves every
       one to a test method in a suite CI runs (ScoreTracker.Tests, .Tests.Api, .Tests.Components,
       .Tests.Integration, .Tests.E2E; never ScoreTracker.ExplorationTests). Writes <wd>/tags/plan.json and prints
       what it could not resolve (a missing file, no such method, a name found twice, not a [Fact] or [Theory]).
apply  Adds `[Trait("Verifies", "<ID>")]` to each resolved test, one trait per ID, on its own line directly above
       the method, indented like the method's other attributes. Idempotent; keeps each file's BOM and line endings.
gaps   Reads every `[Trait("Verifies", "...")]` in the CI suites and every requirement in docs/specs. Writes
       <wd>/untagged.md (the requirements no test names, by spec) and <wd>/tags/held.json (the specs whose every
       requirement is named: the coverage check's starting set). Exit 1 when a trait names no live requirement or
       a Verifies trait is not written as a plain string literal.
"""
import json, os, re, sys

SUITES = ("ScoreTracker/ScoreTracker.Tests/", "ScoreTracker/ScoreTracker.Tests.Api/", "ScoreTracker/ScoreTracker.Tests.Components/",
          "ScoreTracker/ScoreTracker.Tests.Integration/", "ScoreTracker/ScoreTracker.Tests.E2E/")
TEST_LINE_RE = re.compile(r"^\s*-\s*Tests:\s*(.+)$", re.I)
SECTION_RE = re.compile(r"^## ([A-Z][A-Z0-9]{1,7}-\d{3})\b")
REF_RE = re.compile(r"`?([\w./-]+\.cs)::([A-Za-z_]\w*)`?")
TRAIT_RE = re.compile(r"\[\s*(?:Xunit\.)?Trait\s*\(\s*\"Verifies\"\s*,\s*\"([^\"]*)\"\s*\)\s*\]")
ANY_VERIFIES_RE = re.compile(r"Trait\s*\(\s*\"[Vv]erifies\"\s*,\s*([^)]*)\)")
REQ_HEAD_RE = re.compile(r"^### Requirement: (\S+) ", re.M)
ATTR_LINE_RE = re.compile(r"^\s*\[")
TEST_ATTR_RE = re.compile(r"\[\s*(?:Xunit\.)?(?:Fact|Theory|SkippableFact|SkippableTheory)\b")


def general(wd):
    return json.load(open(os.path.join(wd, "general.json"), encoding="utf-8"))


def read_bytes(path):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")
    nl = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), bom, nl


def write_bytes(path, text, bom, nl):
    data = text.replace("\n", nl).encode("utf-8")
    with open(path, "wb") as f:
        f.write((b"\xef\xbb\xbf" if bom else b"") + data)


def evidence_refs(wd):
    refs = []
    root = os.path.join(wd, "evidence")
    for d, _, fs in os.walk(root):
        for f in sorted(fs):
            if not f.endswith(".md"):
                continue
            spec = os.path.relpath(os.path.join(d, f), root).replace("\\", "/")[:-3]
            rid = None
            for ln in open(os.path.join(d, f), encoding="utf-8-sig").read().replace("\r\n", "\n").split("\n"):
                m = SECTION_RE.match(ln)
                if m:
                    rid = m.group(1)
                    continue
                if ln.startswith("## "):
                    rid = None
                    continue
                t = TEST_LINE_RE.match(ln)
                if t and rid:
                    for r in REF_RE.finditer(t.group(1)):
                        refs.append({"spec": spec, "id": rid, "file": r.group(1).lstrip("./"), "method": r.group(2)})
    return refs


def find_method(lines, method):
    pat = re.compile(r"^\s*(?:public|internal|private|protected)?[\w\s<>,\[\]?]*\b" + re.escape(method) + r"\s*\(")
    hits = [i for i, ln in enumerate(lines) if pat.match(ln) and not ln.strip().startswith(("//", "*", "["))]
    return hits


def attr_block(lines, i):
    j = i - 1
    while j >= 0 and (ATTR_LINE_RE.match(lines[j]) or lines[j].strip().startswith("//")):
        j -= 1
    return j + 1, i


def plan(wd):
    repo = general(wd)["codebase"]["repo"]
    resolved, problems = [], []
    for r in evidence_refs(wd):
        path = r["file"].replace("\\", "/")
        if not path.startswith(SUITES):
            problems.append(f"{r['spec']} {r['id']}: {path} is not in a suite CI runs")
            continue
        full = os.path.join(repo, path)
        if not os.path.isfile(full):
            problems.append(f"{r['spec']} {r['id']}: no file {path}")
            continue
        lines = read_bytes(full)[0].split("\n")
        hits = find_method(lines, r["method"])
        if len(hits) != 1:
            problems.append(f"{r['spec']} {r['id']}: {path}::{r['method']} found {len(hits)} times")
            continue
        start, end = attr_block(lines, hits[0])
        if not any(TEST_ATTR_RE.search(lines[k]) for k in range(start, end)):
            problems.append(f"{r['spec']} {r['id']}: {path}::{r['method']} is not a [Fact] or [Theory]")
            continue
        resolved.append(dict(r, file=path))
    os.makedirs(os.path.join(wd, "tags"), exist_ok=True)
    with open(os.path.join(wd, "tags", "plan.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"resolved": resolved, "problems": problems}, f, ensure_ascii=False, indent=1)
    print(f"plan: {len(resolved)} tags resolved across {len({(x['file'], x['method']) for x in resolved})} tests; {len(problems)} unresolved")
    for p in problems:
        print("  " + p)


def apply(wd):
    repo = general(wd)["codebase"]["repo"]
    pl = json.load(open(os.path.join(wd, "tags", "plan.json"), encoding="utf-8"))
    by_file = {}
    for r in pl["resolved"]:
        by_file.setdefault(r["file"], []).append(r)
    added = 0
    for path, rs in sorted(by_file.items()):
        full = os.path.join(repo, path)
        text, bom, nl = read_bytes(full)
        lines = text.split("\n")
        for method in sorted({r["method"] for r in rs}):
            hits = find_method(lines, method)
            if len(hits) != 1:
                print(f"  skipped {path}::{method}: found {len(hits)} times now")
                continue
            i = hits[0]
            start, _ = attr_block(lines, i)
            have = {m.group(1) for k in range(start, i) for m in TRAIT_RE.finditer(lines[k])}
            indent = re.match(r"^\s*", lines[start] if start < i else lines[i]).group(0)
            new = [f'{indent}[Trait("Verifies", "{rid}")]' for rid in sorted({r["id"] for r in rs if r["method"] == method}) if rid not in have]
            if new:
                lines[i:i] = new
                added += len(new)
        write_bytes(full, "\n".join(lines), bom, nl)
    print(f"apply: {added} trait(s) added in {len(by_file)} file(s)")


def suite_files(repo):
    root = os.path.join(repo, "ScoreTracker")
    out = []
    for d in sorted(os.listdir(root)) if os.path.isdir(root) else []:
        if not (d == "ScoreTracker.Tests" or d.startswith("ScoreTracker.Tests.")):
            continue
        for dp, dns, fs in os.walk(os.path.join(root, d)):
            dns[:] = [x for x in dns if x not in ("bin", "obj")]
            out += [os.path.join(dp, f) for f in fs if f.endswith(".cs")]
    return out


def gaps(wd):
    g = general(wd)
    repo = g["codebase"]["repo"]
    tagged, bad = {}, []
    for f in suite_files(repo):
        text = open(f, encoding="utf-8-sig", errors="replace").read()
        rel = os.path.relpath(f, repo).replace("\\", "/")
        for m in TRAIT_RE.finditer(text):
            tagged.setdefault(m.group(1), set()).add(rel)
        for m in ANY_VERIFIES_RE.finditer(text):
            if not re.fullmatch(r"\s*\"[A-Z][A-Z0-9]{1,7}-\d{3}\"\s*", m.group(1)) or "\"verifies\"" in m.group(0):
                bad.append(f"{rel}: Trait(\"Verifies\", {m.group(1).strip()}) is not a plain requirement ID literal")
    specs = {}
    root = os.path.join(repo, "docs", "specs")
    for d in sorted(os.listdir(root)) if os.path.isdir(root) else []:
        if os.path.isdir(os.path.join(root, d)):
            for f in sorted(os.listdir(os.path.join(root, d))):
                if f.endswith(".md"):
                    text = open(os.path.join(root, d, f), encoding="utf-8-sig").read().replace("\r\n", "\n")
                    specs[f"{d}/{f}"] = REQ_HEAD_RE.findall(text)
    live = {i for ids in specs.values() for i in ids}
    for rid in sorted(tagged):
        if rid not in live:
            bad.append(f"Trait(\"Verifies\", \"{rid}\") names no live requirement ({', '.join(sorted(tagged[rid]))})")
    held = sorted(s for s, ids in specs.items() if ids and all(i in tagged for i in ids))
    total = sum(len(v) for v in specs.values())
    untagged = {s: [i for i in ids if i not in tagged] for s, ids in specs.items()}
    n_untagged = sum(len(v) for v in untagged.values())
    L = ["# Requirements no test names yet", "",
         f"Private working list, outside the repo's history: {n_untagged} of {total} requirements in {len(specs)} specs. "
         "It is not a gap list to shrink in the repo; a spec joins the coverage check's held set once every requirement "
         "in it is named.", ""]
    for s, ids in untagged.items():
        if ids:
            L += [f"## {s} ({len(ids)} of {len(specs[s])})", "", ", ".join(ids), ""]
    with open(os.path.join(wd, "untagged.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L) + "\n")
    os.makedirs(os.path.join(wd, "tags"), exist_ok=True)
    with open(os.path.join(wd, "tags", "held.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(held, f, ensure_ascii=False, indent=1)
    print(f"gaps: {total - n_untagged} of {total} requirements named by a test; {len(held)} of {len(specs)} specs fully named "
          f"(tags/held.json); untagged list: {os.path.join(wd, 'untagged.md')}")
    for b in bad:
        print("ERROR " + b)
    sys.exit(1 if bad else 0)


def main():
    a = sys.argv[1:]
    if len(a) != 2 or a[0] not in ("plan", "apply", "gaps"):
        sys.exit(__doc__)
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    {"plan": plan, "apply": apply, "gaps": gaps}[a[0]](a[1])


if __name__ == "__main__":
    main()

"""Every citation of a design doc, the map from design-doc rows to requirements, and the check that none is left.

usage: python -I design_cites.py inventory <repo> <wd>
       python -I design_cites.py map <wd>
       python -I design_cites.py merge <wd> <designmap workflow output file>
       python -I design_cites.py check <repo> [<wd>]

inventory  Lists every citation of a design doc in the repo's tracked text files (docs/design/ itself and this
           skill's folder left out) into <wd>/design/inventory.json, in four forms:
             path   `docs/design/<doc>.md`, with the row tokens on the line (D12, D39-D43, section signs, M5, L3, CH7)
             link   a relative markdown link into design/ from a file under docs/
             name   a design doc's bare file name (`rise.md`), or a hyphenated stem followed by a row token
             token  a bare row token on a line with no citation, in a file that cites a design doc elsewhere
           ADR citations (`ADR-001 D2`) are never counted. Also records the design docs' file names, so `check`
           still knows them after docs/design/ is deleted.
map        Reads every evidence note's `## Design rows` lines and <wd>/design/map-extra.json into
           <wd>/design/map.json ({doc: {token: {"ids": [...], "spec": "...", "none": "..."}}}, token "*" = the whole
           doc), drops IDs that are no requirement heading in docs/specs, and writes every cited (doc, token) the map
           does not hold to <wd>/design/gaps.json.
merge      Adds the designmap workflow's entries to <wd>/design/map-extra.json. Run `map` again after it.
check      Exit 1 while any path, link or name citation is left (token-only lines are reported, never fail).
"""
import json, os, re, subprocess, sys

TEXT_EXT = {".cs", ".razor", ".cshtml", ".css", ".js", ".ts", ".json", ".md", ".yml", ".yaml", ".csproj", ".props",
            ".targets", ".sql", ".html", ".ps1", ".sh", ".xml", ".resx", ".txt", ".config", ".http", ".sln", ".editorconfig"}
SKIP_PREFIXES = ("docs/design/", ".claude/skills/spec-extraction/")
PATH_RE = re.compile(r"docs/design/[\w./-]*[\w-]")
LINK_RE = re.compile(r"\]\(((?:\.\./)*)design/([^)\s#]*)(#[^)\s]*)?\)")
TOKEN_RE = re.compile(r"(?<![\w-])(\u00a7\s?\d+(?:\.\d+)*[a-z]?|(?:D|M|L|CH|Q|S)\d{1,3}[a-z]?(?:\s*[\u2013\u2014-]\s*D?\d{1,3})?)(?![\w])")
ADR_RE = re.compile(r"ADR-\d+[\s:]*(?:(?:D|Q)\d+[/,\s]*)+")
ANY_ID_RE = re.compile(r"(?<![\w-])[A-Z][A-Z0-9]{1,7}-\d{3}(?![\w-])")
REQ_HEAD_RE = re.compile(r"^### Requirement: (\S+) ", re.M)
ROW_LINE_RE = re.compile(r"^\s*-\s*(docs/design/\S+?)\s+(.+?):\s*(.+?)\s*$")


def read(path):
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def tracked(repo):
    out = subprocess.run(["git", "-C", repo, "ls-files"], capture_output=True, text=True, check=True).stdout
    return [p for p in out.splitlines() if p.strip()]


def is_text(p):
    base = os.path.basename(p)
    return os.path.splitext(p)[1].lower() in TEXT_EXT or base in {"CLAUDE.md", "README.md", ".gitattributes"}


def expand(token):
    t = token.replace(" ", "")
    m = re.fullmatch(r"D(\d+)[\u2013\u2014-]D?(\d+)", t)
    if m:
        a, b = int(m.group(1)), int(m.group(2))
        if 0 < b - a <= 30:
            return [f"D{i}" for i in range(a, b + 1)]
    return [t]


def token_spans(text):
    """(position, token) for every row token on a line, ADR citations left out."""
    adr = [(m.start(), m.end()) for m in ADR_RE.finditer(text)]
    out = []
    for m in TOKEN_RE.finditer(text):
        if any(a <= m.start() < b for a, b in adr):
            continue
        out += [(m.start(), t) for t in expand(m.group(1))]
    return out


def line_tokens(text):
    return [t for _, t in token_spans(text)]


def normalize_token(t):
    t = t.strip().strip("`*").strip()
    if t.lower().replace(" ", "") in ("(wholedoc)", "wholedoc", "(whole)", "*", "(doc)", ""):
        return "*"
    return t.replace(" ", "")


def inventory(repo, wd):
    files = tracked(repo)
    design_docs = sorted(p for p in files if p.startswith("docs/design/"))
    stems = {}
    for p in design_docs:
        b = os.path.basename(p)
        if b.lower() != "readme.md" and b.endswith(".md"):
            stems.setdefault(b[:-3], []).append(p)
    unique = {s: v[0] for s, v in stems.items() if len(v) == 1}
    name_re = re.compile(r"(?<![\w/.-])(" + "|".join(sorted(map(re.escape, unique), key=len, reverse=True)) + r")(\.md\b|(?=\s+(?:\u00a7|D\d)))") if unique else None
    items, scanned = [], 0
    for p in files:
        if p.startswith(SKIP_PREFIXES) or not is_text(p):
            continue
        full = os.path.join(repo, p)
        if not os.path.isfile(full):
            continue
        scanned += 1
        lines = read(full).replace("\r\n", "\n").split("\n")
        found = []
        for no, ln in enumerate(lines, 1):
            hits = []
            for m in PATH_RE.finditer(ln):
                hits.append((m.start(), "path", m.group(0).rstrip(".")))
            if p.startswith("docs/") and p.endswith(".md"):
                for m in LINK_RE.finditer(ln):
                    hits.append((m.start(), "link", ("docs/design/" + m.group(2)).rstrip("/")))
            if name_re:
                for m in name_re.finditer(ln):
                    if ln[max(0, m.start() - 7):m.start()].endswith("design/"):
                        continue
                    hits.append((m.start(), "name", unique[m.group(1)]))
            if hits:
                hits.sort()
                # A name inside a link or path to the same doc on the same line is the same citation.
                strong = {(d) for _, f, d in hits if f != "name"}
                hits = [h for h in hits if h[1] != "name" or h[2] not in strong]
                # Each token belongs to the nearest citation before it (tokens before the first go to the first).
                owned = {i: [] for i in range(len(hits))}
                for pos, tok in token_spans(ln):
                    k = max([i for i, h in enumerate(hits) if h[0] <= pos] or [0])
                    owned[k].append(tok)
                for i, (pos, form, doc) in enumerate(hits):
                    found.append({"path": p, "line": no, "form": form, "doc": doc,
                                  "tokens": owned[i], "text": ln.strip()[:240]})
        cited = [f for f in found]
        if cited:
            docs_here = {}
            for f in cited:
                docs_here[f["doc"]] = docs_here.get(f["doc"], 0) + 1
            main_doc = max(docs_here, key=docs_here.get)
            lines_with = {f["line"] for f in cited}
            for no, ln in enumerate(lines, 1):
                if no in lines_with:
                    continue
                toks = line_tokens(ln)
                if toks and re.search(r"(//|/\*|\*|<!--|@\*|#|\(|\[)", ln):
                    found.append({"path": p, "line": no, "form": "token", "doc": main_doc, "tokens": toks,
                                  "text": ln.strip()[:240], "guess": True})
        items += found
    os.makedirs(os.path.join(wd, "design"), exist_ok=True)
    with open(os.path.join(wd, "design", "inventory.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"design_docs": design_docs, "stems": unique, "items": items}, f, ensure_ascii=False, indent=1)
    by_form = {}
    for it in items:
        by_form[it["form"]] = by_form.get(it["form"], 0) + 1
    print(f"scanned {scanned} tracked text files: {len(items)} citations in {len({i['path'] for i in items})} files {by_form}; "
          f"{len({i['doc'] for i in items})} docs cited of {len(design_docs)} under docs/design/")


def live_ids(repo):
    ids = set()
    root = os.path.join(repo, "docs", "specs")
    for d, _, fs in os.walk(root):
        for f in fs:
            if f.endswith(".md") and f != "README.md":
                ids |= set(REQ_HEAD_RE.findall(read(os.path.join(d, f)).replace("\r\n", "\n")))
    return ids


def build_map(wd):
    g = json.load(open(os.path.join(wd, "general.json"), encoding="utf-8"))
    repo = g["codebase"]["repo"]
    ids = live_ids(repo)
    m, dropped, rows = {}, [], 0

    def put(doc, token, new_ids, spec, none, src):
        nonlocal rows
        rows += 1
        e = m.setdefault(doc, {}).setdefault(normalize_token(token), {"ids": [], "spec": "", "none": ""})
        for i in new_ids:
            if i not in ids:
                dropped.append(f"{src}: {doc} {token} -> {i} (no such requirement)")
            elif i not in e["ids"]:
                e["ids"].append(i)
        if spec and not e["spec"]:
            e["spec"] = spec
        if none and not e["ids"] and not e["none"]:
            e["none"] = none

    ev_root = os.path.join(wd, "evidence")
    for d, _, fs in os.walk(ev_root):
        for f in sorted(fs):
            if not f.endswith(".md"):
                continue
            rel = os.path.relpath(os.path.join(d, f), ev_root).replace("\\", "/")
            body = read(os.path.join(d, f)).replace("\r\n", "\n")
            if "## Design rows" not in body:
                continue
            sec = body.split("## Design rows", 1)[1].split("\n## ", 1)[0]
            for ln in sec.split("\n"):
                r = ROW_LINE_RE.match(ln)
                if not r:
                    continue
                doc, toks, value = r.group(1).rstrip(".:"), r.group(2), r.group(3)
                none = value[4:].strip(" -:") if value.lower().startswith("none") else ""
                spec = (re.findall(r"docs/specs/[\w/-]+\.md", value) or [""])[0]
                found = [] if none else ANY_ID_RE.findall(value)
                for t in re.split(r"\s*,\s*", toks):
                    for tt in (expand(t) if t.strip() else []):
                        put(doc, tt, found, spec, none, rel)
    extra = os.path.join(wd, "design", "map-extra.json")
    if os.path.exists(extra):
        for e in json.load(open(extra, encoding="utf-8")):
            put(e["doc"], e["token"], e.get("ids") or [], e.get("spec") or "", e.get("none") or "", "map-extra")
    with open(os.path.join(wd, "design", "map.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(m, f, ensure_ascii=False, indent=1)

    inv = json.load(open(os.path.join(wd, "design", "inventory.json"), encoding="utf-8"))
    gaps = {}
    for it in inv["items"]:
        toks = it["tokens"] or (["*"] if it["form"] != "token" else [])
        for t in toks:
            e = m.get(it["doc"], {}).get(normalize_token(t))
            whole = m.get(it["doc"], {}).get("*")
            if e and (e["ids"] or e["spec"] or e["none"]):
                continue
            if t == "*" and whole:
                continue
            gd = gaps.setdefault(it["doc"], {"tokens": [], "samples": []})
            if t not in gd["tokens"]:
                gd["tokens"].append(t)
            if len(gd["samples"]) < 6:
                gd["samples"].append(f"{it['path']}:{it['line']}: {it['text'][:160]}")
    with open(os.path.join(wd, "design", "gaps.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(gaps, f, ensure_ascii=False, indent=1)
    entries = sum(len(v) for v in m.values())
    print(f"map: {rows} rows read, {entries} (doc, row) entries for {len(m)} docs; {len(dropped)} IDs dropped; "
          f"gaps: {sum(len(v['tokens']) for v in gaps.values())} cited rows in {len(gaps)} docs have no mapping")
    for d in dropped[:30]:
        print("  dropped " + d)


def merge(wd, out_file):
    raw = json.load(open(out_file, encoding="utf-8"))
    r = raw.get("result", raw) if isinstance(raw, dict) else raw
    if isinstance(r, str):
        r = json.loads(r)
    extra_path = os.path.join(wd, "design", "map-extra.json")
    extra = json.load(open(extra_path, encoding="utf-8")) if os.path.exists(extra_path) else []
    n = 0
    for d in r.get("docs") or []:
        for e in d.get("entries") or []:
            extra.append({"doc": d["doc"], "token": e["token"], "ids": e.get("ids") or [], "spec": e.get("spec") or "",
                          "none": e.get("none") or ""})
            n += 1
    with open(extra_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(extra, f, ensure_ascii=False, indent=1)
    print(f"merged {n} entries into {extra_path}; failed docs: {r.get('failed') or []}")


def check(repo, wd=None):
    stems = {}
    if wd and os.path.exists(os.path.join(wd, "design", "inventory.json")):
        stems = json.load(open(os.path.join(wd, "design", "inventory.json"), encoding="utf-8")).get("stems") or {}
    name_re = re.compile(r"(?<![\w/.-])(" + "|".join(sorted(map(re.escape, stems), key=len, reverse=True)) + r")\.md\b") if stems else None
    left, scanned = [], 0
    for p in tracked(repo):
        if p.startswith(SKIP_PREFIXES) or not is_text(p) or not os.path.isfile(os.path.join(repo, p)):
            continue
        scanned += 1
        for no, ln in enumerate(read(os.path.join(repo, p)).replace("\r\n", "\n").split("\n"), 1):
            if PATH_RE.search(ln):
                left.append(f"{p}:{no}: path: {PATH_RE.search(ln).group(0)}")
            if p.startswith("docs/") and LINK_RE.search(ln):
                left.append(f"{p}:{no}: link: {LINK_RE.search(ln).group(0)}")
            if name_re and name_re.search(ln):
                left.append(f"{p}:{no}: name: {name_re.search(ln).group(0)}")
    print(f"scanned {scanned} tracked text files: {len(left)} design-doc citation(s) left")
    for x in left[:80]:
        print("  " + x)
    if len(left) > 80:
        print(f"  ... and {len(left) - 80} more")
    sys.exit(1 if left else 0)


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    a = sys.argv[1:]
    if not a:
        sys.exit(__doc__)
    if a[0] == "inventory" and len(a) == 3:
        inventory(a[1], a[2])
    elif a[0] == "map" and len(a) == 2:
        build_map(a[1])
    elif a[0] == "merge" and len(a) == 3:
        merge(a[1], a[2])
    elif a[0] == "check" and len(a) in (2, 3):
        check(a[1], a[2] if len(a) == 3 else None)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()

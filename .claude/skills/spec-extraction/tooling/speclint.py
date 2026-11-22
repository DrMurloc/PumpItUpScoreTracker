"""Lint PIU Scores' specs against docs/SPECS.md, plus the public-repo safety rules.

usage: python -I speclint.py <repo-root> [spec-file ...] [--no-index]

With no files, lints every docs/specs/<bounded-context>/<feature>.md, the folder's layout and the index
(docs/specs/README.md). With files, lints those (IDs and prefixes are still checked against the whole tree).
Exit 1 on any ERROR. WARN lines are advisory: sizes, wording smells, a bare "#123".

The ERROR rules are exactly docs/SPECS.md's, and the C# spec checks (ScoreTracker.Tests/ArchitectureTests/Specs/)
hold the same ones; keep the two in step. Standard library only. Reads files BOM- and CRLF-safe.

- Front matter: `prefix` and optionally `claims` (routes, api, jobs, discord, events), hand-parsed: `key: value`,
  `kind: [a, b]` or a block list of `- item` lines. Any other key is an ERROR (no `paths` yet; no Status,
  visibility or owner keys). Each surface is claimed by one spec at most.
- One `# ` title, then `## Purpose`, then `## Requirements`; `### Requirement: <PREFIX>-<NNN> <name>` headings;
  the statement (the first paragraph) carries MUST / MUST NOT / SHALL / SHALL NOT and never SHOULD or MAY; at
  least one `#### Scenario:`, each with exactly one WHEN and at least one THEN; no Status or Out lines; 30
  requirements at most.
- IDs unique across the tree, each taking its spec's prefix; prefixes unique, two to eight capitals or digits
  starting with a letter, never reserved (ADR, SHA, AES, OAEP, RFC, ISO, UTF, CVE) and never in the Removed
  register; no removed ID used again; an ID-shaped token whose prefix a spec declares must be a live requirement.
- Public-repo safety: no email outside the example domains; no URL with a real host; no hostname; no key, token,
  connection string, Discord webhook URL or 17-20 digit Discord id; no pull request, issue or commit reference.
- Layout: docs/specs holds the README and <context>/<feature>.md files, nothing else; folder and file names are
  lowercase and hyphenated. The index links every spec on disk, with its prefix, links no missing spec, and has a
  `## Removed` register.
"""
import os, re, sys

KINDS = {"routes", "api", "jobs", "discord", "events"}
FM_KEYS = {"prefix", "claims"}
RESERVED = {"ADR", "SHA", "AES", "OAEP", "RFC", "ISO", "UTF", "CVE"}
PREFIX_RE = re.compile(r"^[A-Z][A-Z0-9]{1,7}$")
ID_RE = re.compile(r"^([A-Z][A-Z0-9]{1,7})-(\d{3})$")
ANY_ID_RE = re.compile(r"(?<![\w-])([A-Z][A-Z0-9]{1,7})-(\d{3})(?![\w-])")
SLUG_RE = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")
REQ_RE = re.compile(r"^### Requirement: (\S+) (\S.*)$")
STEP_RE = re.compile(r"^- \*\*(GIVEN|WHEN|THEN|AND|BUT)\*\* \S")
KEYWORD_RE = re.compile(r"\b(MUST|SHALL)\b")
WEAK_RE = re.compile(r"\b(SHOULD|MAY)\b")
STATUS_RE = re.compile(r"^(\*\*)?Status(\*\*)?\s*:|^- \*\*Status\*\*", re.I)
OUT_RE = re.compile(r"^- \*\*Out\*\*:")
REMOVED_ID_RE = re.compile(r"^- ([A-Z][A-Z0-9]{1,7}-\d{3}) ")
RETIRED_PREFIX_RE = re.compile(r"^- ([A-Z][A-Z0-9]{1,7}) \(retired ")
INDEX_LINK_RE = re.compile(r"\]\(([a-z0-9-]+/[a-z0-9-]+\.md)\)")

# Public-repo safety.
SYNTHETIC = ("example.com", "example.org", "example.net", ".example", ".invalid", ".test", ".localhost", "localhost")
EMAIL_RE = re.compile(r"[\w.+-]+@((?:[\w-]+\.)+[A-Za-z][\w-]*)")
URL_RE = re.compile(r"\b(?:https?|wss?|ftp)://([^/\s)`'\",<>]+)", re.I)
HOST_RE = re.compile(r"(?<![\w.@/-])((?:[a-z0-9-]+\.)+(?:com|net|org|io|gg|dev|app|co|kr|jp|us|uk|xyz|cloud|ai|me|tv|info|biz))(?![\w.-])")
SNOWFLAKE_RE = re.compile(r"(?<![\w.,])\d{17,20}(?![\w.,])")
WEBHOOK_RE = re.compile(r"discord(?:app)?\.com/api/webhooks/", re.I)
CONN_RE = re.compile(r"\b(?:Server|Data Source|Initial Catalog|User ID|Uid|Pwd|Password|AccountKey|SharedAccessKey|SharedAccessSignature|Endpoint)\s*=\s*[^\s;`]+\s*;", re.I)
SECRET_PATTERNS = [
    ("an Anthropic key", re.compile(r"\bsk-ant-[A-Za-z0-9_-]{4,}")),
    ("an API key", re.compile(r"\bsk-[A-Za-z0-9]{20,}")),
    ("a private key", re.compile(r"-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----")),
    ("an AWS key", re.compile(r"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")),
    ("a Google key", re.compile(r"\bAIza[0-9A-Za-z_-]{35}")),
    ("a GitHub token", re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{22,})")),
    ("a Slack token", re.compile(r"\bxox[abposr]-[A-Za-z0-9-]{10,}")),
    ("a Discord bot token", re.compile(r"\b[MNO][A-Za-z\d_-]{23,27}\.[A-Za-z\d_-]{6}\.[A-Za-z\d_-]{27,}")),
    ("a JWT", re.compile(r"\beyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}")),
    ("a password or key assignment", re.compile(r"\b(?:password|passwd|pwd|client_secret|secret_key|api_key|apikey|access_token|auth_token|bot_token)\s*[=:]\s*[^\s;,&\"'<>*`]{4,}", re.I)),
]
REF_PATTERNS = [
    ("a pull request or issue reference", re.compile(r"\b(?:PR|pull request|pull|issue|issues|fix(?:es|ed)?|close[sd]?|resolve[sd]?)\s*#\d+\b", re.I)),
    ("a pull request or issue reference", re.compile(r"\(#\d+\)")),
    ("a GitHub pull request, issue or commit link", re.compile(r"github\.com/[^\s)]+/(?:pull|issues|commit)/", re.I)),
    ("a commit reference", re.compile(r"\bcommit\s+[0-9a-f]{7,40}\b", re.I)),
]
BARE_REF_RE = re.compile(r"(?<![\w&#{])#\d+\b")
HIDDEN_TRIGGER_RE = re.compile(r"\b(when|after|once|then|later|clicks?|presses|opens|signs in again|returns)\b", re.I)
CHANGELOG_RE = re.compile(r"\b(no longer|we changed|used to|is now|are now)\b", re.I)


class Report:
    def __init__(self):
        self.errors = self.warns = 0

    def err(self, where, msg):
        self.errors += 1
        print(f"ERROR {where}: {msg}")

    def warn(self, where, msg):
        self.warns += 1
        print(f"WARN  {where}: {msg}")


def read_text(path):
    """BOM- and CRLF-safe read: the result has LF line endings only."""
    with open(path, encoding="utf-8-sig") as f:
        return f.read().replace("\r\n", "\n").replace("\r", "\n")


def strip_fences(lines):
    out, fence = [], False
    for ln in lines:
        if ln.strip().startswith("```"):
            fence = not fence
            out.append("")
            continue
        out.append("" if fence else ln)
    return out


def unquote(v):
    v = v.strip()
    if len(v) >= 2 and v[0] == v[-1] and v[0] in "\"'":
        return v[1:-1]
    return v


def parse_list(value):
    value = value.strip()
    if not (value.startswith("[") and value.endswith("]")):
        return None
    inner = value[1:-1].strip()
    return [unquote(x) for x in inner.split(",") if x.strip()] if inner else []


def parse_front_matter(fm_lines):
    """Hand parser, stricter than YAML: returns (prefix, claims dict, errors)."""
    errors, prefix, claims, seen = [], None, None, set()
    i = 0
    while i < len(fm_lines):
        ln = fm_lines[i]
        if not ln.strip():
            i += 1
            continue
        if ln.lstrip().startswith("#"):
            errors.append("front matter holds a comment line")
            i += 1
            continue
        m = re.match(r"^([A-Za-z_][\w-]*):(.*)$", ln)
        if not m or ln[0] in " \t":
            errors.append(f"front matter line is not 'key: value': {ln.strip()[:60]}")
            i += 1
            continue
        key, rest = m.group(1), m.group(2).strip()
        if key in seen:
            errors.append(f"front matter key '{key}' appears twice")
        seen.add(key)
        if key not in FM_KEYS:
            errors.append(f"front matter key '{key}' is not allowed (only prefix and claims; no paths yet)")
            i += 1
            while i < len(fm_lines) and fm_lines[i][:1] in (" ", "\t"):
                i += 1
            continue
        if key == "prefix":
            prefix = unquote(rest)
            i += 1
            continue
        # claims
        if rest:
            errors.append("claims must be a map of kinds on the lines below it")
            i += 1
            continue
        claims = {}
        i += 1
        while i < len(fm_lines) and (fm_lines[i][:1] in (" ", "\t") or not fm_lines[i].strip()):
            sub = fm_lines[i]
            if not sub.strip():
                i += 1
                continue
            km = re.match(r"^\s+([A-Za-z_][\w-]*):(.*)$", sub)
            if not km:
                errors.append(f"claims line is not '<kind>: [..]': {sub.strip()[:60]}")
                i += 1
                continue
            kind, val = km.group(1), km.group(2).strip()
            if kind in claims:
                errors.append(f"claims kind '{kind}' appears twice")
            i += 1
            if val:
                items = parse_list(val)
                if items is None:
                    errors.append(f"claims.{kind} must be a list: [a, b] or '- item' lines")
                    items = []
            else:
                items = []
                while i < len(fm_lines):
                    im = re.match(r"^\s+- (.+)$", fm_lines[i])
                    if not im:
                        break
                    items.append(unquote(im.group(1)))
                    i += 1
            claims[kind] = items
    return prefix, claims, errors


def removed_register(index_text):
    ids, prefixes = set(), set()
    if "## Removed" not in index_text:
        return ids, prefixes
    for ln in index_text.split("## Removed", 1)[1].split("\n"):
        m = REMOVED_ID_RE.match(ln)
        if m:
            ids.add(m.group(1))
        m = RETIRED_PREFIX_RE.match(ln)
        if m:
            prefixes.add(m.group(1))
    return ids, prefixes


def safety(rel, lines, rep):
    for i, ln in enumerate(lines, 1):
        where = f"{rel}:{i}"
        for m in EMAIL_RE.finditer(ln):
            if not m.group(1).lower().endswith(SYNTHETIC):
                rep.err(where, f"email address outside the example domains: {m.group(0)}")
        for m in URL_RE.finditer(ln):
            host = m.group(1).lower().split(":")[0].split("@")[-1]
            if not host.endswith(SYNTHETIC):
                rep.err(where, f"URL with a real host: {m.group(0)}")
        for m in HOST_RE.finditer(ln):
            if not m.group(1).endswith(SYNTHETIC):
                rep.err(where, f"hostname: {m.group(1)}")
        if WEBHOOK_RE.search(ln):
            rep.err(where, "a Discord webhook URL")
        if SNOWFLAKE_RE.search(ln):
            rep.err(where, f"a 17-20 digit number (a Discord id?): {SNOWFLAKE_RE.search(ln).group(0)}")
        if CONN_RE.search(ln):
            rep.err(where, "looks like a connection string")
        for label, pat in SECRET_PATTERNS:
            if pat.search(ln):
                rep.err(where, f"looks like {label}: {pat.search(ln).group(0)[:16]}...")
        for label, pat in REF_PATTERNS:
            if pat.search(ln):
                rep.err(where, f"{label}: {pat.search(ln).group(0)}")
        if BARE_REF_RE.search(ln) and not any(p.search(ln) for _, p in REF_PATTERNS):
            rep.warn(where, f"'{BARE_REF_RE.search(ln).group(0)}' may be a pull request or issue number")


def lint_file(path, rel, rep, ctx):
    parts = rel.replace("\\", "/").split("/")
    if len(parts) != 4 or parts[:2] != ["docs", "specs"] or not parts[3].endswith(".md"):
        rep.err(rel, "a spec sits at docs/specs/<bounded-context>/<feature>.md")
        return
    if not SLUG_RE.match(parts[2]) or not SLUG_RE.match(parts[3][:-3]):
        rep.err(rel, "folder and file names are lowercase and hyphenated")
    text = read_text(path)
    lines = text.split("\n")
    safety(rel, lines, rep)
    if lines[0] != "---":
        rep.err(rel, "front matter opens with --- on the first line")
        return
    try:
        end = lines.index("---", 1)
    except ValueError:
        rep.err(rel, "front matter never closes")
        return
    prefix, claims, fm_errors = parse_front_matter(lines[1:end])
    for e in fm_errors:
        rep.err(rel, e)
    spec_prefix = None
    if not prefix or not PREFIX_RE.match(prefix):
        rep.err(rel, f"prefix missing or malformed (two to eight capitals or digits, starting with a letter): {prefix!r}")
    elif prefix in RESERVED:
        rep.err(rel, f"prefix {prefix} is reserved (it already means something in this repo's text)")
    elif prefix in ctx["retired_prefixes"]:
        rep.err(rel, f"prefix {prefix} was retired (the index's Removed register); take a new one")
    elif prefix in ctx["prefixes"]:
        rep.err(rel, f"prefix {prefix} already declared by {ctx['prefixes'][prefix]}")
    else:
        ctx["prefixes"][prefix] = rel
        spec_prefix = prefix
    for kind, items in (claims or {}).items():
        if kind not in KINDS:
            rep.err(rel, f"claim kind '{kind}' is not one of {', '.join(sorted(KINDS))}")
        if not items:
            rep.err(rel, f"claims.{kind} is empty")
        for it in items:
            k = (kind, it.strip().lower())
            if k in ctx["claims"]:
                rep.err(rel, f"{kind} '{it}' is already claimed by {ctx['claims'][k]}")
            else:
                ctx["claims"][k] = rel

    body = strip_fences(lines[end + 1:])
    base = end + 2
    titles = [i for i, ln in enumerate(body) if re.match(r"^# \S", ln)]
    if len(titles) != 1:
        rep.err(rel, f"exactly one '# ' title, found {len(titles)}")
    h2 = [(i, ln[3:].strip()) for i, ln in enumerate(body) if ln.startswith("## ")]
    names = [n for _, n in h2]
    if names != ["Purpose", "Requirements"]:
        rep.err(rel, f"the sections are ## Purpose then ## Requirements, found {names}")
    if "Requirements" not in names:
        return
    req_start = [i for i, n in h2 if n == "Requirements"][0]
    if "Purpose" in names:
        p_start = [i for i, n in h2 if n == "Purpose"][0]
        if not "\n".join(body[p_start + 1:req_start]).strip():
            rep.err(rel, "Purpose is empty")
    for i, ln in enumerate(body[:req_start]):
        s = ln.strip()
        if OUT_RE.match(s):
            rep.err(f"{rel}:{base + i}", "no Out lines")
        if STATUS_RE.match(s):
            rep.err(f"{rel}:{base + i}", "no Status lines")
        if ln.startswith("### ") or ln.startswith("#### "):
            rep.err(f"{rel}:{base + i}", "headings below ## sit only under Requirements")

    sec, sbase = body[req_start + 1:], base + req_start + 1
    reqs, cur = [], None
    for i, ln in enumerate(sec):
        if ln.startswith("### "):
            m = REQ_RE.match(ln)
            if not m:
                rep.err(f"{rel}:{sbase + i}", f"a third-level heading is '### Requirement: <ID> <name>': {ln[:70]}")
                cur = None
                continue
            cur = {"id": m.group(1), "name": m.group(2), "line": sbase + i, "lines": []}
            reqs.append(cur)
        elif ln.startswith("#### ") and not ln.startswith("#### Scenario: "):
            rep.err(f"{rel}:{sbase + i}", f"a fourth-level heading is '#### Scenario: <name>': {ln[:70]}")
        elif re.match(r"^#{5,} ", ln):
            rep.err(f"{rel}:{sbase + i}", "no headings below level 4")
        elif cur is not None:
            cur["lines"].append((sbase + i, ln))
        elif ln.strip():
            rep.err(f"{rel}:{sbase + i}", "text under Requirements before the first requirement")
    if not reqs:
        rep.err(rel, "no requirements")
    if len(reqs) > 30:
        rep.err(rel, f"{len(reqs)} requirements; 30 is the ceiling - split the feature")
    elif len(reqs) > 25:
        rep.warn(rel, f"{len(reqs)} requirements (a spec holds about 10 to 25)")

    for r in reqs:
        where, rid = f"{rel}:{r['line']}", r["id"]
        m = ID_RE.match(rid)
        if not m:
            rep.err(where, f"identifier malformed (<PREFIX>-<NNN>): {rid}")
        elif spec_prefix and m.group(1) != spec_prefix:
            rep.err(where, f"{rid} does not take the spec's prefix {spec_prefix}")
        if rid in ctx["removed_ids"]:
            rep.err(where, f"{rid} was removed (the index's Removed register); IDs are never reused")
        if rid in ctx["ids"]:
            rep.err(where, f"{rid} is already used at {ctx['ids'][rid]}")
        ctx["ids"][rid] = where
        ctx["count"] += 1
        pre, scenarios, sc = [], [], None
        for ln_no, ln in r["lines"]:
            if ln.startswith("#### Scenario: "):
                sc = {"name": ln[len("#### Scenario: "):].strip(), "line": ln_no, "lines": []}
                scenarios.append(sc)
            elif sc is None:
                pre.append((ln_no, ln))
            else:
                sc["lines"].append((ln_no, ln))
        paras, para = [], []
        for ln_no, ln in pre:
            if ln.strip():
                para.append((ln_no, ln))
            elif para:
                paras.append(para)
                para = []
        if para:
            paras.append(para)
        for ln_no, ln in pre:
            if STATUS_RE.match(ln.strip()):
                rep.err(f"{rel}:{ln_no}", f"{rid}: no Status lines - approving a spec confirms it")
        if not paras:
            rep.err(where, f"{rid} has no statement")
            continue
        stmt = " ".join(ln.strip() for _, ln in paras[0])
        if stmt.startswith("|") or stmt.startswith("- ") or stmt.startswith("**"):
            rep.err(where, f"{rid}: the first paragraph after the heading is the statement")
        if not KEYWORD_RE.search(stmt):
            rep.err(where, f"{rid}: the statement carries no MUST, MUST NOT, SHALL or SHALL NOT")
        if WEAK_RE.search(stmt):
            rep.err(where, f"{rid}: the statement uses SHOULD or MAY")
        if CHANGELOG_RE.search(stmt):
            rep.warn(where, f"{rid}: the statement reads like a change log ({CHANGELOG_RE.search(stmt).group(0)!r}); a spec says what is true")
        for p in paras[1:]:
            if not p[0][1].strip().startswith("|"):
                rep.warn(f"{rel}:{p[0][0]}", f"{rid}: only the statement and a table come before the scenarios: {p[0][1].strip()[:60]}")
        if not scenarios:
            rep.err(where, f"{rid} has no scenario")
        for sc in scenarios:
            swhere = f"{rel}:{sc['line']}"
            whens = thens = steps = 0
            in_table = False
            for ln_no, ln in sc["lines"]:
                s = ln.strip()
                if not s:
                    continue
                if s.startswith("|"):
                    in_table = True
                    continue
                if in_table:
                    rep.err(f"{rel}:{ln_no}", f"{rid}: nothing follows a scenario's examples table")
                    continue
                sm = STEP_RE.match(s)
                if not sm:
                    rep.err(f"{rel}:{ln_no}", f"{rid}: a scenario line is '- **GIVEN|WHEN|THEN|AND|BUT** ...': {s[:60]}")
                    continue
                kw = sm.group(1)
                steps += 1
                if kw in ("AND", "BUT") and thens and HIDDEN_TRIGGER_RE.search(s[sm.end() - 1:]):
                    rep.warn(f"{rel}:{ln_no}", f"{rid}: a step after THEN may hide a second trigger - one trigger per scenario")
                whens += kw == "WHEN"
                thens += kw == "THEN"
            if whens != 1:
                rep.err(swhere, f"{rid} scenario '{sc['name']}' has {whens} WHEN steps (exactly one)")
            if thens < 1:
                rep.err(swhere, f"{rid} scenario '{sc['name']}' has no THEN")
            if steps > 6:
                rep.warn(swhere, f"{rid} scenario '{sc['name']}' has {steps} steps")
        for ln_no, ln in r["lines"]:
            for m2 in ANY_ID_RE.finditer(ln):
                ctx["mentions"].append((f"{rel}:{ln_no}", m2.group(0), m2.group(1)))


def lint_layout(root, rep):
    spec_root = os.path.join(root, "docs", "specs")
    for d, dirs, fs in os.walk(spec_root):
        reld = os.path.relpath(d, spec_root).replace("\\", "/")
        depth = 0 if reld == "." else reld.count("/") + 1
        if depth >= 2:
            rep.err(f"docs/specs/{reld}", "docs/specs holds context folders one level down, nothing deeper")
        for f in fs:
            relf = f"docs/specs/{f}" if reld == "." else f"docs/specs/{reld}/{f}"
            if depth == 0 and f == "README.md":
                continue
            if depth == 1 and f.endswith(".md"):
                continue
            rep.err(relf, "nothing else goes in docs/specs: the README and <context>/<feature>.md specs only")


def lint_index(root, specs, ctx, rep):
    idx = os.path.join(root, "docs", "specs", "README.md")
    if not os.path.exists(idx):
        rep.err("docs/specs/README.md", "the index is missing")
        return
    text = read_text(idx)
    if "## Removed" not in text:
        rep.err("docs/specs/README.md", "no '## Removed' register")
    linked = set(INDEX_LINK_RE.findall(text.split("## Removed", 1)[0]))
    on_disk = {}
    for f in specs:
        rel = os.path.relpath(os.path.abspath(f), os.path.join(os.path.abspath(root), "docs", "specs")).replace("\\", "/")
        on_disk[rel] = f
    for rel in sorted(on_disk):
        if rel not in linked:
            rep.err("docs/specs/README.md", f"the index does not list {rel}")
    for rel in sorted(linked - set(on_disk)):
        rep.err("docs/specs/README.md", f"the index lists {rel}, which is not on disk")
    for prefix, rel in ctx["prefixes"].items():
        short = rel.replace("\\", "/")[len("docs/specs/"):]
        line = next((ln for ln in text.split("\n") if f"]({short})" in ln), "")
        if line and f"({prefix})" not in line:
            rep.err("docs/specs/README.md", f"the line for {short} does not show its prefix ({prefix})")


def all_spec_files(root):
    spec_root = os.path.join(root, "docs", "specs")
    out = []
    if os.path.isdir(spec_root):
        for c in sorted(os.listdir(spec_root)):
            d = os.path.join(spec_root, c)
            if os.path.isdir(d):
                out += [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith(".md")]
    return out


def main():
    argv = sys.argv[1:]
    no_index = "--no-index" in argv
    pos = [a for a in argv if not a.startswith("--")]
    if not pos:
        sys.exit(__doc__)
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    root, files = pos[0], pos[1:]
    whole_tree = not files
    specs = all_spec_files(root)
    idx = os.path.join(root, "docs", "specs", "README.md")
    removed_ids, retired = removed_register(read_text(idx)) if os.path.exists(idx) else (set(), set())
    rep = Report()
    ctx = {"ids": {}, "prefixes": {}, "claims": {}, "mentions": [], "count": 0, "removed_ids": removed_ids, "retired_prefixes": retired}
    targets = specs if whole_tree else [os.path.abspath(f) for f in files]
    others = [f for f in specs if os.path.abspath(f) not in {os.path.abspath(t) for t in targets}]
    # Other specs first, quietly, so duplicate IDs, prefixes and claims are judged against the whole tree.
    quiet = Report()
    for f in others:
        rel = os.path.relpath(os.path.abspath(f), os.path.abspath(root)).replace("\\", "/")
        saved = sys.stdout
        sys.stdout = open(os.devnull, "w")
        try:
            lint_file(f, rel, quiet, ctx)
        finally:
            sys.stdout.close()
            sys.stdout = saved
    ctx["mentions"], ctx["count"] = [], 0
    for f in targets:
        rel = os.path.relpath(os.path.abspath(f), os.path.abspath(root)).replace("\\", "/")
        lint_file(f, rel, rep, ctx)
    for where, token, prefix in ctx["mentions"]:
        if prefix in ctx["prefixes"] and token not in ctx["ids"]:
            rep.err(where, f"{token} names no live requirement")
    if whole_tree:
        lint_layout(root, rep)
        if not no_index:
            lint_index(root, specs, ctx, rep)
    print(f"{len(targets)} spec(s), {ctx['count']} requirement(s): {rep.errors} error(s), {rep.warns} warning(s)")
    sys.exit(1 if rep.errors else 0)


if __name__ == "__main__":
    main()

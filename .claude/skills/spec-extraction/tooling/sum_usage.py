"""Sum token usage in a Claude Code session's transcripts: the main session file plus every subagent/workflow file.

usage: python -I sum_usage.py <project-dir> <session-uuid>

Dedupes assistant messages by message id within each file (a message can span several lines with the same usage).
Groups: main session, then each workflow run folder, then any other subagent files.
"""
import json, os, sys
from collections import defaultdict

KEYS = ("input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens", "output_tokens")


def file_usage(path):
    seen = {}
    models = defaultdict(int)
    first = last = None
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            try:
                rec = json.loads(line)
            except ValueError:
                continue
            ts = rec.get("timestamp")
            if ts:
                first = first or ts
                last = ts
            msg = rec.get("message") or {}
            usage = msg.get("usage") if isinstance(msg, dict) else None
            if not usage:
                continue
            mid = msg.get("id") or rec.get("requestId") or rec.get("uuid")
            seen[mid] = (usage, msg.get("model") or "?")
    tot = defaultdict(int)
    for usage, model in seen.values():
        for k in KEYS:
            tot[k] += int(usage.get(k) or 0)
        models[model] += 1
    return tot, models, len(seen), first, last


def group_of(rel):
    parts = rel.replace("\\", "/").split("/")
    if len(parts) == 1:
        return "main session"
    if "workflows" in parts:
        i = parts.index("workflows")
        if i + 1 < len(parts) - 1:
            return "workflow " + parts[i + 1]
    return "other subagents"


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    proj, sid = sys.argv[1], sys.argv[2]
    files = [os.path.join(proj, sid + ".jsonl")]
    for dirpath, _, names in os.walk(os.path.join(proj, sid)):
        files += [os.path.join(dirpath, n) for n in names if n.endswith(".jsonl")]
    groups = defaultdict(lambda: {"tot": defaultdict(int), "models": defaultdict(int), "msgs": 0, "files": 0, "first": None, "last": None})
    for f in files:
        tot, models, n, first, last = file_usage(f)
        g = groups[group_of(os.path.relpath(f, proj).split(os.sep, 1)[-1] if f.endswith(sid + ".jsonl") is False else sid + ".jsonl")]
        for k in KEYS:
            g["tot"][k] += tot[k]
        for m, c in models.items():
            g["models"][m] += c
        g["msgs"] += n
        g["files"] += 1
        if first and (not g["first"] or first < g["first"]):
            g["first"] = first
        if last and (not g["last"] or last > g["last"]):
            g["last"] = last
    grand = defaultdict(int)
    fmt = lambda n: f"{n / 1e6:,.2f}M"
    print(f"{'group':38} {'files':>5} {'msgs':>6} {'input':>9} {'cache_wr':>9} {'cache_rd':>10} {'output':>8}  span / models")
    for name in sorted(groups, key=lambda n: (n != "main session", groups[n]["first"] or "")):
        g = groups[name]
        t = g["tot"]
        for k in KEYS:
            grand[k] += t[k]
        print(f"{name[:38]:38} {g['files']:>5} {g['msgs']:>6} {fmt(t['input_tokens']):>9} {fmt(t['cache_creation_input_tokens']):>9} "
              f"{fmt(t['cache_read_input_tokens']):>10} {fmt(t['output_tokens']):>8}  {(g['first'] or '')[:16]} -> {(g['last'] or '')[:16]} "
              f"{dict(g['models'])}")
    print(f"{'TOTAL':38} {'':>5} {'':>6} {fmt(grand['input_tokens']):>9} {fmt(grand['cache_creation_input_tokens']):>9} "
          f"{fmt(grand['cache_read_input_tokens']):>10} {fmt(grand['output_tokens']):>8}")
    print(f"input + cache writes + output (excl. cache reads): {fmt(grand['input_tokens'] + grand['cache_creation_input_tokens'] + grand['output_tokens'])}")


if __name__ == "__main__":
    main()

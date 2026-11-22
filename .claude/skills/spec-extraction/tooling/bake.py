"""Bake a workflow script with its args, ready for the Workflow tool's scriptPath.

usage: python -I bake.py <wd> shape
       python -I bake.py <wd> specs [--specs <context>/<feature>,...] [--folders <context>,...] [--limit N]
                                    [--effort E] [--model M] [--review-effort E] [--review-model M]
       python -I bake.py <wd> designmap [--effort E] [--model M]
       python -I bake.py <wd> repoint [--files-per-batch 40] [--effort E] [--model M]

Writes <wd>/tooling/<script>-baked.js: the script with `const A = args` replaced by the args as ASCII JSON with LF
endings (the Workflow approval refuses CR or hidden characters, and big args are awkward inline). <wd> sits inside
the repo checkout (.claude/spec-extraction/), so the baked path is a valid scriptPath as it is. Refuses while a
template placeholder (`<...>`) is left in general.json or shape-args.json.

shape      shape.js with shape-args.json, general.json's decisions and leads, and the repo's docs/SPECS.md.
specs      extract-lean.js with every spec in shape/shape.json not yet in runs/written.json (or the ones named),
           shared-rule owners first, grouped by folder into review groups of at most six; --limit caps the specs so
           the plan meter can be read between batches.
designmap  designmap.js with design/gaps.json: one agent per design doc that has cited rows no writer mapped.
repoint    repoint.js with design/inventory.json and design/map.json, batched by file. Files the run edits by hand
           (CLAUDE.md, README.md, docs/SPECS.md, docs/specs/README.md, the citation ratchet, .claude/) are left out.
"""
import json, os, re, shutil, sys

HAND_EDITED = ("CLAUDE.md", "README.md", "docs/SPECS.md", "docs/specs/README.md",
               "ScoreTracker/ScoreTracker.Tests/ArchitectureTests/DesignDocCitationTests.cs")


def opt(flag, default=None):
    return sys.argv[sys.argv.index(flag) + 1] if flag in sys.argv else default


def load(p, default=None):
    if os.path.exists(p):
        return json.load(open(p, encoding="utf-8"))
    if default is None:
        sys.exit(f"missing {p}")
    return default


def placeholders(obj):
    allowed = {"<bounded-context>", "<feature>", "<context>", "<ID>"}
    return sorted({p for p in re.findall(r"<[A-Za-z][^<>]{0,80}>", json.dumps(obj, ensure_ascii=False)) if p not in allowed})


def subst_wd(obj, wd):
    return json.loads(json.dumps(obj).replace("<wd>", wd.replace("\\", "/")))


def bake(wd, script, args):
    src = open(os.path.join(wd, "tooling", script + ".js"), encoding="utf-8").read().replace("\r\n", "\n")
    if "const A = args" not in src:
        sys.exit(f"{script}.js has no 'const A = args' line")
    baked = src.replace("const A = args", "const A = " + json.dumps(args, ensure_ascii=True), 1)
    if any(ord(ch) > 127 for ch in baked):
        sys.exit("non-ASCII characters in the baked script")
    out = os.path.join(wd, "tooling", script + "-baked.js")
    with open(out, "w", encoding="ascii", newline="\n") as f:
        f.write(baked)
    with open(os.path.join(wd, "tooling", f"args-{script}.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(args, f, ensure_ascii=False, indent=1)
    return out, len(baked)


def common(wd, g):
    py = "py -3" if os.name == "nt" and shutil.which("py") else ("python" if shutil.which("python") else "python3")
    return {"repo": g["codebase"]["repo"], "workdir": wd.replace("\\", "/"), "python": py}


def bake_shape(wd, g):
    sa = load(os.path.join(wd, "shape-args.json"))
    sa.pop("_comment", None)
    sa.pop("decisions", None)
    args = dict(common(wd, g), **sa)
    args["codebase"] = dict(sa["codebase"])
    args["decisions"] = g["decisions"]
    args["leads"] = sa.get("leads") or g["leads"]
    args["practice"] = args["repo"] + "/docs/SPECS.md"
    args["rules"] = args["workdir"] + "/tooling/rules.md"
    args = subst_wd(args, wd)
    left = placeholders(args)
    if left:
        sys.exit("unfilled placeholders (general.json / shape-args.json): " + ", ".join(left))
    out, size = bake(wd, "shape", args)
    print(f"baked {out}: {len(args['groups'])} mapper groups, {size:,} bytes")


def bake_specs(wd, g):
    final = load(os.path.join(wd, "shape", "shape.json"))["final"]
    questions = load(os.path.join(wd, "shape", "questions.json"), [])
    done = set(load(os.path.join(wd, "runs", "written.json"), []))
    order = final.get("order") or []
    contexts = sorted(final["contexts"], key=lambda c: order.index(c["folder"]) if c["folder"] in order else len(order))
    keep = ["title", "prefix", "purpose", "audiences", "routes", "api", "jobs", "discord", "events", "flags", "cookies",
            "design_docs", "inbound_mappings", "owns_shared_rules", "cites", "seams", "asking_words", "est_requirements",
            "paths", "notes"]
    want = set(opt("--specs").split(",")) if opt("--specs") else None
    folders = set(opt("--folders").split(",")) if opt("--folders") else None
    feats = []
    for c in contexts:
        for f in c["features"]:
            s = f"{c['folder']}/{f['feature']}"
            if s in done or (want and s not in want) or (folders and c["folder"] not in folders):
                continue
            feats.append(dict({k: f[k] for k in keep if f.get(k) not in (None, [], "")}, context=c["folder"], feature=f["feature"]))
    feats.sort(key=lambda f: 0 if f.get("owns_shared_rules") else 1)
    if opt("--limit"):
        feats = feats[:int(opt("--limit"))]
    chunks = []
    for c in contexts:
        mine = [f for f in feats if f["context"] == c["folder"]]
        for i in range(0, len(mine), 6):
            chunks.append({"name": c["folder"] + (f"-{i // 6 + 1}" if len(mine) > 6 else ""), "features": mine[i:i + 6]})
    tree = [f"{c['folder']}/{f['feature']}.md ({f['prefix']}): {f['title']}" +
            (f" - owns: {'; '.join(f['owns_shared_rules'])}" if f.get("owns_shared_rules") else "")
            for c in contexts for f in c["features"]]
    names = {f"{f['context']}/{f['feature']}" for f in feats}
    args = dict(common(wd, g), codebase=g["codebase"], baseline=g.get("baseline", ""), decisions=g["decisions"],
                leads=g["leads"], styleRefs=g["styleRefs"], rules=wd.replace("\\", "/") + "/tooling/rules.md",
                calls=wd.replace("\\", "/") + "/calls.md", effort=opt("--effort"), model=opt("--model"),
                reviewEffort=opt("--review-effort"), reviewModel=opt("--review-model"), chunks=chunks, tree=tree,
                questions=[q for q in questions if any(n in (q.get("owner_spec") or "") for n in names)])
    args = subst_wd(args, wd)
    left = placeholders({k: args[k] for k in ("decisions", "leads", "styleRefs", "codebase")})
    if left:
        sys.exit("unfilled placeholders (general.json): " + ", ".join(left))
    if not feats:
        sys.exit("nothing to bake: every spec in the shape is already written (runs/written.json)")
    out, size = bake(wd, "extract-lean", args)
    print(f"baked {out}: {len(feats)} specs in {len(chunks)} review groups (writer effort {args['effort'] or 'session'}, "
          f"reviewer effort {args['reviewEffort'] or 'session'}), {len(args['questions'])} questions, {size:,} bytes")


def bake_designmap(wd, g):
    gaps = load(os.path.join(wd, "design", "gaps.json"))
    final = load(os.path.join(wd, "shape", "shape.json"))["final"]
    attached = {}
    for c in final["contexts"]:
        for f in c["features"]:
            for d in f.get("design_docs") or []:
                attached.setdefault(d, []).append(f"docs/specs/{c['folder']}/{f['feature']}.md")
    docs = [{"doc": d, "tokens": v["tokens"], "samples": v["samples"], "specs": attached.get(d, [])} for d, v in sorted(gaps.items())]
    if not docs:
        sys.exit("no gaps: every cited design-doc row is mapped")
    args = dict(common(wd, g), docs=docs, effort=opt("--effort"), model=opt("--model"))
    out, size = bake(wd, "designmap", args)
    print(f"baked {out}: {len(docs)} docs, {sum(len(d['tokens']) for d in docs)} rows to map, {size:,} bytes")


def bake_repoint(wd, g):
    inv = load(os.path.join(wd, "design", "inventory.json"))
    m = load(os.path.join(wd, "design", "map.json"))
    per = int(opt("--files-per-batch", "40"))
    by_file = {}
    for it in inv["items"]:
        if it["path"] in HAND_EDITED or it["path"].startswith(".claude/"):
            continue
        by_file.setdefault(it["path"], []).append(it)

    def resolve(it):
        out = []
        for t in it["tokens"] or ["*"]:
            e = m.get(it["doc"], {}).get(t) or (m.get(it["doc"], {}).get("*") if t == "*" else None)
            if e:
                out.append(f"{t} -> " + (", ".join(e["ids"]) or e["spec"] or ("none: " + e["none"])))
        return "; ".join(out)

    files = sorted(by_file)
    batches = []
    for i in range(0, len(files), per):
        part = files[i:i + per]
        docs = sorted({it["doc"] for f in part for it in by_file[f]})
        map_lines = []
        for d in docs:
            for t, e in sorted(m.get(d, {}).items()):
                map_lines.append(f"{d} {'(whole doc)' if t == '*' else t} -> " + (", ".join(e["ids"]) or e["spec"] or ("none: " + e["none"])))
        batches.append({"name": f"{i // per + 1:02d}", "map": "\n".join(map_lines) or "(no map entries for these docs)",
                        "files": [{"path": f, "items": [f"L{it['line']} [{it['form']}{', doc guessed' if it.get('guess') else ''}] "
                                                        f"{it['text']}" + (f"  =>  {resolve(it)}" if resolve(it) and not it.get('guess') else "")
                                                        for it in by_file[f]]} for f in part]})
    args = dict(common(wd, g), batches=batches, effort=opt("--effort"), model=opt("--model"))
    out, size = bake(wd, "repoint", args)
    print(f"baked {out}: {len(files)} files in {len(batches)} batches, {sum(len(v) for v in by_file.values())} citations, {size:,} bytes; "
          f"left for hand edits: {', '.join(sorted({it['path'] for it in inv['items'] if it['path'] in HAND_EDITED or it['path'].startswith('.claude/')})) or 'none'}")


def main():
    if len(sys.argv) < 3 or sys.argv[2] not in ("shape", "specs", "designmap", "repoint"):
        sys.exit(__doc__)
    wd = os.path.abspath(sys.argv[1])
    g = load(os.path.join(wd, "general.json"))
    {"shape": bake_shape, "specs": bake_specs, "designmap": bake_designmap, "repoint": bake_repoint}[sys.argv[2]](wd, g)


if __name__ == "__main__":
    main()

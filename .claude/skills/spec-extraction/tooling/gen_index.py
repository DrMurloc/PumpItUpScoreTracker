"""Write the spec list into docs/specs/README.md, PIU Scores' spec index (docs/SPECS.md, "Where specs live").

usage: python -I gen_index.py <repo-root> [<general.json>]

One line per spec, grouped by bounded-context folder: `- [<name>](<context>/<feature>.md) (<PREFIX>): <summary>`,
the name and summary split from the spec's `# <name> - <summary>` title. general.json's optional "folderNotes"
({"shell": "...", "api": "...", "jobs": "..."}) adds an italic line under a folder's heading.

The list sits between two marker comments inside `## Specs`. A first run replaces whatever stood between the
section's opening paragraph and `## Removed` (the "No specs yet" lines); later runs rewrite only between the
markers. The title, the opening text and the `## Removed` register are never touched.
"""
import json, os, re, sys

BEGIN = "<!-- BEGIN SPEC LIST: generated from each spec's prefix and title; edit the specs, not these lines. -->"
END = "<!-- END SPEC LIST -->"


def read_text(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read().replace("\r\n", "\n")


def spec_line(ctx, path):
    text = read_text(path)
    m = re.search(r"^prefix:\s*['\"]?([A-Z][A-Z0-9]*)", text, re.M)
    prefix = m.group(1) if m else "?"
    t = re.search(r"^# (.+)$", text.split("\n---\n", 1)[-1], re.M)
    title = t.group(1).strip() if t else os.path.basename(path)[:-3]
    name, _, summary = title.partition(" - ")
    line = f"- [{name.strip()}]({ctx}/{os.path.basename(path)}) ({prefix})"
    return line + (f": {summary.strip()}" if summary else "")


def generated(spec_root, notes):
    out = []
    for ctx in sorted(os.listdir(spec_root)):
        d = os.path.join(spec_root, ctx)
        if not os.path.isdir(d):
            continue
        files = sorted(f for f in os.listdir(d) if f.endswith(".md"))
        if not files:
            continue
        out += [f"### {ctx}", ""]
        if notes.get(ctx):
            out += [f"*{notes[ctx]}*", ""]
        out += [spec_line(ctx, os.path.join(d, f)) for f in files]
        out.append("")
    return "\n".join(out).rstrip() + "\n"


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    repo = sys.argv[1]
    notes = {}
    if len(sys.argv) > 2:
        notes = json.load(open(sys.argv[2], encoding="utf-8")).get("folderNotes") or {}
    spec_root = os.path.join(repo, "docs", "specs")
    readme = os.path.join(spec_root, "README.md")
    if not os.path.exists(readme):
        sys.exit(f"{readme} is missing; the index is written by hand first (docs/SPECS.md)")
    text = read_text(readme)
    block = BEGIN + "\n\n" + generated(spec_root, notes) + "\n" + END
    if BEGIN in text and END in text:
        text = re.sub(re.escape(BEGIN) + r".*?" + re.escape(END), lambda _: block, text, flags=re.S)
    else:
        m = re.search(r"^## Specs\n\n(.*?\n)\n(.*?)(?=^## Removed)", text, re.S | re.M)
        if not m:
            sys.exit("README.md has no '## Specs' section (an opening paragraph, then anything) before '## Removed'")
        text = text[:m.start(2)] + block + "\n\n" + text[m.end(2):]
    with open(readme, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    count = sum(1 for ln in block.split("\n") if ln.startswith("- ["))
    print(f"wrote {readme}: {count} spec(s)")


if __name__ == "__main__":
    main()

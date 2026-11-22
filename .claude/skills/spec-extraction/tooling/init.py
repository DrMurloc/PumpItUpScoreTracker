"""Set up the extraction's working folder.

usage: python -I init.py <repo> <skill> [--findings <folder outside the repo>]

- Creates <repo>/.claude/spec-extraction/ (the working folder, <wd>). `.claude/*` is git-ignored in this repo and
  every SourceTree scan skips `.claude/`, so nothing in <wd> is ever committed or read by a ratchet. Refuses to go
  on if git does not ignore it.
- Copies <skill>/tooling/* to <wd>/tooling/ (always refreshed) and <skill>/templates/* to <wd>/ (kept when present,
  so a re-run never overwrites a filled-in template).
- Makes the private findings folder, outside the repo (default: ~/piu-spec-findings/<today>), and records it in
  <wd>/general.json as "findingsDir". Bugs and security findings are written there and nowhere else.
- Records the baseline commit in general.json.

Safe to re-run. Prints what it did.
"""
import datetime, json, os, shutil, subprocess, sys


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True)


def inside(parent, child):
    parent, child = os.path.abspath(parent), os.path.abspath(child)
    try:
        return os.path.commonpath([parent, child]) == parent
    except ValueError:
        return False


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    repo, skill = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    findings = sys.argv[sys.argv.index("--findings") + 1] if "--findings" in sys.argv else os.path.join(
        os.path.expanduser("~"), "piu-spec-findings", datetime.date.today().isoformat())
    findings = os.path.abspath(findings)
    if inside(repo, findings):
        sys.exit(f"the findings folder must be outside the repo ({repo})")
    wd = os.path.join(repo, ".claude", "spec-extraction")
    os.makedirs(wd, exist_ok=True)
    print(f"working folder: {wd}")
    if git(repo, "check-ignore", "-q", ".claude/spec-extraction/run-log.md").returncode != 0:
        sys.exit("git does not ignore .claude/spec-extraction/ - fix .gitignore before writing anything there")
    print("ignored by git: yes")

    tooling = os.path.join(wd, "tooling")
    os.makedirs(tooling, exist_ok=True)
    n = 0
    for name in sorted(os.listdir(os.path.join(skill, "tooling"))):
        src = os.path.join(skill, "tooling", name)
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(tooling, name))
            n += 1
    print(f"tooling: {n} files copied to {tooling}")
    for name in sorted(os.listdir(os.path.join(skill, "templates"))):
        src, dst = os.path.join(skill, "templates", name), os.path.join(wd, name)
        if not os.path.isfile(src):
            continue
        if os.path.exists(dst):
            print(f"template: kept existing {name}")
        else:
            shutil.copy2(src, dst)
            print(f"template: copied {name}")
    for sub in ("shape", "evidence", "runs", "design", "tags"):
        os.makedirs(os.path.join(wd, sub), exist_ok=True)

    os.makedirs(findings, exist_ok=True)
    gp = os.path.join(wd, "general.json")
    g = json.load(open(gp, encoding="utf-8"))
    g["findingsDir"] = findings.replace("\\", "/")
    g["workdir"] = wd.replace("\\", "/")
    g.setdefault("codebase", {})["repo"] = repo.replace("\\", "/")
    head = git(repo, "rev-parse", "--short", "HEAD").stdout.strip()
    branch = git(repo, "rev-parse", "--abbrev-ref", "HEAD").stdout.strip()
    if head:
        g["baseline"] = f"{branch} @ {head}"
    with open(gp, "w", encoding="utf-8", newline="\n") as f:
        json.dump(g, f, ensure_ascii=False, indent=1)
    print(f"findings folder (private, outside the repo): {findings}")
    print(f"baseline: {g.get('baseline')}")


if __name__ == "__main__":
    main()

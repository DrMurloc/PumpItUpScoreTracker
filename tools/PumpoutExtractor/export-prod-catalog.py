"""Writes songs.json / charts.json / chartmixes.json — the extractor's prod-export input — from a
prod-synced SQL Server (e.g. the local Aspire container), in the shapes ProdExport (Matcher.cs)
deserializes. Uses sqlcmd, so it needs nothing beyond the SQL Server tools.

usage: python export-prod-catalog.py <server,port> <database> <user> <password> <outDir>

The old dev/export API routes this replaced were retired when the dev harness moved onto api/v2
(docs/ARCHITECTURE.md)."""
import json, os, subprocess, sys, tempfile


def query(server, database, user, password, sql):
    # Through a file, not a pipe: sqlcmd only honours -f 65001 when writing with -o, and a piped
    # stdout comes back in the console code page with the non-Latin song names mangled.
    fd, path = tempfile.mkstemp(suffix=".tsv")
    os.close(fd)
    try:
        subprocess.run(
            ["sqlcmd", "-S", server, "-U", user, "-P", password, "-C", "-d", database, "-W", "-s", "\t", "-h", "-1",
             "-f", "65001", "-o", path, "-Q", "SET NOCOUNT ON; " + sql], check=True)
        out = open(path, encoding="utf-8-sig").read()
    finally:
        os.remove(path)
    return [line.split("\t") for line in out.splitlines() if line.strip()]


def cell(v, kind):
    if v == "NULL":
        return None
    if kind == "int":
        return int(v)
    if kind == "dec":
        return float(v)
    return v


def main():
    server, database, user, password, out_dir = sys.argv[1:6]
    os.makedirs(out_dir, exist_ok=True)
    shapes = {
        "songs.json": ("SELECT Id, Name, ImagePath, Type, Duration, Artist, MinBpm, MaxBpm FROM scores.Song",
                       [("Id", "str"), ("Name", "str"), ("ImagePath", "str"), ("Type", "str"), ("Duration", "int"),
                        ("Artist", "str"), ("MinBpm", "dec"), ("MaxBpm", "dec")]),
        "charts.json": ("SELECT Id, SongId, Level, Type, StepArtist, OriginalMixId FROM scores.Chart",
                        [("Id", "str"), ("SongId", "str"), ("Level", "int"), ("Type", "str"), ("StepArtist", "str"),
                         ("OriginalMixId", "str")]),
        "chartmixes.json": ("SELECT Id, ChartId, MixId, Level, NoteCount FROM scores.ChartMix",
                            [("Id", "str"), ("ChartId", "str"), ("MixId", "str"), ("Level", "int"), ("NoteCount", "int")]),
    }
    for name, (sql, cols) in shapes.items():
        data = []
        for row in query(server, database, user, password, sql):
            if len(row) != len(cols):
                continue
            data.append({c: cell(v, k) for (c, k), v in zip(cols, row)})
        with open(os.path.join(out_dir, name), "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False)
        print(f"{name}: {len(data)} rows")


if __name__ == "__main__":
    main()

"""The game's own channel per song, read off the step-file header piucenter republishes.
usage: python read-piucenter.py <piucenter-snapshot.zip> aliases.tsv piucenter.csv
aliases.tsv: ExternalKey <tab> SongId <tab> Name (ExternalChartAlias rows that resolved).
Every per-chart JSON in the snapshot carries SONGCATEGORY; USE_GENRE means read GENRE instead."""
import csv, collections, json, sys, zipfile
from channels import token

snapshot, aliases_path, out = sys.argv[1:4]
alias = {}
for line in open(aliases_path, encoding='utf-8-sig'):
    p = line.rstrip('\n').split('\t')
    if len(p) >= 3:
        alias[p[0]] = (p[1], p[2])


def header(o, depth=0):
    if depth > 6:
        return None
    if isinstance(o, dict):
        if 'SONGCATEGORY' in o or 'GENRE' in o:
            return o.get('GENRE'), o.get('SONGCATEGORY')
        for v in o.values():
            r = header(v, depth + 1)
            if r:
                return r
    elif isinstance(o, list):
        for v in o[:50]:
            r = header(v, depth + 1)
            if r:
                return r
    return None


z = zipfile.ZipFile(snapshot)
per_song = collections.defaultdict(collections.Counter)
names = {}
unaliased = no_header = 0
for entry in z.namelist():
    if not entry.endswith('.json') or 'page-content' in entry:
        continue
    key = entry.rsplit('/', 1)[-1][:-5]
    if key not in alias:
        unaliased += 1
        continue
    try:
        found = header(json.loads(z.read(entry).decode('utf-8', 'replace')))
    except Exception:
        found = None
    if not found:
        no_header += 1
        continue
    genre, category = found
    channel = token(genre if category == 'USE_GENRE' else category) or token(genre)
    if channel is None:
        continue
    song_id, name = alias[key]
    per_song[song_id][channel] += 1
    names[song_id] = name

disagree = {s: c for s, c in per_song.items() if len(c) > 1}
print(f'charts aliased {sum(sum(c.values()) for c in per_song.values())}, unaliased {unaliased}, without a header {no_header}')
print(f'songs with a channel {len(per_song)}; songs whose charts disagree with each other {len(disagree)}')
for s, c in disagree.items():
    print('  ', names[s], dict(c))
with open(out, 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f)
    w.writerow(['songId', 'name', 'channel'])
    for s, c in sorted(per_song.items(), key=lambda kv: names[kv[0]]):
        w.writerow([s, names[s], c.most_common(1)[0][0]])
print('wrote', out)

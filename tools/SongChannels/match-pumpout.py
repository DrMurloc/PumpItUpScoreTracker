"""Pumpout's channel per song, matched to our songs by name and cut.
usage: python match-pumpout.py <pumpout.db> songs.tsv pumpout.csv
songs.tsv: Id <tab> Name <tab> Type <tab> DebutMix <tab> Charts.
A name that matches several pumpout songs, or none, is left for merge.py's hand list."""
import collections, csv, re, sqlite3, sys
from channels import token

db_path, songs_path, out = sys.argv[1:4]
c = sqlite3.connect(db_path)
CUT = {1: 'ShortCut', 2: 'Arcade', 3: 'Remix', 4: 'FullSong'}


def norm(s):
    return re.sub(r'[^a-z0-9]', '', s.casefold().replace('’', "'"))


pumpout = {}
for song_id, cut_id, internal in c.execute('select songId, cutId, internalTitle from song'):
    t = c.execute('select title from songTitle where songId=? and languageId=22', (song_id,)).fetchone()
    cat = c.execute('select c.internalTitle from songCategory sc join category c on c.categoryId=sc.categoryId where sc.songId=?',
                    (song_id,)).fetchone()
    pumpout[song_id] = ((t[0] if t else internal).strip(), CUT.get(cut_id, 'Arcade'), token(cat[0]) if cat else None)

by_key = collections.defaultdict(list)
for song_id, (title, cut, _) in pumpout.items():
    k = norm(title)
    by_key[(k, cut)].append(song_id)
    for suffix in ('shortcut', 'fullsong', 'remix'):
        if k.endswith(suffix):
            by_key[(k[:-len(suffix)], cut)].append(song_id)

matched, unmatched = {}, []
for line in open(songs_path, encoding='utf-8-sig'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 4:
        continue
    song_id, name, song_type, debut = p[:4]
    k = norm(name)
    cands = by_key.get((k, song_type)) or []
    if not cands:
        for suffix in ('shortcut', 'fullsong', 'remix'):
            if k.endswith(suffix):
                cands = by_key.get((k[:-len(suffix)], song_type)) or []
    if not cands:
        cands = [s for (kk, _), ss in by_key.items() if kk == k for s in ss]
    cands = list(dict.fromkeys(cands))
    if len(cands) == 1 and pumpout[cands[0]][2]:
        matched[song_id] = (name, song_type, debut, pumpout[cands[0]][2])
    else:
        unmatched.append((debut, name, len(cands)))

print(f'songs matched to pumpout {len(matched)}; unmatched {len(unmatched)}')
print('unmatched by debut mix:', dict(collections.Counter(u[0] for u in unmatched)))
for u in sorted(unmatched):
    if u[0] not in ('Phoenix', 'Phoenix2'):
        print('  ', u)
with open(out, 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f)
    w.writerow(['songId', 'name', 'type', 'debutMix', 'channel'])
    for song_id, row in sorted(matched.items(), key=lambda kv: kv[1][0]):
        w.writerow([song_id, *row])
print('wrote', out)

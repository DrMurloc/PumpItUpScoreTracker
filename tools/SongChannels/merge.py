"""One channel per song, the sources in the order they win, and the name gaps settled by hand.
usage: python merge.py songs.tsv piucenter.csv pumpout.csv playlists.csv song-channels.csv"""
import collections, csv, sys

songs_path, piucenter_path, pumpout_path, playlists_path, out = sys.argv[1:6]

# The songs our catalog names differently from pumpout, resolved against pumpout's candidates by
# artist on 2026-09-13; every candidate pair shared a channel, so the identity gap never changes
# the answer (docs/design/song-channels.md §7).
HAND = {
    'Adios': 'KPop', 'Bad 8 End 8 Night': 'JMusic', 'Baroque Virus - FULL SONG -': 'WorldMusic',
    'Boong Boong': 'KPop', 'FOUR SEASONS OF LONELINESS verß feat. sariyajin': 'JMusic',
    'FOUR SEASONS OF LONELINESS verß feat. sariyajin - FULL SONG -': 'JMusic', 'Further': 'Original',
    'HANN': 'KPop', 'NEKKOYA - FULL SONG -': 'KPop', 'Nekkoya': 'KPop', 'PICK ME': 'KPop', 'Step': 'KPop',
    'The Little Prince': 'KPop',
}

songs = {}
for line in open(songs_path, encoding='utf-8-sig'):
    p = line.rstrip('\n').split('\t')
    if len(p) >= 4:
        songs[p[0]] = dict(name=p[1], type=p[2], debut=p[3])
piucenter = {r['songId']: r['channel'] for r in csv.DictReader(open(piucenter_path, encoding='utf-8'))}
pumpout = {r['songId']: r['channel'] for r in csv.DictReader(open(pumpout_path, encoding='utf-8'))}
by_name = collections.defaultdict(list)
for song_id, s in songs.items():
    by_name[s['name']].append(song_id)
playlists = {}
for r in csv.DictReader(open(playlists_path, encoding='utf-8')):
    for song_id in by_name.get(r['song'], []):
        playlists.setdefault(song_id, r['channel'])

rows, sources, disagreements = [], collections.Counter(), []
for song_id, s in songs.items():
    cands = [('piucenter', piucenter.get(song_id)), ('pumpout', pumpout.get(song_id)), ('playlist', playlists.get(song_id))]
    cands = [(src, ch) for src, ch in cands if ch]
    if not cands and s['name'] in HAND:
        cands = [('hand', HAND[s['name']])]
    channel = cands[0][1] if cands else ''
    source = cands[0][0] if cands else ''
    others = {ch for _, ch in cands[1:] if ch != channel}
    if others:
        disagreements.append((s['name'], s['debut'], channel, source, cands[1:]))
    sources[source or 'NONE'] += 1
    rows.append(dict(songId=song_id, name=s['name'], type=s['type'], debutMix=s['debut'], channel=channel,
                     source=source, agreeingSources=len(cands) - len(others)))

print('songs', len(rows), '| by winning source', dict(sources))
print('by channel', dict(collections.Counter(r['channel'] for r in rows)))
print('two or more sources agree on', sum(1 for r in rows if r['agreeingSources'] >= 2))
print('disagreements (winner first):')
for d in disagreements:
    print('  ', d)
missing = [r['name'] for r in rows if not r['channel']]
print('without a channel:', len(missing), missing[:20])
rows.sort(key=lambda r: (r['channel'], r['name']))
with open(out, 'w', newline='', encoding='utf-8-sig') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)
print('wrote', out)

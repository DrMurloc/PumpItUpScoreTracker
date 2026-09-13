"""The channel Andamiro's per-version playlists file each Phoenix-era song under.
usage: python join-playlists.py <playlist crawl out/> phoenixcharts.tsv chartvideos.tsv playlists.csv
The two TSVs are tools/YouTubePlaylists' (its README, step 3); the join is its join.py's, keeping
the channel from the playlist title's parenthesis instead of the patch from its version."""
import collections, csv, glob, os, re, sys
from channels import token

out_dir, charts_path, videos_path, out = sys.argv[1:5]
P1 = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'


def norm(s):
    s = re.sub(r'\([^()]*[가-힣][^()]*\)', '', s)
    return re.sub(r'[^a-z0-9]', '', s.casefold())


charts = {}
by_key = collections.defaultdict(list)
for line in open(charts_path, encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 7:
        continue
    mix_id, chart_id, song, chart_type, level, origin, players = p
    mix = 'Phoenix' if mix_id.upper() == P1 else 'Phoenix2'
    charts[(mix, chart_id)] = song
    key_level = int(players) if chart_type == 'CoOp' else int(level)
    by_key[(mix, norm(song), chart_type, key_level)].append(chart_id)

by_video = collections.defaultdict(list)
for line in open(videos_path, encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 2:
        continue
    m = re.search(r'/embed/([A-Za-z0-9_-]{11})', p[0])
    if m:
        by_video[m.group(1)].append(p[1])

CODE = re.compile(r'(CO-?OP\s*\(?\s*x?\s*(\d)\s*\)?|([SD])(\d{1,2}))', re.I)


def parse_title(title):
    body = re.sub(r'^\[[^\]]*\]\s*', '', title)
    codes = []
    for m in CODE.finditer(body):
        if m.group(2):
            codes.append(('CoOp', int(m.group(2))))
        else:
            codes.append(('Single' if m.group(3).upper() == 'S' else 'Double', int(m.group(4))))
    first = CODE.search(body)
    song = body[:first.start()] if first else body
    return re.sub(r'\([^()]*[가-힣][^()]*\)', '', song).strip(' ,-'), codes


assigned = {}
for path in glob.glob(os.path.join(out_dir, '*.json')):
    import json
    d = json.load(open(path, encoding='utf-8'))
    title = d['title']
    if 'STEP CHART' not in title:
        continue
    mix = 'Phoenix2' if 'PHOENIX 2]' in title else 'Phoenix'
    m = re.search(r'\(([^)]+)\)\s*$', title)
    channel = token(m.group(1)) if m else None
    if channel is None:
        print('SKIP', title)
        continue
    for v in d['videos']:
        ids = [c for c in by_video.get(v['videoId'], []) if (mix, c) in charts]
        if not ids:
            song, codes = parse_title(v['title'])
            for chart_type, level in codes:
                ids += by_key.get((mix, norm(song), chart_type, level), [])
        for c in ids:
            assigned.setdefault((mix, c), channel)

per_song = collections.defaultdict(collections.Counter)
for (mix, c), channel in assigned.items():
    per_song[(mix, charts[(mix, c)])][channel] += 1
multi = {k: v for k, v in per_song.items() if len(v) > 1}
print(f'songs with a channel from a playlist {len(per_song)}; songs whose charts sit in more than one {len(multi)}')
for k, v in multi.items():
    print('  ', k, dict(v))
with open(out, 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f)
    w.writerow(['mix', 'song', 'channel', 'charts'])
    for (mix, song), counts in sorted(per_song.items()):
        w.writerow([mix, song, counts.most_common(1)[0][0], sum(counts.values())])
print('wrote', out)

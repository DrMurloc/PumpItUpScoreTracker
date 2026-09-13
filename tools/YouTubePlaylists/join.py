"""Joins the crawled playlists (out/*.json) to the tracker's charts: by stored video id first, by
title where the id misses, per mix. Inputs, all in the working directory (README.md, step 3):
chartvideos.tsv, phoenixcharts.tsv, out/. Outputs playlist-chart-versions.csv, unmatched.tsv and
the coverage report on stdout (docs/design/chart-versions.md §5)."""
import json, glob, re, csv, collections, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
P1 = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'; P2 = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
MIXNAME = {'Phoenix': 'Phoenix', 'Phoenix2': 'Phoenix2'}

def norm(s):
    s = re.sub(r'\([^()]*[가-힣][^()]*\)', '', s)      # drop Hangul paren groups
    return re.sub(r'[^a-z0-9]', '', s.casefold())

# every Phoenix / Phoenix 2 chart row
charts = {}                         # (mix, chartId) -> row
bykey = collections.defaultdict(list)   # (mix, normSong, type, level) -> [chartId]
songs = collections.defaultdict(set)    # (mix, normSong) -> {chartId}
for line in open('phoenixcharts.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 7: continue
    mixid, cid, song, ctype, level, origin, pc = p
    mix = 'Phoenix' if mixid.upper() == P1 else 'Phoenix2'
    row = dict(mix=mix, chartId=cid, song=song, type=ctype, level=int(level), origin=origin, playerCount=int(pc))
    charts[(mix, cid)] = row
    lvl = row['playerCount'] if ctype == 'CoOp' else row['level']
    bykey[(mix, norm(song), ctype, lvl)].append(cid)
    songs[(mix, norm(song))].add(cid)

# stored video ids -> chart ids
byvid = collections.defaultdict(list)
for line in open('chartvideos.tsv', encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 8: continue
    m = re.search(r'/embed/([A-Za-z0-9_-]{11})', p[0])
    if m: byvid[m.group(1)].append(p[1])

def playlist_version(title):
    mix = 'Phoenix2' if 'PHOENIX 2]' in title else 'Phoenix'
    v = re.search(r'v\.?(\d\.\d\d\.\d)', title).group(1)
    return mix, ('1.00.0' if v == '1.00.1' else v)

CODE = re.compile(r'(CO-?OP\s*\(?\s*x?\s*(\d)\s*\)?|([SD])(\d{1,2}))', re.I)
def parse_title(t):
    body = re.sub(r'^\[[^\]]*\]\s*', '', t)          # drop "[PUMP IT UP - PHOENIX] "
    codes = []
    for m in CODE.finditer(body):
        if m.group(2): codes.append(('CoOp', int(m.group(2))))
        else: codes.append(('Single' if m.group(3).upper() == 'S' else 'Double', int(m.group(4))))
    first = CODE.search(body)
    song = body[:first.start()] if first else body
    song = re.sub(r'\([^()]*[가-힣][^()]*\)', '', song).strip(' ,-')
    return song, codes

assign = {}                              # (mix, chartId) -> (version, how)
unmatched = []
stats = collections.Counter()
for f in glob.glob('out/*.json'):
    d = json.load(open(f, encoding='utf-8'))
    mix, ver = playlist_version(d['title'])
    for v in d['videos']:
        cids = [c for c in byvid.get(v['videoId'], []) if (mix, c) in charts]
        if cids:
            for c in cids: assign.setdefault((mix, c), (ver, 'id'))
            stats[(mix, ver, 'id')] += len(cids); continue
        song, codes = parse_title(v['title'])
        ns = norm(song); hit = 0
        for ctype, lvl in codes:
            ids = bykey.get((mix, ns, ctype, lvl))
            how = 'title'
            if not ids:   # level drift of one, same song and type
                for dl in (-1, 1):
                    ids = bykey.get((mix, ns, ctype, lvl + dl))
                    if ids: how = 'title~level'; break
            if ids:
                for c in ids: assign.setdefault((mix, c), (ver, how))
                stats[(mix, ver, how)] += len(ids); hit += 1
            else:
                unmatched.append((mix, ver, v['videoId'], v['title'], song, f'{ctype}{lvl}', 'song known' if (mix, ns) in songs else 'song unknown'))
        if not codes: unmatched.append((mix, ver, v['videoId'], v['title'], song, '', 'no chart code'))

# report
for mix in ('Phoenix', 'Phoenix2'):
    natives = {cid for (m, cid), r in charts.items() if m == mix and r['origin'] == MIXNAME[mix]}
    matched = {cid for (m, cid) in assign if m == mix}
    print(f"\n### {mix}: assigned {len(matched)} charts; natives {len(natives)}; natives assigned {len(natives & matched)}; natives left = launch by rule {len(natives - matched)}; non-natives assigned (revivals or new charts on old songs) {len(matched - natives)}")
    vers = sorted({v for (m, v, h) in stats if m == mix})
    for ver in vers:
        by = {h: stats[(mix, ver, h)] for h in ('id', 'title', 'title~level') if stats[(mix, ver, h)]}
        um = sum(1 for u in unmatched if u[0] == mix and u[1] == ver)
        print(f"   {ver}: {sum(by.values()):>4} charts  {by}  unmatched codes {um}")
    left = sorted(natives - matched, key=lambda c: (charts[(mix, c)]['song'], charts[(mix, c)]['type'], charts[(mix, c)]['level']))
    print(f"  natives not in any playlist ({len(left)}): " + '; '.join(f"{charts[(mix,c)]['song']} {charts[(mix,c)]['type'][0]}{charts[(mix,c)]['level']}" for c in left))
    nn = collections.Counter((assign[(mix, c)][0], charts[(mix, c)]['origin']) for c in matched - natives)
    print(f"  non-native assignments: {dict(nn)}")
print(f"\nunmatched codes: {len(unmatched)}")
for u in unmatched[:40]: print('  ', u[0], u[1], u[5], '|', u[3], '|', u[6])

rows = [dict(mix=m, chartId=c, song=charts[(m, c)]['song'], type=charts[(m, c)]['type'], level=charts[(m, c)]['level'], origin=charts[(m, c)]['origin'], version=v, how=h) for (m, c), (v, h) in assign.items()]
rows.sort(key=lambda r: (r['mix'], r['version'], r['song'], r['type'], r['level']))
with open('playlist-chart-versions.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
with open('unmatched.tsv', 'w', encoding='utf-8') as f:
    for u in unmatched: f.write('\t'.join(map(str, u)) + '\n')

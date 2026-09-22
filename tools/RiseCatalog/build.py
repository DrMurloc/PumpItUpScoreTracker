#!/usr/bin/env python3
"""Emit the Pump It Up RISE catalog as idempotent SQL (docs/design/rise.md §11.3).

Reads a folder of inputs and writes four single-transaction scripts plus reports to an output
folder. Nothing here touches a database; the scripts are reviewed and run by hand, in order,
after the RiseMixes migration has seeded the two scores.Mix rows.

    python build.py <inputs-dir> <out-dir>

Inputs (all CSV/JSON, standard library only):
  rise-song-list.csv        the community sheet: Title, Composer, Category, S1..S6, HD1..HD6, ..., Note, Version
  arcade-station-songs.csv  Dave's Arcade Station list: Title
  export-Phoenix.csv        the site's /Charts/Export.csv for Phoenix: Song, Type, Level, ...
  export-Phoenix2.csv       the same for Phoenix 2
  patches.csv               Mix, Version, ReleaseDate — every Rise patch, dated from the Steam notices
  jackets-manifest.csv      title, gameId, file, ... — the jackets already on the CDN under songs/
  durations.csv (optional)  Title, Seconds — song lengths where measured; 0 otherwise
  arcade-additions.csv (optional)  Title, Version — Arcade Station songs a patch after 1.4.0 added

Every Guid is a v5 of its natural key, so a re-run emits the same ids and every INSERT is
guarded by NOT EXISTS: the scripts are no-ops the second time.
"""
import collections
import csv
import difflib
import functools
import json
import os
import sys
import unicodedata
import uuid

NS = uuid.uuid5(uuid.NAMESPACE_URL, 'https://piuscores.arroweclip.se/rise-catalog')
MIX = {
    'Rise': '8FF3F8AA-3870-4FFB-85F6-97340E091506',
    'RiseArcade': '71B55C75-38BE-492C-97D3-29BE828C83B6',
    'Phoenix': '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B',
    'Phoenix2': 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948',
}
IMAGE_ROOT = 'https://piuimages.arroweclip.se/songs/'
# The sheet's spellings the game disagrees with; the catalog uses the game's.
TITLE_FIXES = {'Pop & Pump & FIVE!!': 'Pop & Pump & DIVE!!'}
# Cells the sheet has wrong, by (title, column): the arcade's CO5M1C R4ILR0AD singles are 10/15/18/21.
LEVEL_FIXES = {('COSM1C R4ILROAD', 'S1'): 10}
MIN_LEVEL, MAX_LEVEL = 1, 29  # DifficultyLevel's range; a value outside it stops the run, never a silent drop
CATEGORY_TYPE = {'REMIX': 'Remix'}  # every other category is an Arcade song
ARCADE_LAUNCH = '1.4.0'  # the Arcade Station's patch: every Rise Arcade chart's arrival version
INF = 10 ** 6


def norm(s):
    s = unicodedata.normalize('NFKC', str(s)).lower()
    return ''.join(c for c in s if c.isalnum())


def guid(*parts):
    return str(uuid.uuid5(NS, '|'.join(str(p) for p in parts))).upper()


def q(s):
    return "N'" + str(s).replace("'", "''") + "'"


def image_name(title):
    return ''.join(c for c in title if c.isascii() and c.isalnum()) + '.png'


def load_export(path):
    charts = collections.defaultdict(lambda: {'S': [], 'D': []})
    names = {}
    with open(path, encoding='utf-8-sig') as f:
        for row in csv.DictReader(f):
            if row['Type'] not in ('S', 'D'):
                continue
            k = norm(row['Song'])
            charts[k][row['Type']].append(int(row['Level']))
            names[k] = row['Song']
    for k in charts:
        charts[k]['S'].sort()
        charts[k]['D'].sort()
    return charts, names


def align(a, b, max_shift=2, ins=2.5, dele=1.5, alt=None):
    """Monotone alignment of sorted levels a (Rise) onto b (arcade, Phoenix 1 levels), alt[j]
    the chart's Phoenix 2 level. Returns (cost, number of optimal alignments, one path)."""
    n, m = len(a), len(b)

    def sub(i, j):
        diff = abs(a[i] - b[j])
        if diff == 0 or (alt and alt[j] is not None and a[i] == alt[j]):
            return 0
        return diff if diff <= max_shift else INF

    @functools.lru_cache(None)
    def best(i, j):
        if i == n and j == m:
            return 0, 1
        opts = []
        if i < n:
            c, k = best(i + 1, j)
            opts.append((c + ins, k))
        if j < m:
            c, k = best(i, j + 1)
            opts.append((c + dele, k))
        if i < n and j < m and sub(i, j) < INF:
            c, k = best(i + 1, j + 1)
            opts.append((c + sub(i, j), k))
        lo = min(o[0] for o in opts)
        return lo, sum(o[1] for o in opts if o[0] == lo)

    def path(i, j):
        out = []
        while not (i == n and j == m):
            lo, _ = best(i, j)
            if i < n and j < m and sub(i, j) < INF and best(i + 1, j + 1)[0] + sub(i, j) == lo:
                out.append(('M', i, j)); i += 1; j += 1
                continue
            if i < n and best(i + 1, j)[0] + ins == lo:
                out.append(('I', i, None)); i += 1
                continue
            out.append(('D', None, j)); j += 1
        return out

    c, k = best(0, 0)
    return c, k, path(0, 0)


def levels(row, columns):
    """The levels a sheet row carries in these columns, fixed where LEVEL_FIXES says so and refused
    outside DifficultyLevel's range — a typo like 110 would otherwise become a chart the site cannot load."""
    out = []
    for col in columns:
        raw = row[col].strip()
        if not raw:
            continue
        value = LEVEL_FIXES.get((row['Title'].strip(), col), int(raw))
        if not MIN_LEVEL <= value <= MAX_LEVEL:
            raise SystemExit(f"{row['Title']} {col} = {raw}: not a level {MIN_LEVEL}-{MAX_LEVEL}; add it to LEVEL_FIXES")
        out.append(value)
    return sorted(out)


def main(inputs, out):
    os.makedirs(os.path.join(out, 'reports'), exist_ok=True)
    p1, names1 = load_export(os.path.join(inputs, 'export-Phoenix.csv'))
    p2, names2 = load_export(os.path.join(inputs, 'export-Phoenix2.csv'))
    names = {**names2, **names1}
    known = set(p1) | set(p2)

    patches = collections.defaultdict(list)
    with open(os.path.join(inputs, 'patches.csv'), encoding='utf-8-sig') as f:
        for row in csv.DictReader(f):
            patches[row['Mix']].append((row['Version'], row['ReleaseDate'] or None))
    jackets = {}
    with open(os.path.join(inputs, 'jackets-manifest.csv'), encoding='utf-8-sig') as f:
        for row in csv.DictReader(f):
            jackets[norm(row['title'])] = row['file']
    additions = {}
    apath = os.path.join(inputs, 'arcade-additions.csv')
    if os.path.exists(apath):
        with open(apath, encoding='utf-8-sig') as f:
            for row in csv.DictReader(f):
                additions[norm(row['Title'])] = row['Version'].strip()
    durations = {}
    dpath = os.path.join(inputs, 'durations.csv')
    if os.path.exists(dpath):
        with open(dpath, encoding='utf-8-sig') as f:
            for row in csv.DictReader(f):
                durations[norm(row['Title'])] = int(float(row['Seconds']))

    with open(os.path.join(inputs, 'rise-song-list.csv'), encoding='utf-8-sig') as f:
        sheet = [r for r in csv.DictReader(f) if r['Title']]

    songs_new, charts_new, membership, alignment_rows, fuzzy, versions_used = [], [], [], [], [], set()
    counts = collections.Counter()
    for r in sheet:
        title = r['Title'].strip()
        if title.endswith('.0') and title[:-2].isdigit():
            title = title[:-2]  # a numeric title the spreadsheet stored as a float ("1948.0")
        title = TITLE_FIXES.get(title, title)
        version = (r['Version'] or 'Base').strip()
        versions_used.add(version)
        singles = levels(r, ['S1', 'S2', 'S3', 'S4', 'S5', 'S6'])
        halves = levels(r, ['HD1', 'HD2', 'HD3', 'HD4', 'HD5', 'HD6'])
        k = norm(title)
        if k not in known:
            m = difflib.get_close_matches(k, list(known), n=1, cutoff=0.85)
            if m:
                fuzzy.append((title, names[m[0]]))
                k = m[0]
        existing = k in known
        tracker_name = names[k] if existing else title
        song_ref = ('name', tracker_name) if existing else ('new', guid('song', norm(title)))
        if not existing:
            songs_new.append({
                'id': song_ref[1], 'name': title, 'artist': r['Composer'].strip()[:128],
                'type': CATEGORY_TYPE.get(r['Category'].strip(), 'Arcade'),
                'image': IMAGE_ROOT + jackets.get(norm(title), image_name(title)),
                'has_art': norm(title) in jackets, 'duration': durations.get(norm(title), 0)
            })
            counts['new songs'] += 1

        # singles: existing songs align onto the arcade charts; new songs are all new charts
        if existing and singles:
            a1, a2 = p1.get(k, {}).get('S', []), p2.get(k, {}).get('S', [])
            arcade = []
            if a1 and a2:
                _, _, pth = align(a1, a2, max_shift=2, ins=1, dele=1)
                for op, i, j in pth:
                    if op == 'M':
                        arcade.append((a1[i], a2[j]))
                    elif op == 'I':
                        arcade.append((a1[i], None))
                    else:
                        arcade.append((None, a2[j]))
            else:
                arcade = [(x, None) for x in a1] or [(None, x) for x in a2]
            arcade.sort(key=lambda c: c[0] if c[0] is not None else c[1])
            origin = [c[0] if c[0] is not None else c[1] for c in arcade]
            alt = [c[1] for c in arcade]
            cost, nopt, pth = align(singles, origin, alt=alt)
            flags = [f'{nopt} equally good alignments'] if nopt > 1 else []
            for op, i, j in pth:
                if op == 'M':
                    rl, (o1, o2) = singles[i], arcade[j]
                    if o1 is not None:
                        target = ('Phoenix', o1)
                    else:
                        target = ('Phoenix2', o2)
                    reading = 'unchanged' if rl == (o1 if o1 is not None else o2) else (
                        'Rise re-rated; Phoenix 2 followed' if rl == o2 else f'Rise-only re-rate {o1 if o1 is not None else o2}->{rl}')
                    if abs(rl - (o1 if o1 is not None else o2)) == 2:
                        flags.append(f'+/-2 shift on S{rl}')
                    membership.append({'mix': 'Rise', 'chart': ('existing', tracker_name, 'Single', target), 'level': rl,
                                       'version': version, 'id': guid('Rise', 'chartmix', norm(title), 'Single', rl)})
                    counts['singles onto arcade charts'] += 1
                    alignment_rows.append([tracker_name, r['Category'], '/'.join(map(str, singles)), '/'.join(map(str, a1)),
                                           '/'.join(map(str, a2)), rl, f'arcade S{target[1]} ({target[0]})', reading, '; '.join(flags)])
                elif op == 'I':
                    rl = singles[i]
                    cid = guid('chart', norm(title), 'Single', rl)
                    charts_new.append({'id': cid, 'song': song_ref, 'type': 'Single', 'level': rl})
                    membership.append({'mix': 'Rise', 'chart': ('new', cid), 'level': rl, 'version': version,
                                       'id': guid('Rise', 'chartmix', norm(title), 'Single', rl)})
                    counts['Rise-only singles on existing songs'] += 1
                    alignment_rows.append([tracker_name, r['Category'], '/'.join(map(str, singles)), '/'.join(map(str, a1)),
                                           '/'.join(map(str, a2)), rl, 'NEW Rise-only chart', 'no arcade single within 2 levels', '; '.join(flags)])
                else:
                    o1, o2 = arcade[j]
                    alignment_rows.append([tracker_name, r['Category'], '/'.join(map(str, singles)), '/'.join(map(str, a1)),
                                           '/'.join(map(str, a2)), '', f'arcade S{o1 if o1 is not None else o2} NOT carried by Rise', '', '; '.join(flags)])
        else:
            for rl in singles:
                cid = guid('chart', norm(title), 'Single', rl)
                charts_new.append({'id': cid, 'song': song_ref, 'type': 'Single', 'level': rl})
                membership.append({'mix': 'Rise', 'chart': ('new', cid), 'level': rl, 'version': version,
                                   'id': guid('Rise', 'chartmix', norm(title), 'Single', rl)})
                counts['singles on new songs'] += 1
        # every half-double is its own chart (D7)
        for hl in halves:
            cid = guid('chart', norm(title), 'HalfDouble', hl)
            charts_new.append({'id': cid, 'song': song_ref, 'type': 'HalfDouble', 'level': hl})
            membership.append({'mix': 'Rise', 'chart': ('new', cid), 'level': hl, 'version': version,
                               'id': guid('Rise', 'chartmix', norm(title), 'HalfDouble', hl)})
            counts['half-doubles'] += 1

    # Rise Arcade: every Phoenix 2 Single and Double of Dave's 352 songs, at Phoenix 2 levels
    arcade_rows, arcade_unmatched = [], []
    with open(os.path.join(inputs, 'arcade-station-songs.csv'), encoding='utf-8-sig') as f:
        for row in csv.DictReader(f):
            arcade_title = row['Title'].strip()
            if arcade_title.endswith('.0') and arcade_title[:-2].isdigit():
                arcade_title = arcade_title[:-2]  # "86.0": a numeric title stored as a float
            k = norm(arcade_title)
            if k not in p2:
                m = difflib.get_close_matches(k, list(p2), n=1, cutoff=0.85)
                if not m:
                    arcade_unmatched.append(row['Title'])
                    continue
                fuzzy.append((row['Title'], names2[m[0]]))
                k = m[0]
            version = additions.get(k, ARCADE_LAUNCH)
            for t, label in (('S', 'Single'), ('D', 'Double')):
                for lv in p2[k][t]:
                    arcade_rows.append({'name': names2[k], 'type': label, 'level': lv, 'version': version,
                                        'id': guid('RiseArcade', 'chartmix', k, label, lv)})
    counts['Rise Arcade rows'] = len(arcade_rows)

    # A song the sheet lists twice (it has happened) would mint the same ids twice; keep the first.
    def dedupe(rows):
        seen, kept = set(), []
        for row in rows:
            if row['id'] in seen:
                counts['duplicate rows dropped'] += 1
                continue
            seen.add(row['id'])
            kept.append(row)
        return kept

    songs_new, charts_new, membership, arcade_rows = map(dedupe, (songs_new, charts_new, membership, arcade_rows))

    missing_versions = versions_used - {v for v, _ in patches['Rise']}
    if missing_versions:
        raise SystemExit(f'patches.csv lacks Rise versions the sheet uses: {sorted(missing_versions)}')

    head = ("SET XACT_ABORT ON;\nSET NOCOUNT ON;\nBEGIN TRAN;\n"
            f"DECLARE @Rise uniqueidentifier = '{MIX['Rise']}';\n"
            f"DECLARE @RiseArcade uniqueidentifier = '{MIX['RiseArcade']}';\n"
            f"DECLARE @Phoenix uniqueidentifier = '{MIX['Phoenix']}';\n"
            f"DECLARE @Phoenix2 uniqueidentifier = '{MIX['Phoenix2']}';\n"
            "IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = @Rise) RAISERROR('The RiseMixes migration has not run', 16, 1);\n")

    # s1: the patches
    s1 = [head]
    for mix, rows in patches.items():
        for order, (version, date) in enumerate(rows, start=1):
            vid = guid(mix, 'version', version)
            date_sql = f"'{date}'" if date else 'NULL'
            s1.append(f"IF NOT EXISTS (SELECT 1 FROM [scores].[MixVersion] WHERE [MixId] = @{mix} AND [Name] = {q(version)}) "
                      f"INSERT INTO [scores].[MixVersion] ([Id], [MixId], [Name], [ReleaseDate], [SortOrder]) "
                      f"VALUES ('{vid}', @{mix}, {q(version)}, {date_sql}, {order * 10});")
    s1.append('COMMIT;')

    # s2: songs and charts
    s2 = [head]
    for s in songs_new:
        dur = s['duration'] * 10_000_000  # Song.Duration is a TimeSpan stored as ticks in a bigint
        s2.append(f"IF NOT EXISTS (SELECT 1 FROM [scores].[Song] WHERE [Id] = '{s['id']}') "
                  f"INSERT INTO [scores].[Song] ([Id], [Name], [ImagePath], [Type], [Duration], [MinBpm], [MaxBpm], [Artist]) "
                  f"VALUES ('{s['id']}', {q(s['name'])}, {q(s['image'])}, '{s['type']}', {dur}, NULL, NULL, {q(s['artist'])});")
    for c in charts_new:
        kind, ref = c['song']
        song_sql = (f"(SELECT TOP 1 s.Id FROM [scores].[Song] s WHERE s.[Name] = {q(ref)} AND EXISTS "
                    f"(SELECT 1 FROM [scores].[Chart] c2 JOIN [scores].[ChartMix] cm2 ON cm2.ChartId = c2.Id "
                    f"WHERE c2.SongId = s.Id AND cm2.MixId IN (@Phoenix, @Phoenix2)))") if kind == 'name' else f"'{ref}'"
        s2.append(f"IF NOT EXISTS (SELECT 1 FROM [scores].[Chart] WHERE [Id] = '{c['id']}') "
                  f"INSERT INTO [scores].[Chart] ([Id], [SongId], [Level], [Type], [StepArtist], [OriginalMixId], [PlayerCount]) "
                  f"VALUES ('{c['id']}', {song_sql}, {c['level']}, '{c['type']}', NULL, @Rise, 1);")
    s2.append(f"IF (SELECT COUNT(*) FROM [scores].[Chart] WHERE [OriginalMixId] = @Rise) < {len(charts_new)} "
              f"RAISERROR('Expected {len(charts_new)} Rise-origin charts', 16, 1);")
    s2.append('COMMIT;')

    # s3: Rise membership
    s3 = [head]
    for m in membership:
        vid = guid('Rise', 'version', m['version'])
        if m['chart'][0] == 'new':
            src = f"SELECT '{m['id']}', '{m['chart'][1]}', @Rise, {m['level']}, NULL, '{vid}'"
            where = f"WHERE NOT EXISTS (SELECT 1 FROM [scores].[ChartMix] x WHERE x.[Id] = '{m['id']}')"
        else:
            _, name, ctype, (origin_mix, origin_level) = m['chart']
            src = (f"SELECT '{m['id']}', c.Id, @Rise, {m['level']}, NULL, '{vid}' FROM [scores].[Chart] c "
                   f"JOIN [scores].[Song] s ON s.Id = c.SongId JOIN [scores].[ChartMix] cm ON cm.ChartId = c.Id AND cm.MixId = @{origin_mix}")
            where = (f"WHERE s.[Name] = {q(name)} AND c.[Type] = '{ctype}' AND cm.[Level] = {origin_level} "
                     f"AND NOT EXISTS (SELECT 1 FROM [scores].[ChartMix] x WHERE x.ChartId = c.Id AND x.MixId = @Rise)")
        s3.append(f"INSERT INTO [scores].[ChartMix] ([Id], [ChartId], [MixId], [Level], [NoteCount], [AddedInVersionId]) {src} {where};")
    s3.append(f"IF (SELECT COUNT(*) FROM [scores].[ChartMix] WHERE [MixId] = @Rise) < {len(membership)} "
              f"RAISERROR('Expected {len(membership)} Rise membership rows — a song name did not resolve', 16, 1);")
    s3.append('COMMIT;')

    # s4: Rise Arcade membership
    s4 = [head]
    for a in arcade_rows:
        version_id = guid('RiseArcade', 'version', a['version'])
        s4.append(f"INSERT INTO [scores].[ChartMix] ([Id], [ChartId], [MixId], [Level], [NoteCount], [AddedInVersionId]) "
                  f"SELECT '{a['id']}', cm.ChartId, @RiseArcade, cm.[Level], cm.[NoteCount], '{version_id}' "
                  f"FROM [scores].[ChartMix] cm JOIN [scores].[Chart] c ON c.Id = cm.ChartId JOIN [scores].[Song] s ON s.Id = c.SongId "
                  f"WHERE cm.MixId = @Phoenix2 AND s.[Name] = {q(a['name'])} AND c.[Type] = '{a['type']}' AND cm.[Level] = {a['level']} "
                  f"AND NOT EXISTS (SELECT 1 FROM [scores].[ChartMix] x WHERE x.ChartId = cm.ChartId AND x.MixId = @RiseArcade);")
    s4.append(f"IF (SELECT COUNT(*) FROM [scores].[ChartMix] WHERE [MixId] = @RiseArcade) < {len(arcade_rows)} "
              f"RAISERROR('Expected {len(arcade_rows)} Rise Arcade membership rows', 16, 1);")
    s4.append('COMMIT;')

    for name, lines in (('s1-rise-versions.sql', s1), ('s2-rise-songs-charts.sql', s2),
                        ('s3-rise-membership.sql', s3), ('s4-rise-arcade-membership.sql', s4)):
        with open(os.path.join(out, name), 'w', encoding='utf-8') as f:
            f.write('\n'.join(lines) + '\n')

    with open(os.path.join(out, 'reports', 'alignment.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['Song', 'Rise category', 'Rise S levels', 'Phoenix 1 S levels', 'Phoenix 2 S levels', 'Rise level',
                    'Mapped to', 'Reading', 'Flags'])
        w.writerows(alignment_rows)
    with open(os.path.join(out, 'reports', 'fuzzy-matches.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(f'{a}  ->  {b}' for a, b in sorted(set(fuzzy))) + '\n')
    with open(os.path.join(out, 'reports', 'unmatched-arcade-songs.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(arcade_unmatched) + '\n')
    with open(os.path.join(out, 'reports', 'art-needed.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(f"{s['name']}  ->  {s['image']}" for s in songs_new if not s['has_art']) + '\n')
    counts['membership rows (Rise)'] = len(membership)
    counts['new chart rows'] = len(charts_new)
    counts['flagged alignments'] = sum(1 for row in alignment_rows if row[-1])
    with open(os.path.join(out, 'reports', 'counts.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(f'{k}: {v}' for k, v in sorted(counts.items())) + '\n')
    print('\n'.join(f'{k}: {v}' for k, v in sorted(counts.items())))
    print('unmatched arcade songs:', len(arcade_unmatched), '| fuzzy matches:', len(set(fuzzy)),
          '| art needed:', sum(1 for s in songs_new if not s['has_art']))


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])

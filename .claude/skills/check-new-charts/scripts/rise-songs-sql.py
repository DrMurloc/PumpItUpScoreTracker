"""check-new-charts, the RISE path: the Phoenix 2 charts of RISE songs PIU Scores already has, as one SQL script.

A patch that brings RISE songs to the arcade (v1.02.0's 17, v1.03.0's 5) cannot go through /Admin/BulkAddCharts:
its collision check only knows Phoenix and Phoenix 2 names, so it would create a second copy of each song. This
emits the script that puts their arcade charts onto the songs that are already there (docs/design/rise.md D7/D8):
an arcade single joins the RISE single it was ported from (order-preserving, one level at most), anything else is
a new chart debuting in Phoenix 2, every Phoenix 2 row carries the patch, every chart its video and sides, every
song its ko-KR name. Every check THROWs, so a bad run writes nothing; @WhatIf = 1 runs it all and rolls back.

    python -I rise-songs-sql.py <spec.json> <out.sql> [appendix.tsv]

spec.json - Korean names exactly as check-korean-names.ps1 read them off the site:
    {"version": "1.03.0", "release": "2026-11-24", "jsonBatch": "phoenix2-batch-2026-11-24.json",
     "sheet": "<the RISE catalog's rise-song-list.csv>",
     "why": ["optional header lines: which of the songs are Steam-link tracks, which teaser songs"],
     "songs": {"Bamboo": "뱀부", ...}}

Run it BEFORE build-batch.ps1: it records the songs in shipped.json with the SQL as their batch, which keeps them
out of the JSON while the drift check keeps watching them. Standard library only; reads the video cache in the
state dir. RISE ids are the catalog's own v5 ids (tools/RiseCatalog/build.py); the rows this script creates get
v5 ids in a namespace of their own, so a re-run emits the same script.
"""
import csv, datetime, json, os, re, sys, uuid, unicodedata

sys.stdout.reconfigure(encoding='utf-8')
STATE = os.path.join(os.environ['USERPROFILE'], '.piu-score-tracker', 'check-new-charts')
RISE = '8FF3F8AA-3870-4FFB-85F6-97340E091506'
P2 = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
CATALOG_NS = uuid.uuid5(uuid.NAMESPACE_URL, 'https://piuscores.arroweclip.se/rise-catalog')
OWN_NS = uuid.uuid5(uuid.NAMESPACE_URL, 'https://piuscores.arroweclip.se/phoenix2-rise-songs')
TYPE_ORDER = {'Single': 0, 'Double': 1, 'CoOp': 2}


def norm(s):
    s = unicodedata.normalize('NFKC', str(s)).lower()
    return ''.join(c for c in s if c.isalnum())


def catalog_guid(*parts):
    return str(uuid.uuid5(CATALOG_NS, '|'.join(str(p) for p in parts))).upper()


def own_guid(*parts):
    return str(uuid.uuid5(OWN_NS, '|'.join(str(p) for p in parts))).upper()


def q(s):
    return "N'" + str(s).replace("'", "''") + "'"


def field(d, f):
    m = re.search(r'(?m)^\s*' + re.escape(f) + r'\s*:\s*(.+?)\s*$', d or '')
    return m.group(1).strip() if m else None


def tnorm(s):
    return re.sub(r'\s+', ' ', s.replace('’', "'")).strip().upper()


def label(t, lvl):
    return f'CO-OP x{lvl}' if t == 'CoOp' else f'{t[0]}{lvl}'


def align(arcade, rise):
    """Order-preserving pairing of arcade singles onto RISE singles, one level apart at most, exact pairs
    preferred (docs/design/rise.md §4.3). Returns the pairs; unpaired arcade singles are new charts."""
    n, m = len(arcade), len(rise)
    best = [[0] * (m + 1) for _ in range(n + 1)]
    for i in range(n - 1, -1, -1):
        for j in range(m - 1, -1, -1):
            gap = abs(arcade[i] - rise[j])
            take = (2 if gap == 0 else 1) + best[i + 1][j + 1] if gap <= 1 else -1
            best[i][j] = max(best[i + 1][j], best[i][j + 1], take)
    pairs, i, j = [], 0, 0
    while i < n and j < m:
        gap = abs(arcade[i] - rise[j])
        if gap <= 1 and best[i][j] == (2 if gap == 0 else 1) + best[i + 1][j + 1]:
            pairs.append((arcade[i], rise[j]))
            i, j = i + 1, j + 1
        elif best[i][j] == best[i + 1][j]:
            i += 1
        else:
            j += 1
    return pairs


def main():
    spec_path, out_path = sys.argv[1], sys.argv[2]
    appendix_path = sys.argv[3] if len(sys.argv) > 3 else None
    spec = json.load(open(spec_path, encoding='utf-8-sig'))
    version, release = spec['version'], spec['release']
    state = json.load(open(os.path.join(STATE, 'state.json'), encoding='utf-8-sig'))
    videos = [json.load(open(os.path.join(STATE, 'videos', f), encoding='utf-8-sig'))
              for f in os.listdir(os.path.join(STATE, 'videos'))]
    sheet = {r['Title']: r for r in csv.DictReader(open(spec['sheet'], encoding='utf-8-sig'))}

    songs = []
    for name, ko in spec['songs'].items():
        charts, bpms, lengths, bga = {}, [], [], None
        for v in videos:
            if v.get('channelId') != state['channelId'] or '[PUMP IT UP - PHOENIX 2]' not in v['title']:
                continue
            d = v.get('description') or ''
            t = field(d, 'Title') or ''
            if not (tnorm(t) == tnorm(name) or tnorm(t).startswith(tnorm(name) + ' ')):
                continue
            b = field(d, 'BPM')
            if b:
                bpms += [float(x) for x in re.findall(r'\d+(?:\.\d+)?', b)]
            if re.search(r'\]\s*BGA', v['title']) or re.search(r'(?m)^\s*(Illustrator|Visualizer)\s*:', d):
                bga = v['lengthSeconds']
                continue
            sc = field(d, 'Step Chart')
            if not sc:
                continue
            lengths.append(v['lengthSeconds'])
            for tok in sc.split(','):
                tok = tok.strip()
                m = re.match(r'(?i)^(S|D)(\d{1,2})$', tok)
                c = re.match(r'(?i)^CO-?OP\s*[xX]?\s*(\d)$', tok)
                if m:
                    key = ('Single' if m.group(1).upper() == 'S' else 'Double', int(m.group(2)))
                elif c:
                    key = ('CoOp', int(c.group(1)))
                else:
                    raise SystemExit(f'{name}: unparsed chart token {tok!r} on {v["videoId"]}')
                if key in charts:
                    raise SystemExit(f'{name}: {label(*key)} on two videos')
                charts[key] = {'video': v['videoId'], 'step': field(d, 'Step Artist')}
        if not charts:
            raise SystemExit(f'{name}: no Phoenix 2 chart videos in the cache')
        if name not in sheet:
            raise SystemExit(f'{name}: not in the RISE sheet - not a RISE song PIU Scores has?')
        row = sheet[name]
        rise_singles = sorted(int(float(row[f'S{i}'])) for i in range(1, 7) if row.get(f'S{i}', '').strip())
        arcade_singles = sorted(lvl for (t, lvl) in charts if t == 'Single')
        pairs = align(arcade_singles, rise_singles)
        paired_arcade = {a for a, _ in pairs}
        paired_rise = {r for _, r in pairs}
        for a in arcade_singles:
            if a not in paired_arcade and any(abs(a - r) <= 2 for r in rise_singles if r not in paired_rise):
                raise SystemExit(f'{name}: arcade S{a} sits near an unpaired RISE single {sorted(set(rise_singles) - paired_rise)} - review by hand')
        steps = {c['step'] for c in charts.values()}
        if None in steps:
            raise SystemExit(f'{name}: a chart video has no Step Artist')
        new = sorted(((t, lvl) for (t, lvl) in charts if t != 'Single' or lvl not in paired_arcade),
                     key=lambda k: (TYPE_ORDER[k[0]], k[1]))
        songs.append({
            'name': name, 'ko': ko, 'id': catalog_guid('song', norm(name)), 'charts': charts, 'pairs': pairs, 'new': new,
            'min': min(bpms), 'max': max(bpms), 'duration': bga if bga is not None else min(lengths),
            'duration_from': 'BGA' if bga is not None else 'shortest chart video',
        })

    total = sum(len(s['charts']) for s in songs)
    song_ids = ', '.join(f"'{s['id']}'" for s in songs)
    version_id = own_guid('version', 'Phoenix2', version)
    why = ''.join(f'\n  {line}' for line in spec.get('why', []))
    L = []
    w = L.append
    w(f"""/*
  Phoenix 2 v{version} ({release}): the arcade charts of the {len(songs)} RISE songs that reached the Phoenix 2 arcade.

  These songs already exist on PIU Scores as RISE songs (tools/RiseCatalog s2/s3, 2026-09-22), so
  /Admin/BulkAddCharts would create a second copy of each.{why}
  This script adds their Phoenix 2 charts to the songs that are already there, the way the RISE catalog
  decisions say (docs/design/rise.md D7/D8), exactly as the v1.02.0 script did for its 17:
    * an arcade single IS the RISE single it was ported from - it gets a Phoenix 2 row on the existing
      chart, at the arcade's level (matched in order; a re-rate moves a chart one level);
    * an arcade double is a NEW chart - half-doubles never map onto doubles - and so is any arcade chart
      RISE does not have; new charts debut in Phoenix 2;
    * every Phoenix 2 row is stamped with patch {version} (created here, sort order +10, if the admin
      page has not created it yet);
    * every chart gets its official video; a video holding two singles of one song gets its sides
      (lower level on the left), the same rule chart creation applies;
    * the step artist fills in where the RISE chart had none; the song's BPM and duration fill in where
      the RISE row left them empty;
    * every song gets its ko-KR name as piugame's Korean session serves it (read {release}) - Korean
      sessions import through that row.
  No channel here: a song's Phoenix 2 scores.SongMix row comes from the channels script (channels-sql.py),
  which reads the v{version} update notice and, where it is silent, the step-chart playlists.

  Run order: upload {spec['jsonBatch']} in /Admin/BulkAddCharts first (Version picker: new
  version {version}, release date {release}), then this script with @WhatIf = 1, read the result sets, then
  @WhatIf = 0, then restart the app (Admin -> Clear Cache does not evict the Korean names or the RISE chart
  dictionaries). Idempotent: a second run changes nothing. Every check THROWs, which rolls the whole script
  back.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @WhatIf bit = 1;             -- 1 = run everything, show the result, roll back. 0 = commit.

DECLARE @Rise uniqueidentifier = '{RISE}';
DECLARE @Phoenix2 uniqueidentifier = '{P2}';
DECLARE @Official nvarchar(30) = N'PUMP IT UP Official';
DECLARE @VersionId uniqueidentifier, @SongId uniqueidentifier, @ChartId uniqueidentifier;
DECLARE @En nvarchar(128), @Ko nvarchar(128), @n int, @msg nvarchar(2000);

BEGIN TRAN;

IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = @Rise) THROW 50000, N'No Rise mix row - this database has not had the RISE catalog applied', 1;
IF NOT EXISTS (SELECT 1 FROM [scores].[Mix] WHERE [Id] = @Phoenix2) THROW 50000, N'No Phoenix 2 mix row', 1;

-- the patch: the one the admin page made, or the same row it would have made
SET @VersionId = (SELECT [Id] FROM [scores].[MixVersion] WHERE [MixId] = @Phoenix2 AND [Name] = N'{version}');
IF @VersionId IS NULL
BEGIN
    SET @VersionId = '{version_id}';
    INSERT INTO [scores].[MixVersion] ([Id], [MixId], [Name], [ReleaseDate], [SortOrder])
    SELECT @VersionId, @Phoenix2, N'{version}', '{release}', ISNULL(MAX([SortOrder]), 0) + 10 FROM [scores].[MixVersion] WHERE [MixId] = @Phoenix2;
    PRINT 'Created Phoenix 2 version {version} ({release}).';
END
""")

    def fmt(x):
        return f'{x:.2f}'.rstrip('0').rstrip('.')

    report_rows = []
    for s in songs:
        en = s['name']
        w(f"-- ===================================================================== {en}")
        w(f"SET @SongId = '{s['id']}';")
        w(f"SET @En = {q(en)};")
        w("IF NOT EXISTS (SELECT 1 FROM [scores].[Song] WHERE [Id] = @SongId AND [Name] = @En)")
        w("    BEGIN SET @msg = CONCAT(N'The RISE song row for ', @En, N' is missing or renamed - nothing written'); THROW 50001, @msg, 1; END")
        w("IF EXISTS (SELECT 1 FROM [scores].[Song] s JOIN [scores].[Chart] c ON c.[SongId] = s.[Id] JOIN [scores].[ChartMix] cm ON cm.[ChartId] = c.[Id] AND cm.[MixId] = @Phoenix2 WHERE s.[Name] = @En AND s.[Id] <> @SongId)")
        w("    BEGIN SET @msg = CONCAT(N'A second song named ', @En, N' already has Phoenix 2 charts (uploaded through BulkAddCharts?) - resolve that first'); THROW 50002, @msg, 1; END")
        w(f"UPDATE [scores].[Song] SET [Duration] = {s['duration'] * 10_000_000} WHERE [Id] = @SongId AND [Duration] = 0;  -- {s['duration']}s")
        w(f"UPDATE [scores].[Song] SET [MinBpm] = {fmt(s['min'])}, [MaxBpm] = {fmt(s['max'])} WHERE [Id] = @SongId AND [MinBpm] IS NULL AND [MaxBpm] IS NULL;")
        for arcade, rise in s['pairs']:
            c = s['charts'][('Single', arcade)]
            cid = catalog_guid('chart', norm(en), 'Single', rise)
            w(f"-- S{arcade}: the RISE single at level {rise}")
            w(f"SET @ChartId = '{cid}';")
            w(f"IF NOT EXISTS (SELECT 1 FROM [scores].[Chart] c JOIN [scores].[ChartMix] cm ON cm.[ChartId] = c.[Id] AND cm.[MixId] = @Rise WHERE c.[Id] = @ChartId AND c.[SongId] = @SongId AND c.[Type] = 'Single' AND cm.[Level] = {rise})")
            w(f"    BEGIN SET @msg = CONCAT(@En, N' S{arcade}: the RISE single at level {rise} is not where the RISE catalog put it'); THROW 50003, @msg, 1; END")
            w(f"UPDATE [scores].[Chart] SET [StepArtist] = {q(c['step'])} WHERE [Id] = @ChartId AND [StepArtist] IS NULL;")
            w("IF NOT EXISTS (SELECT 1 FROM [scores].[ChartMix] WHERE [ChartId] = @ChartId AND [MixId] = @Phoenix2)")
            w(f"    INSERT INTO [scores].[ChartMix] ([Id], [ChartId], [MixId], [Level], [NoteCount], [AddedInVersionId]) VALUES ('{own_guid('chartmix', 'Phoenix2', cid)}', @ChartId, @Phoenix2, {arcade}, NULL, @VersionId);")
            w("IF NOT EXISTS (SELECT 1 FROM [scores].[ChartVideo] WHERE [ChartId] = @ChartId)")
            w(f"    INSERT INTO [scores].[ChartVideo] ([ChartId], [VideoUrl], [ChannelName]) VALUES (@ChartId, N'https://www.youtube.com/embed/{c['video']}', @Official);")
            report_rows.append((en, f'S{arcade}', arcade, rise, 'join RISE chart', cid, c['video'], c['step']))
        for t, lvl in s['new']:
            c = s['charts'][(t, lvl)]
            cid = own_guid('chart', norm(en), t, lvl)
            players = lvl if t == 'CoOp' else 1
            w(f"-- {label(t, lvl)}: a new chart, debuting in Phoenix 2")
            w(f"SET @ChartId = '{cid}';")
            w("IF NOT EXISTS (SELECT 1 FROM [scores].[Chart] WHERE [Id] = @ChartId)")
            w("BEGIN")
            w(f"    IF EXISTS (SELECT 1 FROM [scores].[Chart] c JOIN [scores].[ChartMix] cm ON cm.[ChartId] = c.[Id] AND cm.[MixId] = @Phoenix2 WHERE c.[SongId] = @SongId AND c.[Type] = '{t}' AND cm.[Level] = {lvl})")
            w(f"        BEGIN SET @msg = CONCAT(@En, N' {label(t, lvl)} already has a Phoenix 2 chart'); THROW 50004, @msg, 1; END")
            w(f"    INSERT INTO [scores].[Chart] ([Id], [SongId], [Level], [Type], [StepArtist], [OriginalMixId], [PlayerCount]) VALUES (@ChartId, @SongId, {lvl}, '{t}', {q(c['step'])}, @Phoenix2, {players});")
            w("END")
            w("IF NOT EXISTS (SELECT 1 FROM [scores].[ChartMix] WHERE [ChartId] = @ChartId AND [MixId] = @Phoenix2)")
            w(f"    INSERT INTO [scores].[ChartMix] ([Id], [ChartId], [MixId], [Level], [NoteCount], [AddedInVersionId]) VALUES ('{own_guid('chartmix', 'Phoenix2', cid)}', @ChartId, @Phoenix2, {lvl}, NULL, @VersionId);")
            w("IF NOT EXISTS (SELECT 1 FROM [scores].[ChartVideo] WHERE [ChartId] = @ChartId)")
            w(f"    INSERT INTO [scores].[ChartVideo] ([ChartId], [VideoUrl], [ChannelName]) VALUES (@ChartId, N'https://www.youtube.com/embed/{c['video']}', @Official);")
            report_rows.append((en, label(t, lvl), lvl, '', 'new chart', cid, c['video'], c['step']))
        w(f"SET @Ko = {q(s['ko'])};")
        w("IF EXISTS (SELECT 1 FROM [scores].[SongNameLanguage] WHERE [CultureCode] = 'ko-KR' AND [SongName] = @Ko AND [EnglishSongName] <> @En)")
        w("    BEGIN SET @msg = CONCAT(N'The Korean title ', @Ko, N' already belongs to another song'); THROW 50005, @msg, 1; END")
        w("IF EXISTS (SELECT 1 FROM [scores].[SongNameLanguage] WHERE [CultureCode] = 'ko-KR' AND [EnglishSongName] = @En)")
        w("    UPDATE [scores].[SongNameLanguage] SET [SongName] = @Ko WHERE [CultureCode] = 'ko-KR' AND [EnglishSongName] = @En AND [SongName] <> @Ko;")
        w("ELSE")
        w("    INSERT INTO [scores].[SongNameLanguage] ([EnglishSongName], [CultureCode], [SongName]) VALUES (@En, 'ko-KR', @Ko);")
        w("")

    w("-- ===================================================================== video sides (two singles on one video: lower level on the left)")
    for s in songs:
        by_video = {}
        for (t, lvl), c in s['charts'].items():
            if t == 'Single':
                by_video.setdefault(c['video'], []).append(lvl)
        for vid, lv in sorted(by_video.items(), key=lambda kv: min(kv[1])):
            if len(lv) != 2:
                continue
            lo, hi = sorted(lv)
            url = f'https://www.youtube.com/embed/{vid}'
            w(f"UPDATE v SET [Side] = CASE WHEN cm.[Level] = {lo} THEN 'Left' ELSE 'Right' END FROM [scores].[ChartVideo] v JOIN [scores].[ChartMix] cm ON cm.[ChartId] = v.[ChartId] AND cm.[MixId] = @Phoenix2 WHERE v.[VideoUrl] = N'{url}' AND v.[Side] IS NULL AND cm.[Level] IN ({lo}, {hi}) AND (SELECT COUNT(*) FROM [scores].[ChartVideo] x WHERE x.[VideoUrl] = N'{url}') = 2;  -- {s['name']} S{lo}/S{hi}")
    w("")

    kos = ', '.join(q(s['ko']) for s in songs)
    ens = ', '.join(q(s['name']) for s in songs)
    w(f"""-- ===================================================================== closing checks
SET @n = (SELECT COUNT(*) FROM [scores].[ChartMix] cm JOIN [scores].[Chart] c ON c.[Id] = cm.[ChartId]
          WHERE cm.[MixId] = @Phoenix2 AND c.[SongId] IN ({song_ids}));
IF @n <> {total} BEGIN SET @msg = CONCAT(N'Expected {total} Phoenix 2 charts on the {len(songs)} songs, found ', @n); THROW 50010, @msg, 1; END
SET @n = (SELECT COUNT(*) FROM [scores].[ChartMix] cm JOIN [scores].[Chart] c ON c.[Id] = cm.[ChartId]
          LEFT JOIN [scores].[ChartVideo] v ON v.[ChartId] = c.[Id]
          WHERE cm.[MixId] = @Phoenix2 AND c.[SongId] IN ({song_ids}) AND v.[ChartId] IS NULL);
IF @n <> 0 BEGIN SET @msg = CONCAT(@n, N' of the Phoenix 2 charts have no video'); THROW 50011, @msg, 1; END
SET @n = (SELECT COUNT(*) FROM (SELECT c.[SongId], c.[Type], cm.[Level] FROM [scores].[ChartMix] cm JOIN [scores].[Chart] c ON c.[Id] = cm.[ChartId]
          WHERE cm.[MixId] = @Phoenix2 AND c.[SongId] IN ({song_ids}) GROUP BY c.[SongId], c.[Type], cm.[Level] HAVING COUNT(*) > 1) d);
IF @n <> 0 BEGIN SET @msg = CONCAT(@n, N' song/type/level combinations hold two Phoenix 2 charts'); THROW 50012, @msg, 1; END
SET @n = (SELECT COUNT(*) FROM (SELECT [SongName] FROM [scores].[SongNameLanguage] WHERE [CultureCode] = 'ko-KR' AND [SongName] IN ({kos})
          GROUP BY [SongName] HAVING COUNT(*) > 1) d);
IF @n <> 0 BEGIN SET @msg = CONCAT(@n, N' Korean titles appear twice'); THROW 50013, @msg, 1; END
SET @n = (SELECT COUNT(*) FROM (SELECT [EnglishSongName] FROM [scores].[SongNameLanguage] WHERE [CultureCode] = 'ko-KR' AND [EnglishSongName] IN ({ens})
          GROUP BY [EnglishSongName] HAVING COUNT(*) <> 1) d);
IF @n <> 0 BEGIN SET @msg = CONCAT(@n, N' of the {len(songs)} songs do not have exactly one ko-KR row'); THROW 50014, @msg, 1; END
SET @n = (SELECT COUNT(DISTINCT [EnglishSongName]) FROM [scores].[SongNameLanguage] WHERE [CultureCode] = 'ko-KR' AND [EnglishSongName] IN ({ens}));
IF @n <> {len(songs)} BEGIN SET @msg = CONCAT(N'Expected a ko-KR row on each of the {len(songs)} songs, found ', @n); THROW 50015, @msg, 1; END

-- what the batch looks like now
SELECT s.[Name] AS Song, k.[SongName] AS Korean, c.[Type], cm.[Level] AS Phoenix2Level, rm.[Level] AS RiseLevel,
       CASE WHEN c.[OriginalMixId] = @Phoenix2 THEN 'new' ELSE 'RISE chart' END AS Chart, c.[StepArtist], v.[VideoUrl], v.[Side], mv.[Name] AS AddedIn
FROM [scores].[ChartMix] cm
JOIN [scores].[Chart] c ON c.[Id] = cm.[ChartId]
JOIN [scores].[Song] s ON s.[Id] = c.[SongId]
LEFT JOIN [scores].[ChartMix] rm ON rm.[ChartId] = c.[Id] AND rm.[MixId] = @Rise
LEFT JOIN [scores].[ChartVideo] v ON v.[ChartId] = c.[Id]
LEFT JOIN [scores].[MixVersion] mv ON mv.[Id] = cm.[AddedInVersionId]
LEFT JOIN [scores].[SongNameLanguage] k ON k.[EnglishSongName] = s.[Name] AND k.[CultureCode] = 'ko-KR'
WHERE cm.[MixId] = @Phoenix2 AND c.[SongId] IN ({song_ids})
ORDER BY s.[Name], CASE c.[Type] WHEN 'Single' THEN 0 WHEN 'Double' THEN 1 ELSE 2 END, cm.[Level];

SELECT [Name], [Artist], [Duration] / 10000000 AS DurationSeconds, [MinBpm], [MaxBpm] FROM [scores].[Song] WHERE [Id] IN ({song_ids}) ORDER BY [Name];

IF @WhatIf = 1
BEGIN
    ROLLBACK;
    PRINT 'WhatIf: everything above ran and was rolled back. Set @WhatIf = 0 to commit.';
END
ELSE
BEGIN
    COMMIT;
    PRINT 'Committed. Restart the app so the Korean names and the RISE chart dictionaries reload.';
END
""")

    with open(out_path, 'w', encoding='utf-8-sig', newline='\r\n') as fh:
        fh.write('\n'.join(L))
    if appendix_path:
        with open(appendix_path, 'w', encoding='utf-8') as fh:
            fh.write('song\tchart\tPhoenix 2 level\tRISE level\taction\tchart id\tvideo\tstep artist\n')
            for r in report_rows:
                fh.write('\t'.join(str(x) for x in r) + '\n')

    # keep the songs out of build-batch's JSON while its drift check keeps watching them
    shipped_path = os.path.join(STATE, 'shipped.json')
    shipped = json.load(open(shipped_path, encoding='utf-8-sig')) if os.path.exists(shipped_path) else {'songs': []}
    have = {s['name'] for s in shipped['songs']}
    now = datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
    added = 0
    for s in songs:
        if s['name'] not in have:
            shipped['songs'].append({'name': s['name'], 'charts': sorted(f'{t}|{lvl}' for t, lvl in s['charts']),
                                     'batch': os.path.basename(out_path), 'dateUtc': now})
            added += 1
    if added:
        with open(shipped_path, 'w', encoding='utf-8') as fh:
            json.dump(shipped, fh, ensure_ascii=False, indent=4)

    for s in songs:
        re_rates = [f'S{a} (RISE {r})' for a, r in s['pairs'] if a != r]
        print(f"{s['name']} [{s['ko']}] {s['min']:g}-{s['max']:g} BPM, {s['duration']}s ({s['duration_from']})")
        print(f"   joins RISE charts: {', '.join('S%d' % a for a, _ in s['pairs'])}" + (f"   re-rated: {', '.join(re_rates)}" if re_rates else ''))
        print(f"   new: {', '.join(label(t, lvl) for t, lvl in s['new'])}")
    print(f'{len(songs)} songs, {total} Phoenix 2 charts ({sum(len(s["pairs"]) for s in songs)} join RISE singles, '
          f'{sum(len(s["new"]) for s in songs)} new) -> {out_path}; shipped.json +{added}')


if __name__ == '__main__':
    main()

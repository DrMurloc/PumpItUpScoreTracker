"""check-new-charts: the Phoenix 2 channel (scores.SongMix) of the songs a batch shipped without one.

A song's channel comes from the patch's update notice on piugame.com first (fetch-notice.py: the Category it
prints, the game's own word, there on release day), else from the per-version step-chart playlists its chart
videos sit in (fetch-channels.ps1, consensus across its videos; a split vote stops the run). The playlists lag the
patch by weeks and file what they cannot name under (Original): the Steam-link songs sit in the arcade's
"R!SE Vol.1" channel, which PIU Scores has no value for, so where the notice says R!SE Vol.1 the song is left out
and listed - never taken from the playlist. The script writes a row only where the song is on Phoenix 2 and has
no Phoenix 2 channel yet, lists everything else, and stops if a name resolves to two songs with Phoenix 2 charts.

    python -I channels-sql.py <batch file name, or part of it, as shipped.json records it> <version> <out.sql>

e.g. channels-sql.py 2026-10-15 1.02.0 Downloads/phoenix2-1.02.0-channels-<date>.sql covers both 10-15 files.
"""
import json, os, re, sys, collections, unicodedata
sys.stdout.reconfigure(encoding='utf-8')
STATE = os.path.join(os.environ['USERPROFILE'], '.piu-score-tracker', 'check-new-charts')
BATCH_FILTER, VERSION, OUT = sys.argv[1:4]
VERSION = VERSION.lstrip('vV')
P2 = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
# spellings a song may carry in the tracker besides the shipped name (Reincarnate's rename was optional)
ALT = {'Reincarnate in “A”': 'Reincarnate in "A"'}


def norm(s):
    s = unicodedata.normalize('NFKC', str(s)).lower()
    return ''.join(c for c in s if c.isalnum())


ch = json.load(open(os.path.join(STATE, 'channels.json'), encoding='utf-8-sig')) if os.path.exists(os.path.join(STATE, 'channels.json')) else {}
vids = {}
for f in os.listdir(os.path.join(STATE, 'videos')):
    v = json.load(open(os.path.join(STATE, 'videos', f), encoding='utf-8-sig'))
    vids[v['videoId']] = v
shipped = [s for s in json.load(open(os.path.join(STATE, 'shipped.json'), encoding='utf-8-sig'))['songs'] if BATCH_FILTER in s['batch']]
notice = {}
for f in sorted(os.listdir(STATE)):
    if re.fullmatch(r'notice-.+\.json', f):
        n = json.load(open(os.path.join(STATE, f), encoding='utf-8-sig'))
        for s in n['songs']:
            notice[norm(s['name'])] = {**s, 'version': n['version']}


def title_field(d):
    m = re.search(r'(?m)^\s*Title\s*:\s*(.+?)\s*$', d or '')
    return m.group(1) if m else ''


def same_song(name, t):
    """The shipped name against a video's Title field: plain, Korean-annotated mid-title, or Korean-first."""
    n = name.replace('’', "'").upper()
    t = t.replace('’', "'")
    if t.upper() == n or t.upper().startswith(n + ' '):
        return True
    stripped = re.sub(r'\s*\([^()]*[가-힣][^()]*\)', ' ', t)
    if re.sub(r'\s+', ' ', stripped).strip().upper().replace('(', '').replace(')', '') == n.replace('(', '').replace(')', ''):
        return True
    m = re.match(r'^[^()]*[가-힣][^()]*?\s*\(([^()]+)\)\s*$', t)
    return bool(m and m.group(1).strip().upper() == n)


rows, left_out = [], []
for s in shipped:
    votes = collections.Counter()
    for vid, chan in ch.items():
        v = vids.get(vid)
        if v and '[PUMP IT UP - PHOENIX 2]' in v['title'] and same_song(s['name'], title_field(v.get('description'))):
            votes[chan] += 1
    n = notice.get(norm(s['name']))
    if n:
        if n['channel'] is None:
            left_out.append((s['name'], f'the v{n["version"]} notice files it under {n["category"]}, which PIU Scores has no channel for'))
            print(f'NOT A CHANNEL  {s["name"]} - {n["category"]} (v{n["version"]} notice) - left out')
            continue
        chan, source = n['channel'], f'v{n["version"]} notice'
        if votes and set(votes) != {chan}:
            print(f'FLAG  {s["name"]}: the playlists say {dict(votes)}, the notice says {chan} - taking the notice')
    elif votes:
        if len(votes) > 1:
            raise SystemExit(f'{s["name"]}: its videos sit in more than one channel playlist {dict(votes)} - decide by hand')
        chan, nv = votes.most_common(1)[0]
        source = f'playlist ({nv} videos)'
    else:
        left_out.append((s['name'], 'neither the update notice nor a step-chart playlist names it yet'))
        print(f'NO SOURCE  {s["name"]} - left out')
        continue
    rows.append((s['name'], ALT.get(s['name']), chan, source))
    print(f'{s["name"]:<36} {chan:<11} {source}')

if not rows:
    raise SystemExit('no song has a channel PIU Scores can write - nothing to emit')


def q(x):
    return 'NULL' if x is None else "N'" + x.replace("'", "''") + "'"


sources = sorted({r[3].split(' (')[0] for r in rows})
values = ',\n    '.join(f'({q(a)}, {q(b)}, {q(c)})' for a, b, c, _ in rows)
outs = ''.join(f'\n    {name}: {why}' for name, why in left_out)
left = f'\n\n  Left out - no row written, the song keeps no Phoenix 2 channel:{outs}' if left_out else ''
sql = f"""/*
  Phoenix 2 v{VERSION}: the channel of the {len(rows)} songs that shipped without one.

  Each song's channel is the Category the patch's update notice prints for it (piugame.com, posted on
  release day), else the [PIU PHOENIX 2] v{VERSION} step-chart playlist its chart videos sit in
  (docs/design/song-channels.md §6). Taken from: {', '.join(sources)}.{left}

  Writes one scores.SongMix row (SongId, Phoenix 2, Channel) per song that is on Phoenix 2 and has no
  Phoenix 2 channel yet. A song that is not on Phoenix 2 (its batch never ran) is skipped and listed; a song
  that already has a channel keeps it and is listed. Idempotent; @WhatIf = 1 shows the result and rolls back.
  Then Admin -> Clear Cache (the per-mix chart dictionaries cache for fourteen days).
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @WhatIf bit = 1;             -- 1 = run everything, show the result, roll back. 0 = commit.
DECLARE @Phoenix2 uniqueidentifier = '{P2}';
DECLARE @n int, @msg nvarchar(2000);
DECLARE @Channels TABLE ([Name] nvarchar(128) NOT NULL, [AltName] nvarchar(128) NULL, [Channel] nvarchar(16) NOT NULL);
INSERT INTO @Channels ([Name], [AltName], [Channel]) VALUES
    {values};

BEGIN TRAN;

-- the songs as the tracker has them on Phoenix 2 (by name; a RISE song and a second copy share one)
DECLARE @Songs TABLE ([Name] nvarchar(128) NOT NULL, [SongId] uniqueidentifier NOT NULL, [Channel] nvarchar(16) NOT NULL);
INSERT INTO @Songs ([Name], [SongId], [Channel])
SELECT ch.[Name], s.[Id], ch.[Channel]
FROM @Channels ch
JOIN [scores].[Song] s ON s.[Name] = ch.[Name] OR s.[Name] = ch.[AltName]
WHERE EXISTS (SELECT 1 FROM [scores].[Chart] c JOIN [scores].[ChartMix] cm ON cm.[ChartId] = c.[Id] AND cm.[MixId] = @Phoenix2 WHERE c.[SongId] = s.[Id]);

SET @n = (SELECT COUNT(*) FROM (SELECT [Name] FROM @Songs GROUP BY [Name] HAVING COUNT(*) > 1) d);
IF @n <> 0 BEGIN SET @msg = CONCAT(@n, N' of these names belong to two songs with Phoenix 2 charts - resolve the copies first'); THROW 50001, @msg, 1; END

INSERT INTO [scores].[SongMix] ([SongId], [MixId], [Channel])
SELECT x.[SongId], @Phoenix2, x.[Channel]
FROM @Songs x
WHERE NOT EXISTS (SELECT 1 FROM [scores].[SongMix] sm WHERE sm.[SongId] = x.[SongId] AND sm.[MixId] = @Phoenix2);
PRINT CONCAT(@@ROWCOUNT, ' channel rows written.');

SELECT ch.[Name], ch.[Channel] AS Expected, sm.[Channel] AS Phoenix2Channel,
       CASE WHEN x.[SongId] IS NULL THEN 'not on Phoenix 2 - skipped'
            WHEN sm.[Channel] = ch.[Channel] THEN 'ok'
            ELSE 'kept its own channel' END AS State
FROM @Channels ch
LEFT JOIN @Songs x ON x.[Name] = ch.[Name]
LEFT JOIN [scores].[SongMix] sm ON sm.[SongId] = x.[SongId] AND sm.[MixId] = @Phoenix2
ORDER BY State, ch.[Name];

IF @WhatIf = 1
BEGIN
    ROLLBACK;
    PRINT 'WhatIf: everything above ran and was rolled back. Set @WhatIf = 0 to commit.';
END
ELSE
BEGIN
    COMMIT;
    PRINT 'Committed. Admin -> Clear Cache.';
END
"""
with open(OUT, 'w', encoding='utf-8-sig', newline='\r\n') as fh:
    fh.write(sql)
print(f'{len(rows)} songs -> {OUT}' + (f'; {len(left_out)} left out' if left_out else ''))

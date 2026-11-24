"""check-new-charts step 2c: the patch's update notice on piugame.com - each new song's channel, on release day.

Andamiro posts "PIU PHOENIX 2 v1.03.0 update Notice" on piugame.com the day a patch ships, and it prints every
new song's Category - the channel the song sits in on the cab (ORIGINAL, K-POP, WORLD MUSIC, XROSS) - beside
its artist, BPM and step-chart list. The per-version YouTube playlists that fetch-channels.ps1 reads say the same
thing weeks later, and misfile what they cannot name: a Steam-link song sits in the arcade's "R!SE Vol.1"
channel, which has no playlist, so its videos land in the (Original) one. The notice wins wherever it speaks
(build-batch.ps1, channels-sql.py).

Writes notice-<version>.json in the state dir:
    {"version": "1.03.0", "title": ..., "url": ..., "date": "2026-11-24",
     "songs": [{"name": "I DONT CARE", "category": "K-POP", "channel": "KPop", "section": "new",
                "artist": ..., "bpm": ..., "charts": ["Single|2", ..., "CoOp|2"]},
               {"name": "untitled", "category": "R!SE Vol.1", "channel": null, "section": "steam-link-arcade"}]}
"channel" is the enum name the blob takes, or null for a channel PIU Scores does not have (R!SE Vol.1). The
notice's "Rise Charts Added ... RISE MIX" section is the arcade's side of the Steam link - RISE's own charts,
which Phoenix 2 does not carry (docs/design/rise.md D8) - and is not read.

Prints each song with its channel, and checks the notice's step-chart list against shipped.json where the song
has shipped: the notice is Andamiro's own list of the patch's charts.

    python -I fetch-notice.py 1.03.0
"""
import html, http.cookiejar, json, os, re, sys, unicodedata, urllib.request

sys.stdout.reconfigure(encoding='utf-8')
STATE = os.path.join(os.environ['USERPROFILE'], '.piu-score-tracker', 'check-new-charts')
SITE = 'https://piugame.com'
UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36'
# the Category as the notice prints it -> the enum name (docs/design/song-channels.md D5)
TOKENS = {'ORIGINAL': 'Original', 'K-POP': 'KPop', 'KPOP': 'KPop', 'WORLD MUSIC': 'WorldMusic',
          'WORLDMUSIC': 'WorldMusic', 'J-MUSIC': 'JMusic', 'JMUSIC': 'JMusic', 'XROSS': 'Xross'}


def norm(s):
    s = unicodedata.normalize('NFKC', str(s)).lower()
    return ''.join(c for c in s if c.isalnum())


# piugame bounces a cookieless visitor through its SSO handshake (/ssoc -> am-pass and back) before it serves
# a page; a cookie jar is all the round trip needs - the notices are public
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
opener.addheaders = [('User-Agent', UA), ('Accept-Language', 'en-US,en;q=0.9')]


def get(url):
    with opener.open(url, timeout=30) as r:
        return r.read().decode('utf-8', errors='replace')


def text_lines(page):
    """The post body as text lines: gnuboard puts the post between the view header and the button row."""
    a = page.find('board_st board_view_top')
    b = page.find('board_btn_wrap', a)
    if a < 0 or b < 0:
        raise SystemExit('the notice page has no post body where it used to - piugame markup changed?')
    body = re.sub(r'(?is)<script.*?</script>', ' ', page[a:b])
    body = re.sub(r'(?i)<(br|/p|/div|/li|/tr|/td|/th|/h\d)[^>]*>', '\n', body)
    body = html.unescape(re.sub(r'<[^>]+>', ' ', body))
    lines = (re.sub(r'[ \t ]+', ' ', l).strip() for l in body.split('\n'))
    return [l for l in lines if l]


def chart_keys(spec):
    """'S2, S4, D6, CO-OP (X2)' / 'D22,CO-OPx2' -> ['Single|2', 'Single|4', 'Double|6', 'CoOp|2']"""
    keys = []
    for tok in re.split(r'\s*,\s*', spec.strip()):
        m = re.fullmatch(r'(?i)(S|D)\s*(\d{1,2})', tok)
        c = re.fullmatch(r'(?i)CO-?OP\s*\(?\s*X?\s*(\d)\s*\)?', tok)
        if m:
            keys.append(('Single' if m.group(1).upper() == 'S' else 'Double') + f'|{int(m.group(2))}')
        elif c:
            keys.append(f'CoOp|{int(c.group(1))}')
        elif tok:
            raise SystemExit(f'unparsed step-chart token {tok!r} in {spec!r}')
    return keys


def clean_title(t):
    # "::POINT_NEMO:: (*Premium Mode Exclusive)", "PHYSALIS - (*Premium Mode Exclusive)"
    return re.sub(r'\s*-?\s*\(\*[^)]*\)\s*$', '', t).strip()


def main():
    version = sys.argv[1].lstrip('vV')
    listing = get(f'{SITE}/phoenix2_notice?sca=update')
    found = None
    for m in re.finditer(r'wr_id=(\d+)[^"]*"[^>]*>\s*([^<]*?)\s*<', listing):
        if re.search(r'(?i)\bv' + re.escape(version) + r'\b', m.group(2)) and 'notice' in m.group(2).lower():
            found = (m.group(1), html.unescape(m.group(2)))
            break
    if not found:
        raise SystemExit(f'no "v{version} update Notice" on {SITE}/phoenix2_notice?sca=update yet')
    url = f'{SITE}/phoenix2_notice?sca=update&wr_id={found[0]}'
    lines = text_lines(get(url))
    date = next((m.group(1) for l in lines for m in [re.match(r'(?i)Date Created\s*:\s*(\d{4}-\d{2}-\d{2})', l)] if m), None)

    songs, section, current, steam_channel = [], None, None, None
    for l in lines:
        h = re.match(r'^(\d+)\.\s*(.+)$', l)
        if h:
            head = h.group(2)
            if re.search(r'(?i)^New Songs', head):
                section = 'new'
            elif re.search(r'(?i)Arcade Charts Added', head):
                section = 'steam-link-arcade'
                q = re.search(r"['‘’]([^'‘’]+)['‘’]", head)
                steam_channel = q.group(1) if q else None
            else:
                section = None      # fixes, UCS, and the RISE MIX section (D8: not Phoenix 2's charts)
            current = None
            continue
        if section == 'new':
            t = re.match(r'^\d+\)\s*Title\s*:\s*(.+)$', l)
            if t:
                current = {'name': clean_title(t.group(1)), 'section': 'new'}
                songs.append(current)
                continue
            f = re.match(r'^(Artist|BPM|Category|Step charts)\s*:\s*(.+)$', l, re.I)
            if f and current is not None:
                key = f.group(1).lower()
                if key == 'step charts':
                    current['charts'] = chart_keys(f.group(2))
                else:
                    current[key] = f.group(2).strip()
        elif section == 'steam-link-arcade':
            c = re.search(r'(?i)added to the (.+?) channel in ARCADE MIX', l)
            if c:
                steam_channel = c.group(1).strip()
                continue
            d = re.match(r'^-\s*(.+)$', l)
            if d:
                songs.append({'name': clean_title(d.group(1)), 'section': 'steam-link-arcade', 'category': steam_channel})
    if not songs:
        raise SystemExit(f'{url}: no new songs read - the notice format changed?')

    for s in songs:
        cat = (s.get('category') or '').strip()
        if not cat:
            raise SystemExit(f'{s["name"]}: the notice gives it no Category')
        s['channel'] = TOKENS.get(re.sub(r'\s+', ' ', cat).upper())

    out = {'version': version, 'title': found[1], 'url': url, 'date': date, 'songs': songs}
    path = os.path.join(STATE, f'notice-{version}.json')
    with open(path, 'w', encoding='utf-8') as fh:
        json.dump(out, fh, ensure_ascii=False, indent=4)

    shipped_path = os.path.join(STATE, 'shipped.json')
    shipped = {norm(s['name']): s for s in json.load(open(shipped_path, encoding='utf-8-sig'))['songs']} if os.path.exists(shipped_path) else {}
    print(f'{found[1]} ({date}) {url}')
    for s in songs:
        chan = s['channel'] or f'no channel - "{s["category"]}" is not one PIU Scores has'
        check = ''
        sh = shipped.get(norm(s['name']))
        if sh and s.get('charts'):
            want, have = set(s['charts']), set(sh['charts'])
            check = '   charts match the shipped batch' if want == have else \
                f'   *** CHARTS DIFFER from {sh["batch"]}: notice only {sorted(want - have)}, batch only {sorted(have - want)}'
        elif not sh:
            check = '   (not shipped yet)'
        print(f'  {s["name"]:<36} {chan}{check}')
    print(f'{len(songs)} songs -> {path}')


if __name__ == '__main__':
    main()

"""Pull every video (id + title) of each playlist in inventory.tsv via YouTube's own browse call.
usage: python crawl-playlists.py inventory.tsv <clientVersion> <apiKey> outdir
inventory.tsv rows: playlistId <tab> count <tab> title"""
import json, sys, time, urllib.request, os, re
inv, cv, key, outdir = sys.argv[1:5]
os.makedirs(outdir, exist_ok=True)
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36"
def browse(body):
    payload = {"context": {"client": {"clientName": "WEB", "clientVersion": cv, "hl": "en", "gl": "US"}}}
    payload.update(body)
    req = urllib.request.Request(f"https://www.youtube.com/youtubei/v1/browse?key={key}&prettyPrint=false",
        data=json.dumps(payload).encode(), method="POST",
        headers={"Content-Type": "application/json", "X-Youtube-Client-Name": "1", "X-Youtube-Client-Version": cv, "User-Agent": UA, "Accept-Language": "en-US,en;q=0.9"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())
def walk(o, vids, toks, d=0):
    if o is None or d > 60: return
    if isinstance(o, list):
        for x in o: walk(x, vids, toks, d+1)
        return
    if not isinstance(o, dict): return
    if o.get("contentId") and o.get("contentType") == "LOCKUP_CONTENT_TYPE_VIDEO":
        t = (((o.get("metadata") or {}).get("lockupMetadataViewModel") or {}).get("title") or {}).get("content", "")
        vids.append((o["contentId"], t)); return
    if "playlistVideoRenderer" in o:
        r = o["playlistVideoRenderer"]; t = "".join(x.get("text","") for x in (r.get("title") or {}).get("runs", []))
        vids.append((r.get("videoId"), t)); return
    cc = o.get("continuationCommand")
    if isinstance(cc, dict) and cc.get("token"):
        toks.append(cc["token"]); return
    for v in o.values(): walk(v, vids, toks, d+1)
rows = [l.rstrip("\n").split("\t") for l in open(inv, encoding="utf-8") if l.strip()]
summary = []
for pid, count, title in rows:
    out = os.path.join(outdir, pid + ".json")
    if os.path.exists(out):
        d = json.load(open(out, encoding="utf-8")); summary.append((pid, title, count, len(d["videos"]), "cached")); continue
    vids, toks = [], []
    j = browse({"browseId": "VL" + pid}); walk(j, vids, toks)
    seen_t = set(); rounds = 0
    while toks and rounds < 30:
        t = toks.pop(0)
        if t in seen_t: continue
        seen_t.add(t); more, nt = [], []
        j = browse({"continuation": t}); walk(j, more, nt)
        vids += more; toks += nt; rounds += 1; time.sleep(0.4)
    seen = set(); uniq = []
    for v in vids:
        if v[0] and v[0] not in seen: seen.add(v[0]); uniq.append({"videoId": v[0], "title": v[1]})
    json.dump({"playlistId": pid, "title": title, "declaredCount": count, "videos": uniq}, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    summary.append((pid, title, count, len(uniq), "fetched")); time.sleep(0.6)
    print(f"{title}: declared {count}, got {len(uniq)}", flush=True)
bad = [s for s in summary if str(s[2]) != str(s[3])]
print(f"\n{len(summary)} playlists, {sum(s[3] for s in summary)} videos; count mismatches: {len(bad)}")
for s in bad: print("  MISMATCH", s)

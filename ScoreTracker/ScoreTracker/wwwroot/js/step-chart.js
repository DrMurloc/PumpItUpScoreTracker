// The step-chart strip (docs/design/step-chart-failure-map.md D12): fetches the banked
// timeline and draws it on canvas tiles — panel-colored arrows, hold bodies, the mm:ss ruler,
// the snapshot's own segment boundaries, a whole-chart minimap that doubles as navigation, and
// quick-link chips. Colors come from the token sheet via getComputedStyle, never literals; a
// chart the server didn't render a section for never reaches this file, and a fetch that
// misses hides the section rather than erroring. The static chart page mounts every
// [data-stepchart] on load; the dialog's Steps tab imports { mount } lazily.

document.querySelectorAll('[data-stepchart]').forEach(function (el) { mount(el); });

export async function mount(root) {
    if (!root || root.dataset.scMounted) return;
    root.dataset.scMounted = '1';

    var chartId = root.getAttribute('data-chart-id');
    var mix = root.getAttribute('data-mix');
    var isPhoenix2 = mix === 'Phoenix2';
    var compact = root.getAttribute('data-compact') === '1';
    if (!chartId || !mix) return;

    var strings = readStrings(root);
    var payload = await fetchJson('/Charts/StepChart/' + chartId + '?mix=' + encodeURIComponent(mix));
    // An all-hold chart is rows: [] with a real holds list — it still draws (bank sweep
    // 2026-08-31 found three). Only a chart with nothing at all hides its section.
    if (!payload || !payload.rows ||
        (payload.rows.length === 0 && (!payload.holds || payload.holds.length === 0))) {
        var section = root.closest('section') || root;
        section.hidden = true;
        return;
    }

    var css = getComputedStyle(document.documentElement);
    var token = function (name) { return css.getPropertyValue(name).trim() || '#888'; };
    var COL = {
        upper: token('--panel-upper'), lower: token('--panel-lower'), center: token('--panel-center'),
        footL: token('--foot-l'), footR: token('--foot-r'),
        quant: { 4: token('--quant-4'), 8: token('--quant-8'), 12: token('--quant-12'), 16: token('--quant-16') },
        quantOther: token('--quant-other'),
        ink: token('--mix-ink'), inkMuted: token('--mix-ink-muted'), accent: token('--mix-primary')
    };

    // rows: [time, panelMask, leftMask, quant, beat|null]
    var rows = payload.rows.map(function (r) { return { t: r[0], m: r[1], l: r[2], q: r[3], b: r[4] }; });
    var holds = payload.holds.map(function (h) { return { p: h[0], s: h[1], e: h[2], left: h[3] === 1 }; });
    // segments: [start, end, enps|null, level|null, badge display names, pace code|null] —
    // annotations null on payloads banked before the section-labeling rounds.
    var segments = payload.segments.map(function (s) {
        return {
            s: s[0], e: s[1], enps: s[2], lvl: s[3] != null ? s[3] : null,
            badges: s[4] || [], pace: s[5] || null
        };
    });
    var ranges = payload.ranges.map(function (r) { return { s: r[0], e: r[1] }; });
    var panels = payload.panels;
    var lastNote = Math.max(
        rows.length ? rows[rows.length - 1].t : 0,
        holds.reduce(function (max, h) { return Math.max(max, h.e); }, 0));
    var duration = lastNote + 2;

    var level = parseInt(root.getAttribute('data-level') || '0', 10);

    var box = root.querySelector('[data-stepchart-scroll]');
    if (!box) return;
    box.innerHTML = '';
    var inner = document.createElement('div');
    box.appendChild(inner);

    var view = {
        root: root, box: box, rows: rows, holds: holds, segments: segments, ranges: ranges,
        panels: panels, duration: duration, lastNote: lastNote, level: level, compact: compact,
        meter: payload.meter != null ? payload.meter : null,
        strings: strings, payload: payload, colors: COL,
        isPhoenix2: isPhoenix2, mode: 'arrow', token: token
    };
    root.stepChartView = view;
    view.measureAvail = function () {
        var viewer = root.querySelector('.stepchart-viewer');
        if (!viewer) return null;
        var minicol = root.querySelector('.stepchart-minicol');
        var w = viewer.clientWidth;
        if (minicol && minicol.offsetWidth > 0) w -= minicol.offsetWidth + 14;
        return Math.max(140, w);
    };
    view.availWidth = view.measureAvail();
    applyScale(view);

    try {
        var savedAv = parseInt(window.localStorage.getItem('stepchart-av') || '', 10);
        if (savedAv >= 200 && savedAv <= 900) { view.userAv = savedAv; applyScale(view); }
    } catch (e) { /* private mode */ }

    var resizePending = null;
    window.addEventListener('resize', function () {
        if (resizePending) return;
        resizePending = requestAnimationFrame(function () {
            resizePending = null;
            var avail = view.measureAvail();
            if (avail == null || Math.abs(avail - (view.availWidth || 0)) < 2) return;
            view.availWidth = avail;
            relayoutStrip(view);
        });
    });

    drawStrip(view);
    initMinimap(view);
    buildChips(view);
    renderLegend(view);
    initModes(view);
    initAv(view);

    if (root.getAttribute('data-visibility') === 'StepsOnly') {
        var caveat = root.querySelector('[data-stepchart-caveat]');
        if (caveat) caveat.hidden = false;
    }

    var chipPending = null;
    box.addEventListener('scroll', function () {
        if (chipPending) return;
        chipPending = requestAnimationFrame(function () { chipPending = null; highlightChips(view); });
    });

    if (root.getAttribute('data-visibility') === 'Full') await loadBreaks(view, chartId, mix);

    // The strip opens at the TOP — landing mid-chart is disorienting (owner, 2026-08-30);
    // the chips and the map are the fast ways down.
    highlightChips(view);
}

// Layout from the effective AV. Geometry mirrors the cabinet, measured off gameplay footage
// (Repentance D26, BPM 240 at AV650): columns touch — the arrow IS the column, two pixels of
// air — and pixel velocity is 0.85 x AV, which puts a chart's dense subdivision right around
// one arrow-height apart at the level's usual AV. Spacing/sizing is pattern recognition
// (owner, 2026-08-30), so the strip reads like the game, not like a diagram of it.
function applyScale(view) {
    var av = view.userAv || assistVelocity(view.level);
    view.av = av;
    var base = view.compact
        ? { pps: av * 0.47, colW: 30, gutter: 44, railW: 78 }
        : { pps: av * 0.85, colW: view.panels === 10 ? 40 : 46, gutter: 52, railW: 96 };
    // The strip never scrolls horizontally (owner, 2026-08-30 — a deal breaker on mobile):
    // when the host is narrower than the natural geometry, EVERYTHING scales down uniformly
    // — columns, gutter, rail, and the pixel velocity with them, so arrows keep their
    // proportions and the whole thing reads like the same strip further away.
    var natural = base.gutter + base.colW * view.panels + 14 + base.railW;
    var fit = Math.min(1, (view.availWidth || natural) / natural);
    view.scale = {
        pps: base.pps * fit,
        colW: base.colW * fit,
        gutter: base.gutter * fit,
        railW: base.railW * fit,
        gap: 14 * fit,
        arrow: Math.max(8, base.colW * fit - 2),
        font: fit < 1 ? Math.max(7, Math.round(10 * fit)) : 10
    };
    view.stripW = view.scale.colW * view.panels;
    view.railX = view.scale.gutter + view.stripW + view.scale.gap;
    view.width = view.railX + view.scale.railW;
    view.height = Math.ceil(view.duration * view.scale.pps) + 24;
    view.yOf = function (t) { return 12 + t * view.scale.pps; };
    view.box.firstChild.style.width = view.width + 'px';
}

// Anchor-preserving full relayout — AV changes and host resizes share it.
function relayoutStrip(view) {
    var anchor = view.box.scrollTop / Math.max(1, view.height);
    applyScale(view);
    drawStrip(view);
    view.box.scrollTop = anchor * view.height;
    if (view.repaintMinimap) view.repaintMinimap();
    highlightChips(view);
}

// The AV stepper (owner side-note, 2026-08-30): players have "their AV", so the ramp is only
// the default. The choice is global (localStorage), stepped by 50 within 200-900, and Auto
// returns to the level ramp. Changing it relays the whole strip out — placeholders resize,
// live tiles repaint, the minimap viewport follows; pins and chips hold times, so they land
// themselves.
function initAv(view) {
    var host = view.root.querySelector('[data-stepchart-av-value]');
    var buttons = Array.prototype.slice.call(view.root.querySelectorAll('[data-stepchart-av]'));
    if (!host || buttons.length === 0) return;

    function show() {
        host.textContent = 'AV ' + Math.round(view.av);
        buttons.forEach(function (button) {
            if (button.getAttribute('data-stepchart-av') === 'auto')
                button.setAttribute('aria-pressed', view.userAv ? 'false' : 'true');
        });
    }

    function relayout() {
        relayoutStrip(view);
        show();
    }

    buttons.forEach(function (button) {
        var step = button.getAttribute('data-stepchart-av');
        button.addEventListener('click', function () {
            if (step === 'auto') view.userAv = null;
            else view.userAv = Math.max(200, Math.min(900,
                (view.userAv || Math.round(view.av)) + parseInt(step, 10)));
            try {
                if (view.userAv) window.localStorage.setItem('stepchart-av', String(view.userAv));
                else window.localStorage.removeItem('stepchart-av');
            } catch (e) { /* fine */ }
            relayout();
        });
    });

    show();
}

// The owner's AV ramp, linear between the anchor levels — players run ~300 AV on a 1, ~450 by
// 10, ~600 by 16, capping ~700 at 23+. Level beats NPS: AV is chosen per level, and an
// averaged NPS lies about marathon charts. (Field-corroborated: the reference footage's
// results screen badges AV650 on a D26.)
function assistVelocity(level) {
    if (!level || level <= 1) return 300;
    if (level <= 10) return 300 + (level - 1) * (150 / 9);
    if (level <= 16) return 450 + (level - 10) * (150 / 6);
    if (level <= 23) return 600 + (level - 16) * (100 / 7);
    return 700;
}

function readStrings(root) {
    var block = root.querySelector('[data-stepchart-strings]');
    try { return block ? JSON.parse(block.textContent) : {}; } catch (e) { return {}; }
}

async function fetchJson(url) {
    try {
        var response = await fetch(url, { headers: { Accept: 'application/json' } });
        return response.ok ? await response.json() : null;
    } catch (e) {
        return null;
    }
}

function noteColor(view, row, panel) {
    var lane = panel % 5;
    return lane === 2 ? view.colors.center : (lane === 1 || lane === 3 ? view.colors.upper : view.colors.lower);
}

function holdColor(view, hold) {
    return noteColor(view, null, hold.p);
}

function fmt(t) {
    var m = Math.floor(t / 60), s = Math.floor(t % 60);
    return m + ':' + (s < 10 ? '0' : '') + s;
}

// Tiles are virtual: fixed-height placeholders carry the scroll geometry, and a canvas is
// painted into a placeholder only while it sits within ~1500px of the viewport (and freed
// again when it leaves). At game density a FULL SONG is ~340k px of strip — eager canvases
// at that height are gigabytes of bitmap; a handful of live tiles is a few dozen MB.
function drawStrip(view) {
    var inner = view.box.firstChild;
    inner.innerHTML = '';
    if (view.tileObserver) view.tileObserver.disconnect();
    var TILE = 3000;
    var dpr = Math.min(window.devicePixelRatio || 1, 1.5);

    var byPlaceholder = new Map();
    for (var k = 0; k * TILE < view.height; k++) {
        var tileH = Math.min(TILE, view.height - k * TILE);
        var placeholder = document.createElement('div');
        placeholder.style.height = tileH + 'px';
        inner.appendChild(placeholder);
        byPlaceholder.set(placeholder, { top: k * TILE, h: tileH, drawn: false, el: placeholder });
    }

    function paint(tile) {
        if (tile.drawn) return;
        tile.drawn = true;
        var canvas = document.createElement('canvas');
        canvas.width = Math.round(view.width * dpr);
        canvas.height = Math.round(tile.h * dpr);
        canvas.style.width = view.width + 'px';
        canvas.style.height = tile.h + 'px';
        tile.el.appendChild(canvas);
        var ctx = canvas.getContext('2d');
        ctx.scale(dpr, dpr);
        ctx.translate(0, -tile.top);
        drawTile(view, ctx, tile.top, tile.top + tile.h);
    }

    function free(tile) {
        if (!tile.drawn) return;
        tile.drawn = false;
        tile.el.innerHTML = '';
    }

    view.tileObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            var tile = byPlaceholder.get(entry.target);
            if (!tile) return;
            if (entry.isIntersecting) paint(tile); else free(tile);
        });
    }, { root: view.box, rootMargin: '1500px 0px' });
    byPlaceholder.forEach(function (tile) { view.tileObserver.observe(tile.el); });
}

function drawTile(view, ctx, y0, y1) {
    var s = view.scale;
    var tMin = Math.max(0, (y0 - 60) / s.pps);
    var tMax = (y1 + 60) / s.pps;

    ctx.fillStyle = 'rgba(255,255,255,.03)';
    ctx.fillRect(s.gutter, y0, view.stripW, y1 - y0);
    // No per-column hairlines — the cabinet has none, and at touching-column geometry they
    // read as grid clutter. The pad keeps its edges, doubles keeps its center split.
    ctx.lineWidth = 1;
    [0, view.panels === 10 ? 5 : -1, view.panels].forEach(function (c) {
        if (c < 0) return;
        ctx.strokeStyle = c === 5 ? 'rgba(255,255,255,.16)' : 'rgba(255,255,255,.10)';
        ctx.beginPath();
        ctx.moveTo(s.gutter + c * s.colW + 0.5, y0);
        ctx.lineTo(s.gutter + c * s.colW + 0.5, y1);
        ctx.stroke();
    });

    ctx.font = '10px monospace';
    ctx.textBaseline = 'middle';
    for (var t = 0; t <= view.duration; t += 5) {
        var ry = view.yOf(t);
        if (ry < y0 - 20 || ry > y1 + 20) continue;
        ctx.strokeStyle = 'rgba(255,255,255,.06)';
        ctx.beginPath();
        ctx.moveTo(s.gutter - 4, ry + 0.5);
        ctx.lineTo(view.width - 4, ry + 0.5);
        ctx.stroke();
        ctx.fillStyle = view.colors.inkMuted;
        ctx.textAlign = 'right';
        ctx.fillText(fmt(t), s.gutter - 8, ry);
    }

    view.segments.forEach(function (segment) {
        var ry = view.yOf(segment.s);
        if (ry < y0 - 30 || ry > y1 + 30 || segment.s <= 0) return;
        ctx.strokeStyle = view.colors.accent;
        ctx.globalAlpha = 0.35;
        ctx.setLineDash([5, 5]);
        ctx.beginPath();
        ctx.moveTo(s.gutter, ry + 0.5);
        ctx.lineTo(s.gutter + view.stripW, ry + 0.5);
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.globalAlpha = 1;
    });

    view.holds.forEach(function (hold) {
        if (hold.e < tMin || hold.s > tMax) return;
        var x = s.gutter + hold.p * s.colW + s.colW / 2;
        var hy0 = view.yOf(hold.s);
        var hy1 = view.yOf(hold.e);
        var color = holdColorFor(view, hold);
        ctx.fillStyle = color;
        ctx.globalAlpha = 0.26;
        var bw = s.arrow * 0.55;
        roundRect(ctx, x - bw / 2, hy0, bw, Math.max(hy1 - hy0, 1), bw / 2);
        ctx.fill();
        ctx.globalAlpha = 1;
        ctx.beginPath();
        ctx.arc(x, hy1, s.arrow * 0.2, 0, Math.PI * 2);
        ctx.fill();
        drawArrow(ctx, x, hy0, s.arrow, hold.p, color);
    });

    view.rows.forEach(function (row) {
        if (row.t < tMin || row.t > tMax) return;
        for (var p = 0; p < view.panels; p++) {
            if (!(row.m & (1 << p))) continue;
            drawArrow(ctx, s.gutter + p * s.colW + s.colW / 2, view.yOf(row.t), s.arrow, p,
                rowColorFor(view, row, p));
        }
    });

    drawRail(view, ctx, y0, y1);
}

function finishedText(view, n) {
    return n === 1
        ? (view.strings.finishedOne || '1 broken run made it to the end')
        : (view.strings.finishedMany || '{0} broken runs made it to the end')
            .replace('{0}', n);
}

// The failure rail (design doc D1/D2): a thin axis beside the strip, life-bar pins in the
// near column and proven-Pass pins in the far one, multiplied heads where runs stack, and
// the viewer's own runs as gold open diamonds on the axis itself.
function drawRail(view, ctx, y0, y1) {
    if (!view.breaks) return;
    ctx.strokeStyle = 'rgba(255,255,255,.12)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(view.railX + 0.5, y0);
    ctx.lineTo(view.railX + 0.5, y1);
    ctx.stroke();

    view.pinMarks.forEach(function (pin) {
        var y = view.yOf(pin.t) + (pin.dy || 0);
        if (y < y0 - 24 || y > y1 + 24) return;
        var big = pin.n > 1;
        var r = big ? 9.5 : 5;
        ctx.strokeStyle = pin.color;
        ctx.lineWidth = 1.6;
        ctx.beginPath();
        ctx.moveTo(view.railX, y + 0.5);
        ctx.lineTo(pin.x - r, y + 0.5);
        ctx.stroke();
        ctx.fillStyle = pin.color;
        ctx.beginPath();
        ctx.arc(view.railX, y, 2.6, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(pin.x, y, r, 0, Math.PI * 2);
        ctx.fill();
        if (big) {
            ctx.fillStyle = 'rgba(10,10,14,.92)';
            ctx.font = '700 10px sans-serif';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText('\u00d7' + pin.n, pin.x, y + 0.5);
        }
    });

    ctx.strokeStyle = view.colors.you;
    ctx.lineWidth = 2;
    view.breaks.yours.forEach(function (t) {
        var y = view.yOf(t);
        if (y < y0 - 12 || y > y1 + 12) return;
        ctx.save();
        ctx.translate(view.railX, y);
        ctx.rotate(Math.PI / 4);
        ctx.strokeRect(-4.5, -4.5, 9, 9);
        ctx.restore();
    });
}

async function loadBreaks(view, chartId, mix) {
    var breaks = await fetchJson('/Charts/StepChart/' + chartId + '/Breaks?mix=' + encodeURIComponent(mix));
    if (!breaks || (breaks.total === 0 && !breaks.unplaced && !breaks.finished)) return;

    view.colors.life = view.token('--life-danger');
    view.colors.walk = view.token('--step-walkoff');
    view.colors.pass = view.token('--step-pass');
    view.colors.you = view.token('--step-you');
    view.breaks = breaks;

    var lifeX = view.railX + (view.scale.railW >= 96 ? 24 : 18);
    var passX = view.railX + (view.scale.railW >= 96 ? 56 : 44);
    view.pinMarks = breaks.pins.map(function (pin) {
        return {
            t: pin.t, n: pin.n, from: pin.from, to: pin.to, cause: pin.cause,
            cmds: pin.cmds || [],
            // Walk-offs share the bar column — they are what the bar column used to overclaim —
            // and wear their own hue.
            x: pin.cause === 'pass' ? passX : lifeX,
            color: pin.cause === 'pass' ? view.colors.pass
                : pin.cause === 'walk' ? view.colors.walk
                    : view.colors.life
        };
    });

    // Broken runs that FINISHED get their own pin just past the last note (owner, 2026-08-30):
    // they have no break point to mark, and the earlier bottom-of-the-strip caption sat past
    // seconds of empty outro nobody scrolls to. The offset is pixels, not time, so it hugs the
    // last note at any AV.
    if (breaks.finished > 0)
        view.pinMarks.push({
            t: view.lastNote, dy: 16, n: breaks.finished, from: view.lastNote, to: view.lastNote,
            cause: 'fin', cmds: [], x: lifeX, color: view.colors.inkMuted
        });

    // Death marks are TICKS on the map's right edge in their cause's color, thicker where
    // runs stack — full-width lines drowned the section bands (owner, 2026-08-31). The
    // viewer's own runs notch in gold at the very edge.
    view.drawMinimapPins = function (ctx, yOf, W) {
        view.pinMarks.forEach(function (pin) {
            ctx.fillStyle = pin.color;
            ctx.fillRect(W - 12, yOf(pin.t) + (pin.dy ? 2 : 0) - 1, 10,
                Math.max(2, Math.min(1 + pin.n, 5)));
        });
        ctx.fillStyle = view.colors.you;
        view.breaks.yours.forEach(function (t) {
            ctx.fillRect(W - 5, yOf(t), 4, 2);
        });
    };

    drawStrip(view);
    if (view.repaintMinimap) view.repaintMinimap();
    renderLegend(view);
    bindPinTips(view);
}

// The coloring modes (design doc D12), one seam. Arrows reads the panel; Feet reads the
// snapshot's limb masks. (Timing is withdrawn until its own session — the beats still bank.)
function rowColorFor(view, row, panel) {
    if (view.mode === 'foot')
        return (row.l & (1 << panel)) ? view.colors.footL : view.colors.footR;
    return noteColor(view, row, panel);
}

function holdColorFor(view, hold) {
    if (view.mode === 'foot') return hold.left ? view.colors.footL : view.colors.footR;
    return holdColor(view, hold);
}

// The toggle: three buttons the server rendered, localStorage for stickiness (per ruling 6:
// the lightest thing that behaves well — a static page writes no per-user settings), and
// Timing disabled outright on charts whose beats never aligned (D6).
function initModes(view) {
    var buttons = Array.prototype.slice.call(view.root.querySelectorAll('[data-stepchart-mode]'));
    if (buttons.length === 0) return;

    // Timing mode is withdrawn for now (owner, 2026-08-30 — "isn't working at all; circle
    // back another session"): no button, and a remembered 'quant' falls back to Arrows. The
    // banked beats and quantization are untouched, so bringing it back is markup + a seam.
    var saved = null;
    try { saved = window.localStorage.getItem('stepchart-mode'); } catch (e) { /* private mode */ }
    if (saved === 'foot') view.mode = saved;

    function apply() {
        buttons.forEach(function (button) {
            button.setAttribute('aria-pressed',
                button.getAttribute('data-stepchart-mode') === view.mode ? 'true' : 'false');
        });
        drawStrip(view);
        renderLegend(view);
    }

    buttons.forEach(function (button) {
        var mode = button.getAttribute('data-stepchart-mode');
        button.addEventListener('click', function () {
            if (view.mode === mode) return;
            view.mode = mode;
            try { window.localStorage.setItem('stepchart-mode', mode); } catch (e) { /* fine */ }
            apply();
        });
    });

    // The server ships the markup already pressed on Arrows; repainting the whole strip is
    // only owed when a remembered mode actually differs.
    if (view.mode !== 'arrow') apply();
}

// The cabinet's arrow sprite (owner, 2026-08-30): a ">>>" glyph rotated onto the diagonal —
// a triangular head whose 90° tip is the cell corner and whose two LEGS lie flat on the cell
// edges (those legs are the "flat sides"), a chevron notch in its back, and two tapering
// chevron ribs behind it with background between the pieces. Rotating ">>>" by 45° makes
// every leg and rib arm axis-aligned, which is exactly why a quad of them reads as one flat
// row. The center panel is the cabinet's octagon. Lane = panel % 5 in pad order DL UL C UR
// DR; the canonical arrow points down-right and flips into place by sign.
var CORNER_SIGNS = { 0: [-1, 1], 1: [-1, -1], 3: [1, -1], 4: [1, 1] };

function drawArrow(ctx, x, y, size, panel, color) {
    var lane = panel % 5;
    ctx.save();
    ctx.translate(x, y);
    ctx.fillStyle = color;
    ctx.strokeStyle = 'rgba(255,255,255,.55)';
    ctx.lineWidth = 1.3;
    var h = size * 0.5 - 0.75;

    if (lane === 2) {
        var k = size * 0.29;
        ctx.beginPath();
        ctx.moveTo(-h + k, -h);
        ctx.lineTo(h - k, -h);
        ctx.lineTo(h, -h + k);
        ctx.lineTo(h, h - k);
        ctx.lineTo(h - k, h);
        ctx.lineTo(-h + k, h);
        ctx.lineTo(-h, h - k);
        ctx.lineTo(-h, -h + k);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
    } else {
        ctx.scale(CORNER_SIGNS[lane][0], CORNER_SIGNS[lane][1]);
        // Tip coordinates, as fractions of the cell side: a runs from the tip back along one
        // leg, b along the other. min(a,b) walks the chevron cuts, a+b walks the hypotenuses.
        var u = h * 2;
        var P = function (a, b) { return [h - a * u, h - b * u]; };
        var poly = function (pts) {
            ctx.beginPath();
            pts.forEach(function (p, i) { i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1]); });
            ctx.closePath();
            ctx.fill();
            ctx.stroke();
        };
        // Head: legs 0.78 of the side flat on the cell edges, pointy barbs, notched back.
        var l = 0.78, c = 0.20;
        poly([P(0, 0), P(l, 0), P(l - c, c), P(c, c), P(c, l - c), P(0, l)]);
        // Ribs: chevron bands between min-cuts r0..r1, ends cut on the hypotenuse a+b = L.
        var rib = function (r0, r1, L) {
            poly([P(r0, r0), P(L - r0, r0), P(L - r1, r1), P(r1, r1), P(r1, L - r1), P(r0, L - r0)]);
        };
        rib(0.28, 0.42, 0.94);
        rib(0.50, 0.64, 1.30);
    }
    ctx.restore();
}

function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(x, y, w, h, r); else ctx.rect(x, y, w, h);
}

// The whole-chart map, on both hosts — the dialog rides it in the dead space right of the
// rail at compact sizes (owner, 2026-08-31). CSS owns the dimensions; the canvas reads them.
function initMinimap(view) {
    var canvas = view.root.querySelector('[data-stepchart-minimap]');
    if (!canvas) return;

    var W = canvas.clientWidth || 92, H = canvas.clientHeight || 600;
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = W * dpr;
    canvas.height = H * dpr;
    var ctx = canvas.getContext('2d');
    ctx.scale(dpr, dpr);
    var yOf = function (t) { return (t / view.duration) * (H - 8) + 4; };

    var seconds = Math.max(1, Math.ceil(view.duration));
    var density = new Array(seconds).fill(0);
    view.rows.forEach(function (row) { density[Math.min(seconds - 1, Math.floor(row.t))]++; });
    var maxDensity = Math.max.apply(null, density.concat([1]));

    // The density bars THEMSELVES carry relative difficulty (owner, 2026-08-30 — the band
    // overlays didn't read): red = the Difficulty Spike, yellow = the other notable
    // sections, green = below the bar but within 3 levels of it, bare ink = filler. The
    // hues are the difficulty ramp's own ends and middle.
    var notable = notableOf(view);
    var tierOf = new Array(seconds).fill(null);
    if (notable.threshold != null) {
        var tiers = {
            red: view.token('--diff-underrated'),
            yellow: view.token('--diff-medium'),
            green: view.token('--diff-overrated')
        };
        view.segments.forEach(function (segment) {
            var color = segment === notable.spike ? tiers.red
                : segment.lvl == null ? null
                    : segment.lvl >= notable.threshold ? tiers.yellow
                        : segment.lvl >= notable.threshold - 3 ? tiers.green : null;
            if (!color) return;
            var last = Math.min(seconds, Math.ceil(segment.e));
            for (var sec = Math.max(0, Math.floor(segment.s)); sec < last; sec++) tierOf[sec] = color;
        });
    }

    view.repaintMinimap = function () {
        ctx.clearRect(0, 0, W, H);
        for (var second = 0; second < seconds; second++) {
            var w = (density[second] / maxDensity) * (W * 0.5);
            if (w <= 0) continue;
            ctx.fillStyle = tierOf[second] || 'rgba(255,255,255,.16)';
            ctx.globalAlpha = tierOf[second] ? 0.9 : 1;
            ctx.fillRect(4, yOf(second), w, Math.max(1, (H - 8) / view.duration));
        }
        ctx.globalAlpha = 1;
        if (view.drawMinimapPins) view.drawMinimapPins(ctx, yOf, W, H);
        ctx.strokeStyle = view.colors.accent;
        ctx.lineWidth = 1.5;
        var top = (view.box.scrollTop / view.height) * (H - 8) + 4;
        var viewportH = (view.box.clientHeight / view.height) * (H - 8);
        ctx.strokeRect(1.5, top, W - 3, viewportH);
    };

    var pending = null;
    view.box.addEventListener('scroll', function () {
        if (pending) return;
        pending = requestAnimationFrame(function () { pending = null; view.repaintMinimap(); });
    });
    canvas.addEventListener('click', function (e) {
        var rect = canvas.getBoundingClientRect();
        var fraction = (e.clientY - rect.top - 4) / (rect.height - 8);
        view.box.scrollTo({ top: fraction * view.height - view.box.clientHeight / 2, behavior: motion() });
    });
    view.repaintMinimap();
}

function motion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth';
}

// The notable sections (owner rulings 2026-08-31): piucenter rates every segment's difficulty
// alone; a section is notable when its level is within 1.5 of min(chart's own peak, listed
// level) — "either, whichever is lower": a chart that is easy for its label marks its own top
// band, a chart that is hard for its label also marks everything at the label. The peak
// section is the Difficulty Spike. One chip per qualifying segment, never merged — adjacent
// notable segments are different characters (a bracket wall, then the run). Payloads banked
// before this round carry no levels and fall back to the old shape: the densest segment by
// eNPS plus the snapshot's ranges of interest.
var NotableWindow = 1.5;

function notableOf(view) {
    var peak = null;
    view.segments.forEach(function (segment) {
        if (segment.lvl == null) return;
        if (peak == null || segment.lvl > peak) peak = segment.lvl;
    });

    // No levels (a payload banked before the labeling rounds): nothing is notable. Better an
    // empty chip row than generic placeholders (owner, 2026-08-30).
    if (peak == null) return { spike: null, sections: [] };

    var threshold = Math.min(peak, view.meter != null ? view.meter : peak) - NotableWindow;
    // Very Fast promotes: the folder's fastest footwork is notable even when the model rates
    // the passage below the level bar (owner, 2026-08-30 — Solve My Hurt's 16.57-level drill
    // section is the chart's signature). Corpus cost: 89% of charts gain nothing, p90 one
    // extra chip.
    var sections = view.segments.filter(function (segment) {
        return (segment.lvl != null && segment.lvl >= threshold) || segment.pace === 'vf';
    });
    var spike = null;
    sections.forEach(function (segment) {
        if (!spike && segment.lvl === peak) spike = segment;
    });
    return { spike: spike, sections: sections, threshold: threshold };
}

// A section's label is its top skill and its folder-relative pace word — never a number, and
// never a generic placeholder (owner, 2026-08-30). Empty when the section has neither.
function sectionLabel(view, section) {
    var parts = [];
    if (section.badges && section.badges.length) parts.push(section.badges[0]);
    var pace = { vf: view.strings.veryFast || 'Very Fast', f: view.strings.fast || 'Fast',
        s: view.strings.slow || 'Slow', vs: view.strings.verySlow || 'Very Slow' }[section.pace];
    if (pace) parts.push(pace);
    return parts.join(' · ');
}

// One chip per notable section, wearing the section's top skill and its density — "Drills
// 12.4 NPS", the peak as "Difficulty Spike: Close Twists 8.7 NPS" (owner rulings 2026-08-31).
// Death clusters no longer chip — the whole-chart map's ticks carry them now.
function buildChips(view) {
    var host = view.root.querySelector('[data-stepchart-chips]');
    if (!host) return;
    host.innerHTML = '';
    view.chipSpans = [];

    var notable = notableOf(view);
    notable.sections.forEach(function (section) {
        var inner = sectionLabel(view, section);
        var label = section === notable.spike
            ? (inner
                ? (view.strings.spike || 'Difficulty Spike: {0}').replace('{0}', inner)
                : (view.strings.spike || 'Difficulty Spike: {0}').replace('{0}', '').replace(/[:：]\s*$/, ''))
            : inner;
        if (!label) return;
        var chip = addChip(view, host, section === notable.spike ? 'spike' : 'struct', label, section.s);
        view.chipSpans.push({ el: chip, s: section.s, e: section.e });
    });

    highlightChips(view);
}

// A chip lights while the viewport is inside its section — the chips double as a "you are
// here" for the strip (owner, 2026-08-31).
function highlightChips(view) {
    if (!view.chipSpans) return;
    var top = (view.box.scrollTop - 12) / view.scale.pps;
    var bottom = (view.box.scrollTop + view.box.clientHeight - 12) / view.scale.pps;
    view.chipSpans.forEach(function (span) {
        span.el.classList.toggle('stepchart-chip-active', span.s < bottom && span.e > top);
    });
}

function addChip(view, host, kind, label, t, count) {
    var chip = document.createElement('button');
    chip.type = 'button';
    chip.className = 'stepchart-chip stepchart-chip-' + kind;
    var text = label + (count && count > 1 ? ' ×' + count : '');
    chip.innerHTML = '<i></i>' + escapeHtml(text) + ' <small>' + fmt(t) + '</small>';
    chip.addEventListener('click', function () {
        view.box.scrollTo({ top: Math.max(0, view.yOf(t) - 110), behavior: motion() });
        view.box.focus({ preventScroll: true });
    });
    host.appendChild(chip);
    return chip;
}

function escapeHtml(text) {
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function renderLegend(view) {
    var host = view.root.querySelector('[data-stepchart-legend]');
    if (!host) return;
    host.innerHTML = '';
    if (view.mode === 'foot') {
        legendEntry(host, view.colors.footL, view.strings.leftFoot || 'Left foot');
        legendEntry(host, view.colors.footR, view.strings.rightFoot || 'Right foot');
    } else {
        legendEntry(host, view.colors.upper, view.strings.upper || 'Upper');
        legendEntry(host, view.colors.lower, view.strings.lower || 'Lower');
        legendEntry(host, view.colors.center, view.strings.center || 'Center');
    }

    if (view.breaks && (view.breaks.total > 0 || view.breaks.unplaced > 0)) {
        // On Phoenix 2 the bar-death series wears the game's own Pass G command art \u2014 the
        // badge is the sentence, never spelled out (owner, 2026-08-30).
        if (view.breaks.life > 0) {
            if (view.isPhoenix2) {
                var lifeSpan = legendEntry(host, view.colors.life, ' \u00b7 ' + view.breaks.life);
                var passImg = document.createElement('img');
                passImg.className = 'stepchart-legend-cmd';
                passImg.src = CommandArtRoot + 'Pass_Plate_G.png';
                passImg.alt = view.strings.passG || 'Pass G';
                lifeSpan.insertBefore(passImg, lifeSpan.lastChild);
            } else {
                legendEntry(host, view.colors.life,
                    (view.strings.lifeBreak || 'Life Bar Break') + ' \u00b7 ' + view.breaks.life);
            }
        }
        if (view.breaks.walk > 0)
            legendEntry(host, view.colors.walk,
                (view.strings.walkOff || 'Walk off') + ' \u00b7 ' + view.breaks.walk);
        if (view.breaks.pass > 0)
            legendEntry(host, view.colors.pass,
                (view.strings.stagePass || 'Stage Pass') + ' \u00b7 ' + view.breaks.pass);
        if (view.breaks.yours.length > 0)
            legendEntry(host, view.colors.you,
                (view.strings.yourRuns || 'Your runs') + ' \u00b7 ' + view.breaks.yours.length);
        // Breaks imported without judgement counts can never be placed — the rail admits to
        // them instead of letting the placed set read as the whole story (owner, 2026-08-30).
        if (view.breaks.unplaced > 0)
            legendEntry(host, view.colors.inkMuted,
                (view.strings.unplaced || 'Unplaced') + ' \u00b7 ' + view.breaks.unplaced);
    }
}

// The game's own command-window art, mirrored — the same root PassCommandBadge uses.
var CommandArtRoot = 'https://piuimages.arroweclip.se/commands/';

// Hovering a pin explains it: the span, the count, the cause — and on Singles pads the D34
// hedge, because a proven non-lifebar break may be the other pad's command.
function bindPinTips(view) {
    if (view.tipBound) return;
    view.tipBound = true;
    var tip = document.createElement('div');
    tip.className = 'stepchart-tip';
    tip.hidden = true;
    document.body.appendChild(tip);

    view.box.addEventListener('mousemove', function (e) {
        if (!view.pinMarks) { tip.hidden = true; return; }
        var rect = view.box.getBoundingClientRect();
        var x = e.clientX - rect.left + view.box.scrollLeft;
        var y = e.clientY - rect.top + view.box.scrollTop;
        var hit = null;
        for (var i = 0; i < view.pinMarks.length; i++) {
            var pin = view.pinMarks[i];
            var r = (pin.n > 1 ? 9.5 : 5) + 5;
            var dx = x - pin.x;
            var dy = y - view.yOf(pin.t) - (pin.dy || 0);
            if (dx * dx + dy * dy <= r * r) { hit = pin; break; }
        }

        if (!hit) { tip.hidden = true; return; }
        // Terse on purpose (owner, 2026-08-30): the badge art IS the sentence for a named
        // Pass, and everything else is three words plus a count.
        var range = hit.n > 1 && hit.from !== hit.to ? fmt(hit.from) + '\u2013' + fmt(hit.to) : fmt(hit.t);
        var count = hit.n > 1 ? ' \u00d7' + hit.n : '';
        var body;
        if (hit.cause === 'fin') {
            body = '<b>' + escapeHtml(finishedText(view, hit.n)) + '</b>';
        } else if (hit.cause === 'pass' && hit.cmds.length) {
            body = hit.cmds.map(function (cmd) {
                return '<img class="stepchart-tip-cmd" alt="' + escapeHtml(cmd) +
                    '" src="' + CommandArtRoot + encodeURI(cmd) + '.png">';
            }).join('') + escapeHtml(count);
        } else if (hit.cause === 'pass') {
            body = '<b>' + escapeHtml((view.strings.unknownBreak || 'Unknown Break') + count) + '</b>';
        } else if (hit.cause === 'walk') {
            body = '<b>' + escapeHtml((view.strings.walkOff || 'Walk off') + count) + '</b>';
        } else if (view.isPhoenix2) {
            // On Phoenix 2 the bar cannot end a Premium song without Pass G, and the game
            // has art for it — the badge is the sentence, never spelled out (owner,
            // 2026-08-30). Phoenix 1 has no commands and keeps the plain phrase.
            body = '<img class="stepchart-tip-cmd" alt="' + escapeHtml(view.strings.passG || 'Pass G') +
                '" src="' + CommandArtRoot + 'Pass_Plate_G.png">' + escapeHtml(count);
        } else {
            body = '<b>' + escapeHtml((view.strings.lifeBreak || 'Life Bar Break') + count) + '</b>';
        }

        tip.innerHTML = '<div class="stepchart-tip-time">' + range + '</div>' + body;
        tip.hidden = false;
        tip.style.left = Math.min(e.clientX + 14, window.innerWidth - tip.offsetWidth - 12) + 'px';
        tip.style.top = (e.clientY + 14) + 'px';
    });
    view.box.addEventListener('mouseleave', function () { tip.hidden = true; });
}

function legendEntry(host, color, label) {
    var span = document.createElement('span');
    span.className = 'stepchart-legend-entry';
    var dot = document.createElement('i');
    dot.style.background = color;
    span.appendChild(dot);
    span.appendChild(document.createTextNode(label));
    host.appendChild(span);
    return span;
}

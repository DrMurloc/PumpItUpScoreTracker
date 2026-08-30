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
    var compact = root.getAttribute('data-compact') === '1';
    if (!chartId || !mix) return;

    var strings = readStrings(root);
    var payload = await fetchJson('/Charts/StepChart/' + chartId + '?mix=' + encodeURIComponent(mix));
    if (!payload || !payload.rows || payload.rows.length === 0) {
        var section = root.closest('section') || root;
        section.hidden = true;
        return;
    }

    var css = getComputedStyle(document.documentElement);
    var token = function (name) { return css.getPropertyValue(name).trim() || '#888'; };
    var COL = {
        upper: token('--panel-upper'), lower: token('--panel-lower'), center: token('--panel-center'),
        ink: token('--mix-ink'), inkMuted: token('--mix-ink-muted'), accent: token('--mix-primary')
    };

    // rows: [time, panelMask, leftMask, quant, beat|null]
    var rows = payload.rows.map(function (r) { return { t: r[0], m: r[1], l: r[2], q: r[3], b: r[4] }; });
    var holds = payload.holds.map(function (h) { return { p: h[0], s: h[1], e: h[2], left: h[3] === 1 }; });
    var segments = payload.segments.map(function (s) { return { s: s[0], e: s[1], enps: s[2] }; });
    var ranges = payload.ranges.map(function (r) { return { s: r[0], e: r[1] }; });
    var panels = payload.panels;
    var duration = Math.max(
        rows.length ? rows[rows.length - 1].t : 0,
        holds.reduce(function (max, h) { return Math.max(max, h.e); }, 0)) + 2;

    var scale = compact
        ? { pps: 110, colW: 30, gutter: 44, railW: 78, arrow: 19 }
        : { pps: 200, colW: panels === 10 ? 40 : 46, gutter: 52, railW: 96, arrow: panels === 10 ? 25 : 27 };
    var stripW = scale.colW * panels;
    var railX = scale.gutter + stripW + 14;
    var width = railX + scale.railW;
    var height = Math.ceil(duration * scale.pps) + 24;
    var yOf = function (t) { return 12 + t * scale.pps; };

    var box = root.querySelector('[data-stepchart-scroll]');
    if (!box) return;
    box.innerHTML = '';
    var inner = document.createElement('div');
    inner.style.width = width + 'px';
    box.appendChild(inner);

    var view = {
        root: root, box: box, rows: rows, holds: holds, segments: segments, ranges: ranges,
        panels: panels, duration: duration, scale: scale, stripW: stripW, railX: railX,
        width: width, height: height, yOf: yOf, strings: strings, payload: payload, colors: COL,
        mode: 'arrow', token: token
    };
    root.stepChartView = view;

    drawStrip(view);
    initMinimap(view, compact);
    buildChips(view);
    renderLegend(view);

    if (root.getAttribute('data-visibility') === 'StepsOnly') {
        var caveat = root.querySelector('[data-stepchart-caveat]');
        if (caveat) caveat.hidden = false;
    }

    // Land on the crux rather than the silent intro — the reader came to see the chart's teeth.
    var crux = cruxOf(view);
    if (crux) box.scrollTop = Math.max(0, yOf(crux.s) - 90);
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

function drawStrip(view) {
    var inner = view.box.firstChild;
    inner.innerHTML = '';
    var TILE = 3000;
    var dpr = Math.min(window.devicePixelRatio || 1, 1.5);

    for (var k = 0; k * TILE < view.height; k++) {
        var tileH = Math.min(TILE, view.height - k * TILE);
        var canvas = document.createElement('canvas');
        canvas.width = Math.round(view.width * dpr);
        canvas.height = Math.round(tileH * dpr);
        canvas.style.width = view.width + 'px';
        canvas.style.height = tileH + 'px';
        inner.appendChild(canvas);
        var ctx = canvas.getContext('2d');
        ctx.scale(dpr, dpr);
        ctx.translate(0, -k * TILE);
        drawTile(view, ctx, k * TILE, k * TILE + tileH);
    }
}

function drawTile(view, ctx, y0, y1) {
    var s = view.scale;
    var tMin = Math.max(0, (y0 - 60) / s.pps);
    var tMax = (y1 + 60) / s.pps;

    ctx.fillStyle = 'rgba(255,255,255,.03)';
    ctx.fillRect(s.gutter, y0, view.stripW, y1 - y0);
    ctx.lineWidth = 1;
    for (var c = 0; c <= view.panels; c++) {
        ctx.strokeStyle = view.panels === 10 && c === 5 ? 'rgba(255,255,255,.16)' : 'rgba(255,255,255,.06)';
        ctx.beginPath();
        ctx.moveTo(s.gutter + c * s.colW + 0.5, y0);
        ctx.lineTo(s.gutter + c * s.colW + 0.5, y1);
        ctx.stroke();
    }

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
        var bw = s.arrow * 0.62;
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
}

// Mode-aware colors; Feet and Timing arrive with the mode toggle and read the same seams.
function rowColorFor(view, row, panel) {
    return noteColor(view, row, panel);
}

function holdColorFor(view, hold) {
    return holdColor(view, hold);
}

// Correct outward directions (workshop round 2): canvas rotation is clockwise from "up", so
// UL=-45°, UR=+45°, DR=+135°, DL=-135°. Lane = panel % 5 in pad order DL UL C UR DR.
var ANGLES = { 0: -135, 1: -45, 3: 45, 4: 135 };

function drawArrow(ctx, x, y, size, panel, color) {
    var lane = panel % 5;
    ctx.save();
    ctx.translate(x, y);
    ctx.fillStyle = color;
    if (lane === 2) {
        ctx.rotate(Math.PI / 4);
        var r = size * 0.4;
        roundRect(ctx, -r, -r, r * 2, r * 2, size * 0.16);
        ctx.fill();
        ctx.strokeStyle = 'rgba(255,255,255,.4)';
        ctx.lineWidth = 1.3;
        ctx.stroke();
    } else {
        ctx.rotate(ANGLES[lane] * Math.PI / 180);
        var half = size * 0.5;
        ctx.beginPath();
        ctx.moveTo(0, -half);
        ctx.lineTo(half * 0.85, half * 0.15);
        ctx.lineTo(half * 0.33, half * 0.15);
        ctx.lineTo(half * 0.33, half);
        ctx.lineTo(-half * 0.33, half);
        ctx.lineTo(-half * 0.33, half * 0.15);
        ctx.lineTo(-half * 0.85, half * 0.15);
        ctx.closePath();
        ctx.fill();
        ctx.strokeStyle = 'rgba(255,255,255,.4)';
        ctx.lineWidth = 1.3;
        ctx.stroke();
    }
    ctx.restore();
}

function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(x, y, w, h, r); else ctx.rect(x, y, w, h);
}

function initMinimap(view, compact) {
    var canvas = view.root.querySelector('[data-stepchart-minimap]');
    if (!canvas || compact) {
        if (canvas && canvas.parentElement) canvas.parentElement.hidden = true;
        return;
    }

    var W = 92, H = 600;
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

    view.repaintMinimap = function () {
        ctx.clearRect(0, 0, W, H);
        ctx.fillStyle = 'rgba(255,255,255,.14)';
        for (var second = 0; second < seconds; second++) {
            var w = (density[second] / maxDensity) * 36;
            if (w > 0) ctx.fillRect(6, yOf(second), w, Math.max(1, (H - 8) / view.duration));
        }
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

function cruxOf(view) {
    var crux = null;
    view.segments.forEach(function (segment) {
        if (segment.enps == null) return;
        if (!crux || segment.enps > crux.enps) crux = segment;
    });
    return crux;
}

function buildChips(view) {
    var host = view.root.querySelector('[data-stepchart-chips]');
    if (!host) return;
    host.innerHTML = '';

    var crux = cruxOf(view);
    if (crux) addChip(view, host, 'struct', view.strings.crux || 'Crux', crux.s);
    view.ranges.slice(0, 2).forEach(function (range) {
        if (crux && Math.abs(range.s - crux.s) < 3) return;
        addChip(view, host, 'struct', view.strings.range || 'Notable run', range.s);
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
    legendEntry(host, view.colors.upper, view.strings.upper || 'Upper');
    legendEntry(host, view.colors.lower, view.strings.lower || 'Lower');
    legendEntry(host, view.colors.center, view.strings.center || 'Center');
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

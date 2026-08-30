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
        footL: token('--foot-l'), footR: token('--foot-r'),
        quant: { 4: token('--quant-4'), 8: token('--quant-8'), 12: token('--quant-12'), 16: token('--quant-16') },
        quantOther: token('--quant-other'),
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

    // Spacing tracks how the chart is actually read (owner, 2026-08-30): players run ~300 AV
    // on a 1, ~450 by 10, ~600 by 16, capping ~700 at 23+. AV is pixels-per-second up to a
    // constant, so the strip scrolls at the level's reading speed — a 24's sixteenths spread
    // out instead of soup, a 3's quarters stop floating in space. Level beats NPS here
    // because AV is chosen per level, and an averaged NPS lies about marathon charts.
    var level = parseInt(root.getAttribute('data-level') || '0', 10);
    var av = assistVelocity(level);
    var scale = compact
        ? { pps: av * 110 / 600, colW: 30, gutter: 44, railW: 78, arrow: 19 }
        : { pps: av / 3, colW: panels === 10 ? 40 : 46, gutter: 52, railW: 96, arrow: panels === 10 ? 25 : 27 };
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
    initModes(view);

    if (root.getAttribute('data-visibility') === 'StepsOnly') {
        var caveat = root.querySelector('[data-stepchart-caveat]');
        if (caveat) caveat.hidden = false;
    }

    if (root.getAttribute('data-visibility') === 'Full') await loadBreaks(view, chartId, mix);

    // Land on the crux rather than the silent intro — the reader came to see the chart's teeth.
    var crux = cruxOf(view);
    if (crux) box.scrollTop = Math.max(0, yOf(crux.s) - 90);
}

// The owner's AV ramp, linear between the anchor levels.
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

    drawRail(view, ctx, y0, y1);
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
        var y = view.yOf(pin.t);
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
    if (!breaks || breaks.total === 0) return;

    view.colors.life = view.token('--life-danger');
    view.colors.pass = view.token('--step-pass');
    view.colors.you = view.token('--step-you');
    view.breaks = breaks;

    var lifeX = view.railX + (view.scale.railW >= 96 ? 24 : 18);
    var passX = view.railX + (view.scale.railW >= 96 ? 56 : 44);
    view.pinMarks = breaks.pins.map(function (pin) {
        return {
            t: pin.t, n: pin.n, from: pin.from, to: pin.to, cause: pin.cause,
            cmds: pin.cmds || [],
            x: pin.cause === 'pass' ? passX : lifeX,
            color: pin.cause === 'pass' ? view.colors.pass : view.colors.life
        };
    });

    view.drawMinimapPins = function (ctx, yOf) {
        view.pinMarks.forEach(function (pin) {
            ctx.fillStyle = pin.color;
            ctx.fillRect(48 + (pin.cause === 'pass' ? 20 : 0), yOf(pin.t) - 1,
                Math.max(3, Math.min(pin.n * 4, 18)), 3);
        });
    };

    drawStrip(view);
    if (view.repaintMinimap) view.repaintMinimap();
    buildChips(view);
    renderLegend(view);
    bindPinTips(view);
}

// The three coloring modes (design doc D12), one seam. Arrows reads the panel; Feet reads
// the snapshot's limb masks; Timing reads the quantization the .ssc alignment attached.
function rowColorFor(view, row, panel) {
    if (view.mode === 'foot')
        return (row.l & (1 << panel)) ? view.colors.footL : view.colors.footR;
    if (view.mode === 'quant')
        return view.colors.quant[row.q] || view.colors.quantOther;
    return noteColor(view, row, panel);
}

function holdColorFor(view, hold) {
    if (view.mode === 'foot') return hold.left ? view.colors.footL : view.colors.footR;
    // Hold heads carry no quantization of their own — in Timing mode they read as "other".
    if (view.mode === 'quant') return view.colors.quantOther;
    return holdColor(view, hold);
}

// The toggle: three buttons the server rendered, localStorage for stickiness (per ruling 6:
// the lightest thing that behaves well — a static page writes no per-user settings), and
// Timing disabled outright on charts whose beats never aligned (D6).
function initModes(view) {
    var buttons = Array.prototype.slice.call(view.root.querySelectorAll('[data-stepchart-mode]'));
    if (buttons.length === 0) return;

    var timingAvailable = !!view.payload.aligned;
    var saved = null;
    try { saved = window.localStorage.getItem('stepchart-mode'); } catch (e) { /* private mode */ }
    if (saved === 'foot' || (saved === 'quant' && timingAvailable)) view.mode = saved;

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
        if (mode === 'quant' && !timingAvailable) {
            button.disabled = true;
            button.title = view.strings.timingUnavailable ||
                'Timing colors need a step file that aligned to beats.';
            return;
        }

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

    // Structure first, then the two biggest death clusters by count (design doc D17).
    if (view.pinMarks) {
        view.pinMarks.slice().sort(function (a, b) { return b.n - a.n; })
            .filter(function (pin) { return pin.n >= 3; })
            .slice(0, 2)
            .forEach(function (pin) {
                addChip(view, host, pin.cause === 'pass' ? 'pass' : 'life',
                    pin.cause === 'pass'
                        ? (view.strings.passCluster || 'Pass cluster')
                        : (view.strings.deathSpike || 'Death spike'),
                    pin.t, pin.n);
            });
    }
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
    } else if (view.mode === 'quant') {
        legendEntry(host, view.colors.quant[4], view.strings.quarters || 'Quarters');
        legendEntry(host, view.colors.quant[8], view.strings.eighths || 'Eighths');
        legendEntry(host, view.colors.quant[12], view.strings.twelfths || 'Twelfths');
        legendEntry(host, view.colors.quant[16], view.strings.sixteenths || 'Sixteenths');
        legendEntry(host, view.colors.quantOther, view.strings.finer || 'Finer');
    } else {
        legendEntry(host, view.colors.upper, view.strings.upper || 'Upper');
        legendEntry(host, view.colors.lower, view.strings.lower || 'Lower');
        legendEntry(host, view.colors.center, view.strings.center || 'Center');
    }

    if (view.breaks && view.breaks.total > 0) {
        legendEntry(host, view.colors.life,
            (view.strings.lifeBreak || 'Life Bar Break') + ' \u00b7 ' + view.breaks.life);
        if (view.breaks.pass > 0)
            legendEntry(host, view.colors.pass,
                (view.strings.stagePass || 'Stage Pass') + ' \u00b7 ' + view.breaks.pass);
        if (view.breaks.yours.length > 0)
            legendEntry(host, view.colors.you,
                (view.strings.yourRuns || 'Your runs') + ' \u00b7 ' + view.breaks.yours.length);
    }
}

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
            var dy = y - view.yOf(pin.t);
            if (dx * dx + dy * dy <= r * r) { hit = pin; break; }
        }

        if (!hit) { tip.hidden = true; return; }
        // Terse on purpose (owner, 2026-08-30): the badge art IS the sentence for a named
        // Pass, and everything else is three words plus a count.
        var range = hit.n > 1 ? fmt(hit.from) + '\u2013' + fmt(hit.to) : fmt(hit.t);
        var count = hit.n > 1 ? ' \u00d7' + hit.n : '';
        var body;
        if (hit.cause === 'pass' && hit.cmds.length) {
            body = hit.cmds.map(function (cmd) {
                return '<img class="stepchart-tip-cmd" alt="' + escapeHtml(cmd) +
                    '" src="https://piuimages.arroweclip.se/commands/' + encodeURI(cmd) + '.png">';
            }).join('') + escapeHtml(count);
        } else if (hit.cause === 'pass') {
            body = '<b>' + escapeHtml((view.strings.unknownBreak || 'Unknown Break') + count) + '</b>';
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

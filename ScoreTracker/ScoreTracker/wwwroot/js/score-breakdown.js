// One implementation of the Phoenix score story, shared by its two surfaces: the Score
// Calculator page (live, as counts are typed) and the Score Breakdown Dialog (once, for a
// recorded play). The factory closes over the engine-emitted constants block, so nothing
// here can disagree with the server that rendered the page — and because both surfaces call
// the same renderers, they cannot drift from each other (session-breakdown.md §7.3).
//
// The score/plate/walk arithmetic mirrors ScoreScreen and is pinned there. Markup classes
// (sc-bar*, judg-*) ship in site.css; the labels are the CALLER's, so each surface localizes
// through its own channel and this module stays string-free.

export function createScoreBreakdown(constants, mix, lang) {
    'use strict';

    var CONST = constants;

    function n0(v) { return Math.round(v).toLocaleString(lang); }
    function fmt(template) {
        var args = Array.prototype.slice.call(arguments, 1);
        return template.replace(/\{(\d+)\}/g, function (_, i) { return args[+i] === undefined ? '' : args[+i]; });
    }

    // ---- the formula, mirrored from ScoreScreen (floor; combo term at half a percent).
    function score(p, gr, gd, bd, m, c) {
        var total = p + gr + gd + bd + m;
        if (total <= 0 || total >= 10000 || c > total || c < 0) return null;
        var w = CONST.weights;
        return Math.floor(
            (w.accuracy * (p + w.great * gr + w.good * gd + w.bad * bd) + w.combo * c) / total * 1000000);
    }

    function gradeFor(value, forMix) {
        var floors = CONST.floors[forMix || mix];
        for (var i = 0; i < floors.length; i++)
            if (value >= floors[i].floor) return floors[i];
        return floors[floors.length - 1];
    }

    function plateFor(gr, gd, bd, m) {
        if (gr === 0 && gd === 0 && bd === 0 && m === 0) return 'Perfect Game';
        if (gd === 0 && bd === 0 && m === 0) return 'Ultimate Game';
        if (bd === 0 && m === 0) return 'Extreme Game';
        if (m === 0) return 'Superb Game';
        if (m <= 5) return 'Marvelous Game';
        if (m <= 10) return 'Talented Game';
        if (m <= 20) return 'Fair Game';
        return 'Rough Game';
    }

    // Deterministic per input: the walk reseeds so retelling the same screen retells the same
    // path (mulberry32; production's stream-seeded Random differs per call, same idea).
    function mulberry32(seed) {
        var a = seed | 0;
        return function () {
            a = a + 0x6D2B79F5 | 0;
            var t = Math.imul(a ^ a >>> 15, 1 | a);
            t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
            return ((t ^ t >>> 14) >>> 0) / 4294967296;
        };
    }

    // The site's weighted-random walk (ScoreScreen.IterateWithWeightedRandom): one non-perfect
    // converts per step, chosen in proportion to how many the player actually got, so the
    // recipe auto-scales to their own error mix — a goods-heavy play is told "fewer goods".
    // labels: { label, points, best, get, morePerfects, fewerMisses, fewerBads, fewerGoods,
    // fewerGreats, moreCombo }.
    function renderNextLetter(next, s, labels) {
        var current = gradeFor(s.score);
        if (current.grade === 'SSS+') { next.textContent = labels.best; return; }
        var floors = CONST.floors[mix];
        var target = null;
        for (var i = floors.length - 1; i >= 0; i--) if (floors[i].floor > s.score) { target = floors[i]; break; }
        if (!target) { next.textContent = labels.best; return; }

        var rng = mulberry32(1949);
        var w = { p: s.p, gr: s.gr, gd: s.gd, bd: s.bd, m: s.m, c: s.c };
        var guard = 0;
        while (guard++ < 200000) {
            var sc = score(w.p, w.gr, w.gd, w.bd, w.m, w.c);
            if (sc === null || gradeFor(sc).grade !== current.grade) break;
            var total = w.m + w.bd + w.gd + w.gr;
            if (total <= 0) { if (w.c >= w.p + w.gr) { guard = 1e9; break; } w.c++; continue; }
            var pick = Math.floor(rng() * total) + 1;
            if (pick > total - w.gr) { w.gr--; w.p++; }
            else if (pick > total - w.gr - w.gd) { w.gd--; w.p++; w.c++; }
            else if (pick > total - w.gr - w.gd - w.bd) { w.bd--; w.p++; w.c++; }
            else { w.m--; w.p++; w.c++; }
        }
        var label = '<span class="sc-next-label">' + labels.label + ' · ' +
            fmt(labels.points, n0(target.floor - s.score)) + '</span>';
        if (guard >= 200000) { next.innerHTML = label; return; }
        var got = gradeFor(score(w.p, w.gr, w.gd, w.bd, w.m, w.c)).grade;
        var parts = [];
        if (w.p > s.p) parts.push(fmt(labels.morePerfects, '<b>' + (w.p - s.p) + '</b>'));
        if (s.m > w.m) parts.push(fmt(labels.fewerMisses, '<b>' + (s.m - w.m) + '</b>'));
        if (s.bd > w.bd) parts.push(fmt(labels.fewerBads, '<b>' + (s.bd - w.bd) + '</b>'));
        if (s.gd > w.gd) parts.push(fmt(labels.fewerGoods, '<b>' + (s.gd - w.gd) + '</b>'));
        if (s.gr > w.gr) parts.push(fmt(labels.fewerGreats, '<b>' + (s.gr - w.gr) + '</b>'));
        var comboGain = (s.m - w.m) + (s.bd - w.bd) + (s.gd - w.gd);
        if (comboGain > 0) parts.push(fmt(labels.moreCombo, '<b>' + comboGain + '</b>'));
        next.innerHTML = label +
            fmt(labels.get, '<b>' + got + '</b>') + ' ' + parts.join(', ') + '!';
    }

    function segment(cls, left, width, tip) {
        return '<span class="sc-barseg ' + cls + '" data-sc-tip="' + tip.replace(/"/g, '&quot;') +
            '" style="left:' + left + '%;width:' + width + '%"></span>';
    }

    // Both attribution bars: where the score came from and where the loss went — different
    // questions, deliberately both (D44). labels: { perfects, greats, goods, bads, misses,
    // combo, brokenCombo, gained, lost, notEarned, nothingLost, clipped }.
    function renderBars(el, s, labels) {
        var w = CONST.weights;
        var total = s.p + s.gr + s.gd + s.bd + s.m;
        var pts = function (count, weight) { return w.accuracy * weight * count / total * 1000000; };
        // [class, label, points, count] — the bar is sized by POINTS, and a bad pays half a
        // good per note, so the count rides every tooltip to keep the sliver legible.
        var gained = [
            ['judg-perfect', labels.perfects, pts(s.p, 1), s.p],
            ['judg-great', labels.greats, pts(s.gr, w.great), s.gr],
            ['judg-good', labels.goods, pts(s.gd, w.good), s.gd],
            ['judg-bad', labels.bads, pts(s.bd, w.bad), s.bd],
            ['sc-seg-combo', labels.combo, w.combo * s.c / total * 1000000, s.c]
        ];
        var lost = [
            ['judg-great', labels.greats, pts(s.gr, 1 - w.great), s.gr],
            ['judg-good', labels.goods, pts(s.gd, 1 - w.good), s.gd],
            ['judg-bad', labels.bads, pts(s.bd, 1 - w.bad), s.bd],
            ['judg-miss', labels.misses, pts(s.m, 1), s.m],
            ['sc-seg-combo', labels.brokenCombo, w.combo * (total - s.c) / total * 1000000, total - s.c]
        ];
        var lo = Math.min(Math.floor(s.score / 100000), 9) * 100000;
        var span = 1000000 - lo;
        var acc = 0;
        var segs1 = '';
        gained.forEach(function (g) {
            var a = Math.max(acc, lo);
            var b = Math.min(acc + g[2], 1000000);
            if (b > a) {
                var clipped = acc < lo;
                segs1 += segment(g[0] + (clipped ? ' sc-clip' : ''), (a - lo) / span * 100, (b - a) / span * 100,
                    g[1] + ' ×' + n0(g[3]) + ' — +' + n0(g[2]) +
                    (clipped ? ' (' + labels.clipped + ')' : ''));
            }
            acc += g[2];
        });
        var missing = 1000000 - s.score;
        var leg1 = gained.filter(function (g) { return g[2] >= .5; }).map(function (g) {
            return '<span class="sc-it"><i class="sc-sw sc-barseg ' + g[0] +
                '" style="position:static;width:9px;height:9px;border-radius:50%"></i>' +
                g[1] + ' <b>+' + n0(g[2]) + '</b></span>';
        }).join('');
        if (missing > 0)
            leg1 += '<span class="sc-it"><i class="sc-sw sc-sw-empty"></i>' +
                labels.notEarned + ' <b>' + n0(missing) + '</b></span>';

        var totalLost = 0;
        lost.forEach(function (l) { totalLost += l[2]; });
        var segs2 = '';
        var acc2 = 0;
        if (totalLost > 0)
            lost.forEach(function (l) {
                if (l[2] <= 0) return;
                segs2 += segment(l[0], acc2 / totalLost * 100, Math.max(l[2] / totalLost * 100 - .3, .3),
                    l[1] + ' ×' + n0(l[3]) + ' — −' + n0(l[2]) + ' / ' + n0(totalLost) +
                    ' (' + Math.round(l[2] / totalLost * 100) + '%)');
                acc2 += l[2];
            });
        var leg2 = totalLost > 0
            ? lost.filter(function (l) { return l[2] >= .5; }).map(function (l) {
                return '<span class="sc-it"><i class="sc-sw sc-barseg ' + l[0] +
                    '" style="position:static;width:9px;height:9px;border-radius:50%"></i>' +
                    l[1] + ' <b>−' + n0(l[2]) + '</b></span>';
            }).join('')
            : '<span class="sc-it">' + labels.nothingLost + '</span>';

        el.innerHTML =
            '<div class="sc-bar"><h4>' + labels.gained +
            '<span class="sc-range">' + n0(lo) + ' → 1,000,000</span></h4>' +
            '<div class="sc-bartrack">' + segs1 + '</div>' +
            '<div class="sc-barlegend">' + leg1 + '</div></div>' +
            '<div class="sc-bar"><h4>' + labels.lost +
            '<span class="sc-range">0 → ' + n0(totalLost) + '</span></h4>' +
            '<div class="sc-bartrack">' + segs2 + '</div>' +
            '<div class="sc-barlegend">' + leg2 + '</div></div>';
    }

    return {
        score: score,
        gradeFor: gradeFor,
        plateFor: plateFor,
        renderBars: renderBars,
        renderNextLetter: renderNextLetter
    };
}

// ---- one tooltip for every [data-sc-tip] carrier under the given root (chart hit areas,
// bar segments). The tip element is shared: a page hosting both the calculator's charts and
// a breakdown dialog gets one tip, never a stack of them.
var tip = null;

export function attachBreakdownTips(root) {
    if (!tip) {
        tip = document.createElement('div');
        tip.className = 'sc-tip';
        tip.hidden = true;
        document.body.appendChild(tip);
    }
    function move(e) {
        var carrier = e.target.closest('[data-sc-tip]');
        if (!carrier) { tip.hidden = true; return; }
        tip.textContent = carrier.getAttribute('data-sc-tip');
        tip.hidden = false;
        tip.style.left = Math.min(e.clientX + 14, window.innerWidth - 310) + 'px';
        tip.style.top = Math.min(e.clientY + 14, window.innerHeight - 90) + 'px';
    }
    function leave() { tip.hidden = true; }
    root.addEventListener('mousemove', move);
    root.addEventListener('mouseleave', leave);
    return function () {
        root.removeEventListener('mousemove', move);
        root.removeEventListener('mouseleave', leave);
        tip.hidden = true;
    };
}

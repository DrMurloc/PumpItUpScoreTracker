// The Phoenix score page's moving parts (docs/design/phoenix-score-calculator.md D2), on a page
// that is otherwise static HTML: the live calculator with its two attribution bars and the
// weighted-random next-letter walk, the chart-size chips, the Singles/Doubles toggle, chart
// tooltips, and the plays dialog. Every constant this script reads — both mixes' grade floors,
// the judgement weights, the owner-verified calorie table — comes from the JSON block the
// server emitted from the engine, so nothing here can disagree with the page it sits under.
// The score/plate/walk arithmetic mirrors ScoreScreen and is pinned there; without this script
// the page loses the tool and keeps every fact.
(function () {
    'use strict';

    var page = document.querySelector('[data-sc-page]');
    if (!page) return;

    var constantsBlock = page.querySelector('[data-sc-constants]');
    if (!constantsBlock) return;
    var CONST = JSON.parse(constantsBlock.textContent);
    var MIX = page.getAttribute('data-sc-mix');
    var OTHER_MIX = MIX === 'Phoenix2' ? 'Phoenix' : 'Phoenix2';
    var lang = document.documentElement.lang || undefined;

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

    function gradeFor(value, mix) {
        var floors = CONST.floors[mix];
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

    // The metal each letter and plate wears, as the site's tokens — classes stay in the
    // stylesheet, but chip backgrounds are set inline from these custom-property names.
    var GRADE_TOKEN = {
        'SSS+': '--grade-sssplus', 'SSS': '--grade-sss', 'SS+': '--grade-ssplus', 'SS': '--grade-ss',
        'S+': '--grade-splus', 'S': '--grade-s', 'AAA+': '--grade-aaaplus', 'AAA': '--grade-aaa',
        'AA+': '--grade-aaplus', 'AA': '--grade-aa', 'A+': '--grade-aplus', 'A': '--grade-a',
        'B': '--grade-b', 'C': '--grade-c', 'D': '--grade-d', 'F': '--grade-f'
    };
    var PLATE_TOKEN = {
        'Perfect Game': '--plate-pg', 'Ultimate Game': '--plate-ug', 'Extreme Game': '--plate-eg',
        'Superb Game': '--plate-sg', 'Marvelous Game': '--plate-mg', 'Talented Game': '--plate-tg',
        'Fair Game': '--plate-fg', 'Rough Game': '--plate-rg'
    };

    // ---- the calculator.
    var calc = page.querySelector('[data-sc-calc]');
    var inputs = {};
    calc.querySelectorAll('[data-sc-in]').forEach(function (el) { inputs[el.getAttribute('data-sc-in')] = el; });
    function out(name) { return calc.querySelector('[data-sc-out="' + name + '"]'); }

    function readInt(name) {
        var v = parseInt(inputs[name].value, 10);
        return isNaN(v) ? 0 : Math.abs(v);
    }

    // Deterministic per input: the walk reseeds so retyping the same screen retells the same
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
    function nextLetter(next, s) {
        var current = gradeFor(s.score, MIX);
        if (current.grade === 'SSS+') { next.textContent = next.getAttribute('data-t-best'); return; }
        var floors = CONST.floors[MIX];
        var target = null;
        for (var i = floors.length - 1; i >= 0; i--) if (floors[i].floor > s.score) { target = floors[i]; break; }
        if (!target) { next.textContent = next.getAttribute('data-t-best'); return; }

        var rng = mulberry32(1949);
        var w = { p: s.p, gr: s.gr, gd: s.gd, bd: s.bd, m: s.m, c: s.c };
        var guard = 0;
        while (guard++ < 200000) {
            var sc = score(w.p, w.gr, w.gd, w.bd, w.m, w.c);
            if (sc === null || gradeFor(sc, MIX).grade !== current.grade) break;
            var total = w.m + w.bd + w.gd + w.gr;
            if (total <= 0) { if (w.c >= w.p + w.gr) { guard = 1e9; break; } w.c++; continue; }
            var pick = Math.floor(rng() * total) + 1;
            if (pick > total - w.gr) { w.gr--; w.p++; }
            else if (pick > total - w.gr - w.gd) { w.gd--; w.p++; w.c++; }
            else if (pick > total - w.gr - w.gd - w.bd) { w.bd--; w.p++; w.c++; }
            else { w.m--; w.p++; w.c++; }
        }
        var label = '<span class="sc-next-label">' + next.getAttribute('data-t-label') + ' · ' +
            fmt(next.getAttribute('data-t-points'), n0(target.floor - s.score)) + '</span>';
        if (guard >= 200000) { next.innerHTML = label; return; }
        var got = gradeFor(score(w.p, w.gr, w.gd, w.bd, w.m, w.c), MIX).grade;
        var parts = [];
        if (w.p > s.p) parts.push(fmt(next.getAttribute('data-t-more-perfects'), '<b>' + (w.p - s.p) + '</b>'));
        if (s.m > w.m) parts.push(fmt(next.getAttribute('data-t-fewer-misses'), '<b>' + (s.m - w.m) + '</b>'));
        if (s.bd > w.bd) parts.push(fmt(next.getAttribute('data-t-fewer-bads'), '<b>' + (s.bd - w.bd) + '</b>'));
        if (s.gd > w.gd) parts.push(fmt(next.getAttribute('data-t-fewer-goods'), '<b>' + (s.gd - w.gd) + '</b>'));
        if (s.gr > w.gr) parts.push(fmt(next.getAttribute('data-t-fewer-greats'), '<b>' + (s.gr - w.gr) + '</b>'));
        var comboGain = (s.m - w.m) + (s.bd - w.bd) + (s.gd - w.gd);
        if (comboGain > 0) parts.push(fmt(next.getAttribute('data-t-more-combo'), '<b>' + comboGain + '</b>'));
        next.innerHTML = label +
            fmt(next.getAttribute('data-t-get'), '<b>' + got + '</b>') + ' ' + parts.join(', ') + '!';
    }

    function segment(cls, left, width, tip) {
        return '<span class="sc-barseg ' + cls + '" data-sc-tip="' + tip.replace(/"/g, '&quot;') +
            '" style="left:' + left + '%;width:' + width + '%"></span>';
    }

    function bars(el, s) {
        var w = CONST.weights;
        var total = s.p + s.gr + s.gd + s.bd + s.m;
        var pts = function (count, weight) { return w.accuracy * weight * count / total * 1000000; };
        var gained = [
            ['judg-perfect', inputs.perfects.previousElementSibling.textContent.trim(), pts(s.p, 1)],
            ['judg-great', inputs.greats.previousElementSibling.textContent.trim(), pts(s.gr, w.great)],
            ['judg-good', inputs.goods.previousElementSibling.textContent.trim(), pts(s.gd, w.good)],
            ['judg-bad', inputs.bads.previousElementSibling.textContent.trim(), pts(s.bd, w.bad)],
            ['sc-seg-combo', inputs.combo.previousElementSibling.textContent.trim(), w.combo * s.c / total * 1000000]
        ];
        var lost = [
            ['judg-great', inputs.greats.previousElementSibling.textContent.trim(), pts(s.gr, 1 - w.great)],
            ['judg-good', inputs.goods.previousElementSibling.textContent.trim(), pts(s.gd, 1 - w.good)],
            ['judg-bad', inputs.bads.previousElementSibling.textContent.trim(), pts(s.bd, 1 - w.bad)],
            ['judg-miss', inputs.misses.previousElementSibling.textContent.trim(), pts(s.m, 1)],
            ['sc-seg-combo', el.getAttribute('data-t-broken-combo'), w.combo * (total - s.c) / total * 1000000]
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
                    g[1] + ' — ' + n0(g[2]) + (clipped ? ' (' + el.getAttribute('data-t-clipped') + ')' : ''));
            }
            acc += g[2];
        });
        var missing = 1000000 - s.score;
        var leg1 = gained.filter(function (g) { return g[2] >= .5; }).map(function (g) {
            return '<span class="sc-it"><i class="sc-sw sc-barseg ' + g[0] +
                '" style="position:static;width:9px;height:9px;border-radius:50%"></i>' +
                g[1] + ' <b>' + n0(g[2]) + '</b></span>';
        }).join('');
        if (missing > 0)
            leg1 += '<span class="sc-it"><i class="sc-sw sc-sw-empty"></i>' +
                el.getAttribute('data-t-not-earned') + ' <b>' + n0(missing) + '</b></span>';

        var totalLost = 0;
        lost.forEach(function (l) { totalLost += l[2]; });
        var segs2 = '';
        var acc2 = 0;
        if (totalLost > 0)
            lost.forEach(function (l) {
                if (l[2] <= 0) return;
                segs2 += segment(l[0], acc2 / totalLost * 100, Math.max(l[2] / totalLost * 100 - .3, .3),
                    l[1] + ' — ' + n0(l[2]) + ' / ' + n0(totalLost) + ' (' + Math.round(l[2] / totalLost * 100) + '%)');
                acc2 += l[2];
            });
        var leg2 = totalLost > 0
            ? lost.filter(function (l) { return l[2] >= .5; }).map(function (l) {
                return '<span class="sc-it"><i class="sc-sw sc-barseg ' + l[0] +
                    '" style="position:static;width:9px;height:9px;border-radius:50%"></i>' +
                    l[1] + ' <b>−' + n0(l[2]) + '</b></span>';
            }).join('')
            : '<span class="sc-it">' + el.getAttribute('data-t-nothing-lost') + '</span>';

        el.innerHTML =
            '<div class="sc-bar"><h4>' + el.getAttribute('data-t-gained') +
            '<span class="sc-range">' + n0(lo) + ' → 1,000,000</span></h4>' +
            '<div class="sc-bartrack">' + segs1 + '</div>' +
            '<div class="sc-barlegend">' + leg1 + '</div></div>' +
            '<div class="sc-bar"><h4>' + el.getAttribute('data-t-lost') +
            '<span class="sc-range">0 → ' + n0(totalLost) + '</span></h4>' +
            '<div class="sc-bartrack">' + segs2 + '</div>' +
            '<div class="sc-barlegend">' + leg2 + '</div></div>';
    }

    function calorieSteps(calories, total) {
        var perStep = .0621;
        if (total <= 700) {
            var bucket = 12;
            for (var i = 0; i < CONST.calorieThresholds.length; i++)
                if (CONST.calorieThresholds[i][0] >= total) { bucket = CONST.calorieThresholds[i][1]; break; }
            perStep = .035 + .0023 * bucket;
        }
        return calories / perStep;
    }

    function markSpreadRow(gradeName) {
        page.querySelectorAll('[data-sc-spread-grade]').forEach(function (row) {
            row.classList.toggle('sc-you', row.getAttribute('data-sc-spread-grade') === gradeName);
        });
    }

    function recalc() {
        var s = {
            p: readInt('perfects'), gr: readInt('greats'), gd: readInt('goods'),
            bd: readInt('bads'), m: readInt('misses'), c: readInt('combo')
        };
        var value = score(s.p, s.gr, s.gd, s.bd, s.m, s.c);
        var gradeChip = out('grade');
        var plateChip = out('plate');
        var barsEl = out('bars');
        var nextEl = out('next');
        var calEl = out('calories');
        if (value === null) {
            out('score').textContent = '—';
            gradeChip.hidden = plateChip.hidden = barsEl.hidden = nextEl.hidden = calEl.hidden = true;
            out('crossmix').textContent = '';
            markSpreadRow(null);
            return;
        }
        s.score = value;
        out('score').textContent = n0(value);
        var grade = gradeFor(value, MIX);
        gradeChip.textContent = grade.grade;
        gradeChip.style.background = 'var(' + GRADE_TOKEN[grade.grade] + ')';
        gradeChip.hidden = false;
        var plate = plateFor(s.gr, s.gd, s.bd, s.m);
        plateChip.textContent = plate;
        plateChip.style.background = 'var(' + PLATE_TOKEN[plate] + ')';
        plateChip.hidden = false;
        var other = gradeFor(value, OTHER_MIX);
        var cross = out('crossmix');
        cross.textContent = other.grade !== grade.grade
            ? fmt(cross.getAttribute('data-t-other'), cross.getAttribute('data-other-mix'), other.grade)
            : '';
        nextEl.hidden = false;
        nextLetter(nextEl, s);
        barsEl.hidden = false;
        bars(barsEl, s);
        var calories = parseFloat(inputs.calories.value);
        if (!isNaN(calories) && calories > 0) {
            calEl.innerHTML = fmt(calEl.getAttribute('data-t-arrows'),
                '<b>' + n0(calorieSteps(Math.abs(calories), s.p + s.gr + s.gd + s.bd + s.m)) + '</b>');
            calEl.hidden = false;
        } else {
            calEl.hidden = true;
        }
        markSpreadRow(grade.grade);
    }

    Object.keys(inputs).forEach(function (name) { inputs[name].addEventListener('input', recalc); });

    // ---- the chart-size chips reprice the cost cards and the budget table.
    var sizeGroup = page.querySelector('[data-sc-sizes]');
    if (sizeGroup) {
        function costOf(kind, notes) {
            // Best-case combos, mirroring the model: greats keep the run, a good tops out one
            // short, a bad or an edge miss breaks one note off it.
            if (kind === 'great') return 1000000 - score(notes - 1, 1, 0, 0, 0, notes);
            if (kind === 'good') return 1000000 - score(notes - 1, 0, 1, 0, 0, notes - 1);
            if (kind === 'bad') return 1000000 - score(notes - 1, 0, 0, 1, 0, notes - 1);
            if (kind === 'miss') return 1000000 - score(notes - 1, 0, 0, 0, 1, notes - 1);
            return 1000000 - score(notes - 1, 0, 0, 0, 1, Math.floor((notes - 1) / 2));
        }
        function budgetFor(floor, notes) {
            var low = 0, high = notes;
            while (low < high) {
                var candidate = Math.floor((low + high + 1) / 2);
                if (score(notes - candidate, candidate, 0, 0, 0, notes) >= floor) low = candidate;
                else high = candidate - 1;
            }
            return low;
        }
        sizeGroup.addEventListener('click', function (e) {
            var chip = e.target.closest('[data-sc-size]');
            if (!chip) return;
            var notes = parseInt(chip.getAttribute('data-sc-size'), 10);
            sizeGroup.querySelectorAll('[data-sc-size]').forEach(function (b) {
                b.setAttribute('aria-pressed', String(b === chip));
            });
            ['great', 'good', 'bad', 'miss'].forEach(function (kind) {
                page.querySelector('[data-sc-cost="' + kind + '"]').textContent = '−' + n0(costOf(kind, notes));
            });
            var missMid = page.querySelector('[data-sc-cost="missmid"]');
            missMid.textContent = fmt(missMid.getAttribute('data-t'), n0(costOf('missmid', notes)));
            var missNote = page.querySelector('[data-sc-cost="missnote"]');
            missNote.textContent = fmt(missNote.getAttribute('data-t'),
                n0(notes), n0(costOf('missmid', notes)), n0(costOf('miss', notes)));
            page.querySelectorAll('[data-sc-budget]').forEach(function (cell) {
                var grade = cell.getAttribute('data-sc-budget');
                var floors = CONST.floors[MIX];
                for (var i = 0; i < floors.length; i++)
                    if (floors[i].grade === grade) { cell.textContent = n0(budgetFor(floors[i].floor, notes)); break; }
            });
        });
    }

    // ---- the Singles/Doubles toggle over the note-count charts.
    var typeButtons = page.querySelectorAll('[data-sc-type-button]');
    typeButtons.forEach(function (button) {
        button.addEventListener('click', function () {
            var type = button.getAttribute('data-sc-type-button');
            page.querySelectorAll('[data-sc-type]').forEach(function (block) {
                block.hidden = block.getAttribute('data-sc-type') !== type;
            });
            typeButtons.forEach(function (b) {
                b.setAttribute('aria-pressed', String(b === button));
            });
        });
    });

    // ---- one tooltip for every [data-sc-tip] carrier (chart hit areas, bar segments).
    var tip = document.createElement('div');
    tip.className = 'sc-tip';
    tip.hidden = true;
    document.body.appendChild(tip);
    page.addEventListener('mousemove', function (e) {
        var carrier = e.target.closest('[data-sc-tip]');
        if (!carrier) { tip.hidden = true; return; }
        tip.textContent = carrier.getAttribute('data-sc-tip');
        tip.hidden = false;
        tip.style.left = Math.min(e.clientX + 14, window.innerWidth - 310) + 'px';
        tip.style.top = Math.min(e.clientY + 14, window.innerHeight - 90) + 'px';
    });
    page.addEventListener('mouseleave', function () { tip.hidden = true; });
})();

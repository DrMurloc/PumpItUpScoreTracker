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
        // [class, label, points, count] — the bar is sized by POINTS, and a bad pays half a
        // good per note, so the count rides every tooltip to keep the sliver legible.
        var gained = [
            ['judg-perfect', inputs.perfects.previousElementSibling.textContent.trim(), pts(s.p, 1), s.p],
            ['judg-great', inputs.greats.previousElementSibling.textContent.trim(), pts(s.gr, w.great), s.gr],
            ['judg-good', inputs.goods.previousElementSibling.textContent.trim(), pts(s.gd, w.good), s.gd],
            ['judg-bad', inputs.bads.previousElementSibling.textContent.trim(), pts(s.bd, w.bad), s.bd],
            ['sc-seg-combo', inputs.combo.previousElementSibling.textContent.trim(), w.combo * s.c / total * 1000000, s.c]
        ];
        var lost = [
            ['judg-great', inputs.greats.previousElementSibling.textContent.trim(), pts(s.gr, 1 - w.great), s.gr],
            ['judg-good', inputs.goods.previousElementSibling.textContent.trim(), pts(s.gd, 1 - w.good), s.gd],
            ['judg-bad', inputs.bads.previousElementSibling.textContent.trim(), pts(s.bd, 1 - w.bad), s.bd],
            ['judg-miss', inputs.misses.previousElementSibling.textContent.trim(), pts(s.m, 1), s.m],
            ['sc-seg-combo', el.getAttribute('data-t-broken-combo'), w.combo * (total - s.c) / total * 1000000, total - s.c]
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
                    (clipped ? ' (' + el.getAttribute('data-t-clipped') + ')' : ''));
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
                el.getAttribute('data-t-not-earned') + ' <b>' + n0(missing) + '</b></span>';

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
            out('score').classList.add('sc-empty');
            out('hint').hidden = false;
            gradeChip.hidden = plateChip.hidden = barsEl.hidden = nextEl.hidden = calEl.hidden = true;
            out('crossmix').textContent = '';
            markSpreadRow(null);
            return;
        }
        s.score = value;
        out('score').textContent = n0(value);
        out('score').classList.remove('sc-empty');
        out('hint').hidden = true;
        // The letter and plate render as the site's art (LetterGradeIcon / ScoreBreakdown
        // vocabulary) from the URLs the server emitted — never as invented chips.
        var grade = gradeFor(value, MIX);
        gradeChip.src = CONST.gradeImages[grade.grade];
        gradeChip.alt = grade.grade;
        gradeChip.hidden = false;
        var plate = plateFor(s.gr, s.gd, s.bd, s.m);
        plateChip.src = CONST.plateImages[plate];
        plateChip.alt = plate;
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

    // ---- the plays dialog: fetched once from the signed-in MyPlays endpoint, filtered
    // client-side, a row click fills the calculator (design doc D7). Scores render from the
    // same arithmetic the calculator uses, so a filled row and its result always agree.
    var loadButton = page.querySelector('[data-sc-load]');
    var dialog = page.querySelector('[data-sc-dialog]');
    if (loadButton && dialog) {
        var playList = dialog.querySelector('[data-sc-play-list]');
        var playFilter = dialog.querySelector('[data-sc-play-filter]');
        var plays = null;

        function esc(text) {
            return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        }

        function messageRow(text) {
            return '<tr><td style="color:var(--mix-ink-muted)">' + esc(text) + '</td></tr>';
        }

        function renderPlays() {
            if (!plays) { playList.innerHTML = messageRow(playList.getAttribute('data-t-loading')); return; }
            if (plays.length === 0) { playList.innerHTML = messageRow(playList.getAttribute('data-t-empty')); return; }
            var query = playFilter.value.trim().toLowerCase();
            var rows = plays.filter(function (play) {
                return !query || (play.song + ' ' + play.difficulty).toLowerCase().indexOf(query) >= 0;
            }).map(function (play) {
                var value = score(play.perfects, play.greats, play.goods, play.bads, play.misses, play.combo);
                var grade = value === null ? null : gradeFor(value, MIX);
                var plate = plateFor(play.greats, play.goods, play.bads, play.misses);
                // The site's row vocabulary, exactly as SessionScoreRow composes it: the plain
                // jacket, the bubble art (text only where no art exists), and the letter and
                // plate as images — a finished fail wears the broken letter art.
                var gradeImages = play.isBroken ? CONST.gradeImagesBroken : CONST.gradeImages;
                return '<tr data-sc-play="' + play.index + '" tabindex="0">' +
                    '<td><img class="sc-jk" src="' + esc(play.jacket) + '" alt="" loading="lazy"></td>' +
                    '<td>' + (play.bubble
                        ? '<img class="sc-bub" src="' + esc(play.bubble) + '" alt="' + esc(play.difficulty) + '" loading="lazy">'
                        : '<span class="sc-bubtext">' + esc(play.difficulty) + '</span>') + '</td>' +
                    '<td class="sc-pt-name">' + esc(play.song) + '</td>' +
                    '<td class="sc-pt-j">' +
                    '<span class="judg-perfect">' + n0(play.perfects) + '</span><span class="sc-sl">/</span>' +
                    '<span class="judg-great">' + n0(play.greats) + '</span><span class="sc-sl">/</span>' +
                    '<span class="judg-good">' + n0(play.goods) + '</span><span class="sc-sl">/</span>' +
                    '<span class="judg-bad">' + n0(play.bads) + '</span><span class="sc-sl">/</span>' +
                    '<span class="judg-miss">' + n0(play.misses) + '</span><span class="sc-sl"> · </span>' +
                    '<span class="sc-pt-combo">' + n0(play.combo) + '</span></td>' +
                    '<td class="sc-pt-score"><span class="sc-wrap">' +
                    '<span class="sc-n">' + (value === null ? '—' : n0(value)) + '</span>' +
                    '<span class="sc-gstack">' +
                    (grade === null ? '' :
                        '<img class="sc-gstack-grade" src="' + esc(gradeImages[grade.grade]) + '" alt="' + grade.grade + '">') +
                    '<img class="sc-gstack-plate" src="' + esc(CONST.plateImages[plate]) + '" alt="' + esc(plate) + '">' +
                    '</span></span></td></tr>';
            });
            playList.innerHTML = rows.length ? rows.join('') : messageRow(playList.getAttribute('data-t-none-match'));
        }

        function openDialog() {
            dialog.hidden = false;
            playFilter.value = '';
            renderPlays();
            playFilter.focus();
            if (plays !== null) return;
            fetch(playList.getAttribute('data-endpoint'), { credentials: 'same-origin' })
                .then(function (response) { return response.ok ? response.json() : []; })
                .then(function (rows) {
                    plays = rows.map(function (row, index) { row.index = index; return row; });
                    renderPlays();
                })
                .catch(function () { plays = []; renderPlays(); });
        }

        function closeDialog() { dialog.hidden = true; }

        loadButton.addEventListener('click', openDialog);
        dialog.querySelector('[data-sc-dialog-close]').addEventListener('click', closeDialog);
        dialog.addEventListener('click', function (e) { if (e.target === dialog) closeDialog(); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeDialog(); });
        playFilter.addEventListener('input', renderPlays);
        playList.addEventListener('click', function (e) {
            var row = e.target.closest('[data-sc-play]');
            if (!row) return;
            var play = plays[parseInt(row.getAttribute('data-sc-play'), 10)];
            if (!play) return;
            inputs.perfects.value = play.perfects;
            inputs.greats.value = play.greats;
            inputs.goods.value = play.goods;
            inputs.bads.value = play.bads;
            inputs.misses.value = play.misses;
            inputs.combo.value = play.combo;
            closeDialog();
            recalc();
        });
    }

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

// The Phoenix score page's moving parts (docs/design/phoenix-score-calculator.md D2), on a page
// that is otherwise static HTML: the live calculator with its two attribution bars and the
// weighted-random next-letter walk, the chart-size chips, the Singles/Doubles toggle, chart
// tooltips, and the plays dialog. Every constant this script reads — both mixes' grade floors,
// the judgement weights, the owner-verified calorie table — comes from the JSON block the
// server emitted from the engine, so nothing here can disagree with the page it sits under.
// The score/plate/walk/bars arithmetic lives in score-breakdown.js, shared with the Score
// Breakdown Dialog so the two surfaces cannot drift (session-breakdown.md §7.3); without this
// script the page loses the tool and keeps every fact.
import { createScoreBreakdown, attachBreakdownTips } from './score-breakdown.js';

var page = document.querySelector('[data-sc-page]');
if (page) initialize();

function initialize() {
    var constantsBlock = page.querySelector('[data-sc-constants]');
    if (!constantsBlock) return;
    var CONST = JSON.parse(constantsBlock.textContent);
    var MIX = page.getAttribute('data-sc-mix');
    var OTHER_MIX = MIX === 'Phoenix2' ? 'Phoenix' : 'Phoenix2';
    var lang = document.documentElement.lang || undefined;

    var engine = createScoreBreakdown(CONST, MIX, lang);
    var score = engine.score;
    var gradeFor = engine.gradeFor;
    var plateFor = engine.plateFor;

    function n0(v) { return Math.round(v).toLocaleString(lang); }
    function fmt(template) {
        var args = Array.prototype.slice.call(arguments, 1);
        return template.replace(/\{(\d+)\}/g, function (_, i) { return args[+i] === undefined ? '' : args[+i]; });
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

    function judgementLabel(name) {
        return inputs[name].previousElementSibling.textContent.trim();
    }

    // The shared renderers take their strings from the caller: this page's channel is the
    // data-t-* attributes the server localized into the markup.
    function barLabels(el) {
        return {
            perfects: judgementLabel('perfects'),
            greats: judgementLabel('greats'),
            goods: judgementLabel('goods'),
            bads: judgementLabel('bads'),
            misses: judgementLabel('misses'),
            combo: judgementLabel('combo'),
            brokenCombo: el.getAttribute('data-t-broken-combo'),
            gained: el.getAttribute('data-t-gained'),
            lost: el.getAttribute('data-t-lost'),
            notEarned: el.getAttribute('data-t-not-earned'),
            nothingLost: el.getAttribute('data-t-nothing-lost'),
            clipped: el.getAttribute('data-t-clipped')
        };
    }

    function nextLabels(el) {
        return {
            label: el.getAttribute('data-t-label'),
            points: el.getAttribute('data-t-points'),
            best: el.getAttribute('data-t-best'),
            get: el.getAttribute('data-t-get'),
            morePerfects: el.getAttribute('data-t-more-perfects'),
            fewerMisses: el.getAttribute('data-t-fewer-misses'),
            fewerBads: el.getAttribute('data-t-fewer-bads'),
            fewerGoods: el.getAttribute('data-t-fewer-goods'),
            fewerGreats: el.getAttribute('data-t-fewer-greats'),
            moreCombo: el.getAttribute('data-t-more-combo')
        };
    }

    function markSpreadRow(gradeName) {
        page.querySelectorAll('[data-sc-spread-grade]').forEach(function (row) {
            row.classList.toggle('sc-you', row.getAttribute('data-sc-spread-grade') === gradeName);
        });
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
        engine.renderNextLetter(nextEl, s, nextLabels(nextEl));
        barsEl.hidden = false;
        engine.renderBars(barsEl, s, barLabels(barsEl));
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

    // A link can hand the calculator a play: the Score Breakdown Dialog's "open in the
    // calculator" carries the counts as query parameters, and typing over them works exactly
    // as if they had been typed in the first place.
    var params = new URLSearchParams(window.location.search);
    var prefilled = false;
    ['perfects', 'greats', 'goods', 'bads', 'misses', 'combo'].forEach(function (name) {
        var v = params.get(name);
        if (v !== null && /^\d+$/.test(v)) { inputs[name].value = v; prefilled = true; }
    });
    if (prefilled) recalc();

    // ---- the chart-size chips reprice the cost cards and the budget table.
    var sizeGroup = page.querySelector('[data-sc-sizes]');
    if (sizeGroup) {
        var costOf = function (kind, notes) {
            // Best-case combos, mirroring the model: greats keep the run, a good tops out one
            // short, a bad or an edge miss breaks one note off it.
            if (kind === 'great') return 1000000 - score(notes - 1, 1, 0, 0, 0, notes);
            if (kind === 'good') return 1000000 - score(notes - 1, 0, 1, 0, 0, notes - 1);
            if (kind === 'bad') return 1000000 - score(notes - 1, 0, 0, 1, 0, notes - 1);
            if (kind === 'miss') return 1000000 - score(notes - 1, 0, 0, 0, 1, notes - 1);
            return 1000000 - score(notes - 1, 0, 0, 0, 1, Math.floor((notes - 1) / 2));
        };
        var budgetFor = function (floor, notes) {
            var low = 0, high = notes;
            while (low < high) {
                var candidate = Math.floor((low + high + 1) / 2);
                if (score(notes - candidate, candidate, 0, 0, 0, notes) >= floor) low = candidate;
                else high = candidate - 1;
            }
            return low;
        };
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

        var esc = function (text) {
            return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        };

        var messageRow = function (text) {
            return '<tr><td style="color:var(--mix-ink-muted)">' + esc(text) + '</td></tr>';
        };

        var renderPlays = function () {
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
        };

        var openDialog = function () {
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
        };

        var closeDialog = function () { dialog.hidden = true; };

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

    // ---- one tooltip for every [data-sc-tip] carrier (chart hit areas, bar segments) —
    // shared with the breakdown dialog so a page hosting both never grows two.
    attachBreakdownTips(page);
}

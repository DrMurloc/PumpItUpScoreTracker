// The PUMBILITY calculator's three interactions (docs/design/pumbility-calculator.md D2), on a
// page that is otherwise static HTML: the Singles/Doubles toggle, the value table's contour
// click, and the quick calculator. Every number this script multiplies comes from the JSON
// block each type block emits — the server wrote it from ScoringConfiguration — so nothing here
// can disagree with the tables it sits under. Without this script the page loses the toggle and
// the click and the calculator stops at its server-rendered default; it loses no fact.
(function () {
    'use strict';

    var page = document.querySelector('[data-pc-page]');
    if (!page) return;

    var lang = document.documentElement.lang || undefined;
    var GRADES = ['F', 'D', 'C', 'B', 'A', 'A+', 'AA', 'AA+', 'AAA', 'AAA+', 'S', 'S+', 'SS', 'SS+', 'SSS', 'SSS+'];

    function n0(v) { return Math.round(v).toLocaleString(lang); }
    function n2(v) { return v.toLocaleString(lang, { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
    function fmt(template) {
        var args = Array.prototype.slice.call(arguments, 1);
        return template.replace(/\{(\d+)\}/g, function (_, i) { return args[+i] === undefined ? '' : args[+i]; });
    }

    // ---- the type toggle: one type's sections at a time, both always in the HTML.
    var buttons = page.querySelectorAll('[data-pc-type-button]');
    var blocks = page.querySelectorAll('[data-pc-type]');
    function setType(type) {
        blocks.forEach(function (b) { b.hidden = b.getAttribute('data-pc-type') !== type; });
        buttons.forEach(function (b) {
            b.setAttribute('aria-pressed', String(b.getAttribute('data-pc-type-button') === type));
        });
    }
    buttons.forEach(function (b) {
        b.addEventListener('click', function () { setType(b.getAttribute('data-pc-type-button')); });
    });

    // ---- the contour click: the closest cell on every other row, rows out of reach dimmed.
    page.querySelectorAll('[data-pc-table]').forEach(function (table) {
        var section = table.closest('section');
        var cap = section ? section.querySelector('[data-pc-contour-cap]') : null;
        var prefix = table.getAttribute('data-pc-prefix') || '';
        var idle = cap ? cap.innerHTML : '';

        function clear() {
            table.querySelectorAll('td.pc-v').forEach(function (c) { c.classList.remove('pc-hit', 'pc-pick', 'pc-far'); });
            if (cap) cap.innerHTML = idle;
        }

        function pick(cell) {
            var v = parseFloat(cell.getAttribute('data-v'));
            var l0 = parseInt(cell.getAttribute('data-l'), 10);
            var g0 = cell.getAttribute('data-g');
            var eqs = [];
            table.querySelectorAll('tbody tr').forEach(function (tr) {
                var cells = Array.prototype.filter.call(tr.querySelectorAll('td.pc-v'), function (c) { return c.hasAttribute('data-v'); });
                cells.forEach(function (c) { c.classList.remove('pc-hit', 'pc-pick', 'pc-far'); });
                if (!cells.length) return;
                var best = null, bd = Infinity;
                cells.forEach(function (c) {
                    var d = Math.abs(parseFloat(c.getAttribute('data-v')) - v);
                    if (d < bd) { bd = d; best = c; }
                });
                var l = parseInt(best.getAttribute('data-l'), 10);
                if (l === l0) { best.classList.add('pc-pick'); return; }
                var top = parseFloat(cells[cells.length - 1].getAttribute('data-v'));
                var bottom = parseFloat(cells[0].getAttribute('data-v'));
                if (top < v * 0.985 || bottom > v * 1.015) { cells.forEach(function (c) { c.classList.add('pc-far'); }); return; }
                best.classList.add('pc-hit');
                if (Math.abs(l - l0) <= 2 || Math.abs(parseFloat(best.getAttribute('data-v')) - v) / v < 0.004) {
                    eqs.push({ l: l, g: best.getAttribute('data-g'), v: parseFloat(best.getAttribute('data-v')) });
                }
            });
            if (!cap) return;
            eqs.sort(function (a, b) { return b.l - a.l; });
            var on = cap.getAttribute('data-t-on') || '{0} on {1}';
            var list = eqs.slice(0, 5).map(function (e) {
                return '<span class="pc-eq">' + fmt(on, e.g, prefix + e.l) + '</span><span class="pc-muted">' + n0(e.v) + '</span>';
            }).join(' · ');
            cap.innerHTML = '<b>' + prefix + l0 + ' ' + g0 + ' = ' + n2(v) + '</b>' +
                '<span>' + (cap.getAttribute('data-t-closest') || '') + '</span>' +
                (list || '<span class="pc-muted">' + (cap.getAttribute('data-t-none') || '') + '</span>') +
                '<button type="button" data-pc-clear>' + (cap.getAttribute('data-t-clear') || 'clear') + '</button>';
            cap.querySelector('[data-pc-clear]').addEventListener('click', clear);
        }

        table.addEventListener('click', function (e) {
            var cell = e.target.closest('td.pc-v[data-v]');
            if (cell) pick(cell);
        });
    });

    // ---- the quick calculator: level · grade · plate → the exact value, and what equals it nearby.
    page.querySelectorAll('[data-pc-calc]').forEach(function (form) {
        var block = form.querySelector('[data-pc-constants]');
        if (!block) return;
        var c;
        try { c = JSON.parse(block.textContent); } catch (err) { return; }
        var levelSel = form.querySelector('[data-pc-level]');
        var gradeSel = form.querySelector('[data-pc-grade]');
        var plateSel = form.querySelector('[data-pc-plate]');
        var out = form.querySelector('[data-pc-out]');
        var math = form.querySelector('[data-pc-math]');
        var eq = form.querySelector('[data-pc-eq]');
        var prefix = c.prefix || '';

        function value(level, grade, plate) {
            var base = c.levels[String(level)];
            if (base === undefined) return 0;
            var g = c.grades[grade];
            var p = c.plates[plate === undefined ? 'RG' : plate];
            return c.additive ? base * (g + p) : base * g * p;
        }

        function update() {
            var level = parseInt(levelSel.value, 10);
            var grade = gradeSel.value;
            var plate = plateSel ? plateSel.value : 'RG';
            var v = value(level, grade, plate);
            var base = c.levels[String(level)];
            out.textContent = n2(v);
            if (math) {
                math.innerHTML = c.additive
                    ? '<i class="pc-t-level">' + n0(base) + '</i> × (<i class="pc-t-grade">' + c.grades[grade].toFixed(2) + '</i> + <i class="pc-t-plate">' + c.plates[plate].toFixed(3) + '</i>)' +
                      (c.singlesUp ? ' &nbsp;·&nbsp; ' + fmt(math.getAttribute('data-t-up') || 'Base({0})', level + 1) : '')
                    : '<i class="pc-t-level">' + n0(base) + '</i> × <i class="pc-t-grade">' + c.grades[grade].toFixed(2) + '</i>';
            }
            if (!eq) return;
            var items = [];
            [4, 3, 2, 1, -1, -2].forEach(function (d) {
                var l2 = level + d;
                if (c.levels[String(l2)] === undefined) return;
                var best = null, bd = Infinity;
                GRADES.forEach(function (g2) {
                    var v2 = value(l2, g2, 'RG');
                    var diff = Math.abs(v2 - v);
                    if (diff < bd) { bd = diff; best = { g: g2, v: v2 }; }
                });
                var top = value(l2, 'SSS+', c.additive ? 'PG' : 'RG');
                var bottom = value(l2, 'F', 'RG');
                var name = prefix + l2;
                if (v > top * 1.005) items.push('<span>' + fmt(eq.getAttribute('data-t-cant') || '{0} cannot reach it', '<b>' + name + '</b>', n0(top)) + '</span>');
                else if (v < bottom * 0.995) items.push('<span>' + fmt(eq.getAttribute('data-t-beats') || '{0} beats it at any pass', '<b>' + name + '</b>') + '</span>');
                else items.push('<span>' + fmt(eq.getAttribute('data-t-on') || '{0} on {1}', '<b>' + best.g + '</b>', '<b>' + name + '</b>') + ' <span class="pc-muted">' + n0(best.v) + '</span></span>');
            });
            eq.innerHTML = '<span class="pc-lbl">' + (eq.getAttribute('data-t-label') || '') + '</span>' + items.join('');
        }

        [levelSel, gradeSel, plateSel].forEach(function (s) { if (s) s.addEventListener('change', update); });
        update();
    });
})();

// Bridge for the Weekly Charts hub (docs/design/weekly-charts-overhaul.md §3.3). The page is
// static HTML with data-challenge-* controls and real chart links; the one island
// (ChallengeDialogHost) registers itself here and this forwards clicks to it. Until the island's
// circuit connects, ref is null and the controls are honest inert HTML — same posture as the
// shell's static nav. Loaded globally; harmless on pages with no data-challenge-* elements.
(function () {
    var ref = null;

    window.challengeBoard = {
        register: function (dotNetRef) {
            ref = dotNetRef;
            // The controls are inert until this fires; mark the document so a test (or any
            // watcher) can tell the island's circuit has connected before driving it.
            document.documentElement.setAttribute('data-challenge-ready', '1');
        }
    };

    // Density is pure presentation: swap the grid variant instantly (no circuit), then persist
    // in the background. Anonymous visitors get the swap; the POST 401s for them, harmlessly.
    function applyDensity(button) {
        var value = button.getAttribute('data-den');
        var grid = document.querySelector('#weekly .challenge-grid');
        if (grid) grid.setAttribute('data-density', value);
        var group = button.closest('.challenge-den-group');
        if (group) {
            group.querySelectorAll('[data-den]').forEach(function (b) {
                b.classList.toggle('on', b === button);
            });
        }
        persist('Density__WeeklyCharts', value);
    }

    // Persist an allowlisted UI preference in the background; the visual swap already happened.
    function persist(key, value) {
        var body = new URLSearchParams();
        body.set('key', key);
        body.set('value', value);
        fetch('/Preferences/Set', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        }).catch(function () { });
    }

    // The URL keeps naming the visible state for sharing and crawlers (§12.5) — but as a
    // record of the swap, never the mechanism.
    function nameState(mutate) {
        var url = new URL(window.location.href);
        mutate(url.searchParams);
        history.replaceState(null, '', url);
    }

    // The monthly segment row (M18): all four boards are in the HTML; swap which one shows.
    function applyMonthlyType(button) {
        var value = button.getAttribute('data-mtype');
        var card = button.closest('.challenge-railcard') || document;
        card.querySelectorAll('[data-mtype]').forEach(function (b) {
            b.classList.toggle('on', b === button);
            b.setAttribute('aria-selected', b === button ? 'true' : 'false');
        });
        card.querySelectorAll('[data-mboard]').forEach(function (b) {
            b.hidden = b.getAttribute('data-mboard') !== value;
        });
        nameState(function (q) {
            if (value === 'Combined') q.delete('type');
            else q.set('type', value);
        });
    }

    // Show all / suggested only (M13): every card is in the HTML; the grid class decides.
    function toggleShowAll() {
        var grid = document.querySelector('#weekly .challenge-grid');
        if (!grid) return;
        var filtered = grid.classList.toggle('suggested-only');
        nameState(function (q) {
            if (filtered) q.delete('suggested');
            else q.set('suggested', 'all');
        });
    }

    // Relevant players (M20): rows carry both worlds; the class swaps them, the
    // preference persists per account.
    function applyRelevant(input) {
        var grid = document.querySelector('#weekly .challenge-grid');
        if (grid) grid.classList.toggle('relevant-on', input.checked);
        persist('WeeklyCharts__RelevantPlayers', input.checked ? 'true' : 'false');
    }

    document.addEventListener('change', function (e) {
        var relevant = e.target.closest('[data-challenge-relevant]');
        if (relevant) applyRelevant(relevant);
    });

    document.addEventListener('click', function (e) {
        var den = e.target.closest('[data-den]');
        if (den) {
            e.preventDefault();
            applyDensity(den);
            return;
        }
        var mtype = e.target.closest('[data-mtype]');
        if (mtype) {
            e.preventDefault();
            applyMonthlyType(mtype);
            return;
        }
        var showAll = e.target.closest('[data-challenge-showall]');
        if (showAll) {
            e.preventDefault();
            toggleShowAll();
            return;
        }
        var record = e.target.closest('[data-challenge-record]');
        var board = e.target.closest('[data-challenge-board]');
        var monthly = e.target.closest('[data-challenge-monthly]');
        var chart = e.target.closest('[data-challenge-chart]');
        var rotate = e.target.closest('[data-challenge-rotate]');
        if (!record && !board && !monthly && !chart && !rotate) return;
        // No circuit yet: let chart links follow their real href (crawler-facing fallback);
        // the buttons simply do nothing until the island connects.
        if (!ref) {
            if (record || board || monthly || rotate) e.preventDefault();
            return;
        }
        e.preventDefault();
        if (record) {
            ref.invokeMethodAsync('OpenRecord', record.getAttribute('data-challenge-record'),
                record.hasAttribute('data-daily'));
        } else if (board) {
            ref.invokeMethodAsync('OpenBoard', board.getAttribute('data-challenge-board'),
                board.hasAttribute('data-daily'));
        } else if (monthly) {
            var active = document.querySelector('.challenge-seg-btn.on[data-mtype]');
            ref.invokeMethodAsync('OpenMonthly', active ? active.getAttribute('data-mtype') : 'Combined');
        } else if (chart) {
            ref.invokeMethodAsync('OpenChart', chart.getAttribute('data-challenge-chart'));
        } else if (rotate) {
            ref.invokeMethodAsync('OpenRotate');
        }
    });

    // Tell the shell a page dock is present so the bottom nav coexists and slides away on scroll
    // (the page renders the dock markup statically; MainLayout's PageDockService isn't in play here).
    function announceDock() {
        if (document.querySelector('.page-dock') && window.shell) {
            window.shell.setDockState(true, false);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', announceDock);
    } else {
        announceDock();
    }
})();

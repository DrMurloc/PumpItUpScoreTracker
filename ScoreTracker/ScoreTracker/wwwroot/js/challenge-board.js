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
        var body = new URLSearchParams();
        body.set('key', 'Density__WeeklyCharts');
        body.set('value', value);
        fetch('/Preferences/Set', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        }).catch(function () { /* preference is best-effort; the swap already happened */ });
    }

    document.addEventListener('click', function (e) {
        var den = e.target.closest('[data-den]');
        if (den) {
            e.preventDefault();
            applyDensity(den);
            return;
        }
        var record = e.target.closest('[data-challenge-record]');
        var board = e.target.closest('[data-challenge-board]');
        var chart = e.target.closest('[data-challenge-chart]');
        var rotate = e.target.closest('[data-challenge-rotate]');
        if (!record && !board && !chart && !rotate) return;
        // No circuit yet: let chart links follow their real href (crawler-facing fallback);
        // the buttons simply do nothing until the island connects.
        if (!ref) {
            if (record || board || rotate) e.preventDefault();
            return;
        }
        e.preventDefault();
        if (record) {
            ref.invokeMethodAsync('OpenRecord', record.getAttribute('data-challenge-record'),
                record.hasAttribute('data-daily'));
        } else if (board) {
            ref.invokeMethodAsync('OpenBoard', board.getAttribute('data-challenge-board'),
                board.hasAttribute('data-daily'));
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

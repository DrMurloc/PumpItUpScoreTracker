// Debug harness for the reconnect overlay (#components-reconnect-modal in App.razor).
//
// Ctrl+Alt+R  cycles the overlay through every state class Blazor can set.
// Ctrl+Alt+X  clears it.
//
// The paused and resume-failed states only occur when the server parks a circuit, which
// cannot be provoked on demand — this is the one way to see every state on a real page.
// It forces the classes by hand, so it shows what each state looks like; it does not prove
// the wiring. A real state change from Blazor overwrites whatever this set, and the classes
// are plain CSS togglable from any devtools console, so shipping the shortcut exposes
// nothing a visitor could not already do.
(function () {
    var STATES = [
        ['components-reconnect-show'],
        ['components-reconnect-show', 'components-reconnect-retrying'],
        ['components-reconnect-paused'],
        ['components-reconnect-failed'],
        ['components-reconnect-resume-failed'],
        ['components-reconnect-rejected']
    ];
    var i = -1;

    function modal() { return document.getElementById('components-reconnect-modal'); }

    function clear() {
        var m = modal();
        if (!m) return;
        m.className = '';
        i = -1;
    }

    function next() {
        var m = modal();
        if (!m) { console.warn('[reconnect-preview] no #components-reconnect-modal on this page'); return; }
        i = (i + 1) % STATES.length;
        m.className = STATES[i].join(' ');

        // Blazor fills these on a real outage; nothing fills them when the classes are forced.
        var attempt = document.getElementById('components-reconnect-current-attempt');
        var max = document.getElementById('components-reconnect-max-retries');
        if (attempt) attempt.innerText = '5';
        if (max) max.innerText = '12';

        console.log('[reconnect-preview] ' + (i + 1) + '/' + STATES.length + ' — ' + STATES[i].join(' '));
    }

    document.addEventListener('keydown', function (e) {
        if (!e.ctrlKey || !e.altKey) return;
        var k = (e.key || '').toLowerCase();
        if (k === 'r') { e.preventDefault(); next(); }
        else if (k === 'x') { e.preventDefault(); clear(); }
    });
})();

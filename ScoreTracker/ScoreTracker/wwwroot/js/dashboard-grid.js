// Home dashboard drag-to-reorder (design doc §2.4, D6): the ghost and the insertion
// indicator are pure client-side — 60Hz pointermove never crosses SignalR; only the
// drop does, as one MoveHomePageWidgetCommand dispatch (the same operation the edit
// arrows and the mobile reorder use). Listeners delegate from a stable board wrapper,
// so Blazor re-renders inside it never orphan the handlers. Pointer events, not HTML5
// drag-and-drop: no ghost-image quirks, and touch stays deliberately unbound (D3 —
// mobile reorders with the arrows).
window.dashboardGrid = {
    _state: null,

    // D16: widget telemetry rides Clarity custom events; a no-op when Clarity isn't
    // loaded (local dev). Which widgets earn future investment is THE roadmap
    // question this page creates — instrumented from day one.
    track: function (eventName) {
        if (window.clarity) window.clarity('event', eventName);
    },

    init: function (board, dotnetRef) {
        this.dispose();
        const state = { board: board, dotnetRef: dotnetRef, onDown: null };
        state.onDown = e => {
            const handle = e.target.closest('.dash-drag-handle');
            if (!handle || e.pointerType === 'touch') return;
            const cell = handle.closest('.dash-cell');
            if (!cell) return;
            e.preventDefault();
            this._start(state, cell, e);
        };
        board.addEventListener('pointerdown', state.onDown);
        this._state = state;
    },

    dispose: function () {
        if (!this._state) return;
        this._state.board.removeEventListener('pointerdown', this._state.onDown);
        this._state = null;
    },

    _cells: function (board) {
        return Array.from(board.querySelectorAll('.dash-cell'));
    },

    _start: function (state, cell, downEvent) {
        const board = state.board;
        const rect = cell.getBoundingClientRect();
        const ghost = cell.cloneNode(true);
        Object.assign(ghost.style, {
            position: 'fixed', left: rect.left + 'px', top: rect.top + 'px',
            width: rect.width + 'px', height: rect.height + 'px',
            opacity: '0.75', pointerEvents: 'none', zIndex: '2000', margin: '0'
        });
        document.body.appendChild(ghost);
        cell.classList.add('dash-dragging');

        const startX = downEvent.clientX;
        const startY = downEvent.clientY;
        const origIndex = this._cells(board).indexOf(cell);
        let targetIndex = origIndex;

        const move = e => {
            ghost.style.transform = 'translate(' + (e.clientX - startX) + 'px,' + (e.clientY - startY) + 'px)';
            targetIndex = this._indexAt(board, cell, e.clientX, e.clientY, origIndex);
            this._indicate(board, cell, targetIndex, origIndex);
        };
        const up = () => {
            document.removeEventListener('pointermove', move);
            document.removeEventListener('pointerup', up);
            ghost.remove();
            cell.classList.remove('dash-dragging');
            this._cells(board).forEach(c => c.classList.remove('dash-drop-target'));
            if (targetIndex !== origIndex && cell.dataset.widgetId)
                state.dotnetRef.invokeMethodAsync('OnWidgetDropped', cell.dataset.widgetId, targetIndex);
        };
        document.addEventListener('pointermove', move);
        document.addEventListener('pointerup', up);
    },

    // Insertion index among the OTHER cells (matches MoveHomePageWidgetCommand's
    // remove-then-insert semantics): nearest cell center wins, before/after decided
    // by the pointer's position relative to it in reading order.
    _indexAt: function (board, dragged, x, y, origIndex) {
        const others = this._cells(board).filter(c => c !== dragged);
        if (!others.length) return origIndex;
        let best = null;
        let bestDist = Infinity;
        let bestIndex = -1;
        others.forEach((c, i) => {
            const r = c.getBoundingClientRect();
            const cx = r.left + r.width / 2;
            const cy = r.top + r.height / 2;
            const d = (cx - x) * (cx - x) + (cy - y) * (cy - y);
            if (d < bestDist) {
                bestDist = d;
                best = { cx: cx, cy: cy, h: r.height };
                bestIndex = i;
            }
        });
        const after = y > best.cy + best.h / 4
            || (Math.abs(y - best.cy) <= best.h / 4 && x > best.cx);
        return after ? bestIndex + 1 : bestIndex;
    },

    _indicate: function (board, dragged, index, origIndex) {
        const others = this._cells(board).filter(c => c !== dragged);
        others.forEach(c => c.classList.remove('dash-drop-target'));
        if (index === origIndex || !others.length) return;
        others[Math.min(index, others.length - 1)].classList.add('dash-drop-target');
    }
};

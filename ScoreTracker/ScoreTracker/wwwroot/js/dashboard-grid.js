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
        let target = null;

        const move = e => {
            ghost.style.transform = 'translate(' + (e.clientX - startX) + 'px,' + (e.clientY - startY) + 'px)';
            target = this._targetAt(board, cell, e.clientX, e.clientY);
            this._cells(board).forEach(c => c.classList.toggle('dash-swap-target', c === target));
        };
        const up = () => {
            document.removeEventListener('pointermove', move);
            document.removeEventListener('pointerup', up);
            ghost.remove();
            cell.classList.remove('dash-dragging');
            this._cells(board).forEach(c => c.classList.remove('dash-swap-target'));
            if (target && cell.dataset.widgetId && target.dataset.widgetId)
                state.dotnetRef.invokeMethodAsync('OnWidgetSwapped',
                    cell.dataset.widgetId, target.dataset.widgetId);
        };
        document.addEventListener('pointermove', move);
        document.addEventListener('pointerup', up);
    },

    // Swap semantics (field-test round 1): the cell under the pointer is the trade
    // partner — nobody else moves. The ghost is pointer-events:none, so
    // elementsFromPoint sees through it.
    _targetAt: function (board, dragged, x, y) {
        for (const el of document.elementsFromPoint(x, y)) {
            if (!el.closest) continue;
            const cell = el.closest('.dash-cell');
            if (cell && cell !== dragged && board.contains(cell)) return cell;
        }
        return null;
    }
};

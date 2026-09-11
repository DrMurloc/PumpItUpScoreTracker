// The pointer half of LevelSpreadChart (docs/design/pumbility-overhaul.md D66). The chart draws itself
// in HTML and CSS; this only reads where the pointer is. Over a column it opens that column's tooltip —
// whose lines the component rendered hidden inside the column — marks the count under the pointer, and
// writes the one line that depends on it: how many peers keep exactly that many charts. Keyboard focus
// opens the same tooltip without that line. Nothing here reaches the server.

const format = (template, ...args) =>
    (template || '').replace(/\{(\d+)\}/g, (match, index) => (args[Number(index)] ?? match));

// Answers the handle the component disposes with: by the time it does, the chart's element may already
// have left the page, so the listeners cannot be found again through it.
export function mount(root) {
    const tip = root?.querySelector('.pmb-spread-tip');
    if (!tip) return { dispose() {} };
    let active = null;

    const clearHot = column => column.querySelectorAll('[data-hot]').forEach(bin => bin.removeAttribute('data-hot'));

    const hide = () => {
        if (active) {
            clearHot(active);
            active.removeAttribute('data-active');
        }
        active = null;
        tip.removeAttribute('data-open');
        tip.replaceChildren();
    };

    // The count the pointer sits on: the column's scale runs from half a chart below zero to half above the top.
    const countUnder = (column, clientY) => {
        const plot = column.querySelector('.pmb-spread-colplot');
        const top = Number(column.closest('.pmb-spread-plot')?.dataset.top || 0);
        if (!plot || !(top > 0)) return null;
        const rect = plot.getBoundingClientRect();
        if (rect.height <= 0 || clientY < rect.top || clientY > rect.bottom) return null;
        const value = (top + 0.5) - ((clientY - rect.top) / rect.height) * (top + 1);
        return Math.min(top, Math.max(0, Math.round(value)));
    };

    const pointerLine = (column, count) => {
        const bins = new Map((column.dataset.bins || '').split(',').filter(Boolean)
            .map(pair => pair.split(':').map(Number)));
        const peers = bins.get(count) || 0;
        const total = Number(column.dataset.peers || 0);
        const share = total ? Math.round((100 * peers) / total) : 0;
        const line = document.createElement('div');
        line.className = 'pmb-spread-tippointer';
        const strong = document.createElement('strong');
        strong.textContent = count === 1 ? root.dataset.chartOne : format(root.dataset.charts, count);
        const rest = document.createElement('span');
        rest.textContent = ' ' + (peers === 1
            ? format(root.dataset.peerOne, share)
            : format(root.dataset.peers, peers, share));
        line.append(strong, rest);
        return line;
    };

    const show = (column, clientX, clientY) => {
        if (active !== column) {
            hide();
            active = column;
            column.setAttribute('data-active', '');
        }
        const body = column.querySelector('.pmb-spread-tipbody');
        tip.replaceChildren(...(body ? Array.from(body.children, child => child.cloneNode(true)) : []));
        clearHot(column);
        if (clientY != null) {
            const count = countUnder(column, clientY);
            if (count != null) {
                column.querySelector(`.pmb-spread-bin[data-k="${count}"]`)?.setAttribute('data-hot', '');
                tip.insertBefore(pointerLine(column, count), tip.children[1] || null);
            }
        }
        tip.setAttribute('data-open', '');

        const box = root.getBoundingClientRect();
        const cell = column.getBoundingClientRect();
        const x = (clientX ?? cell.left + cell.width / 2) - box.left;
        const y = (clientY ?? cell.top + cell.height / 3) - box.top;
        const width = tip.offsetWidth;
        const height = tip.offsetHeight;
        let left = x + 14;
        if (left + width > root.clientWidth - 2) left = x - width - 14;
        let top = y - height - 10;
        if (top < 0) top = y + 16;
        tip.style.left = `${Math.max(2, left)}px`;
        tip.style.top = `${top}px`;
    };

    const columnOf = event => event.target instanceof Element ? event.target.closest('.pmb-spread-col') : null;

    const onMove = event => {
        const column = columnOf(event);
        if (column && root.contains(column)) show(column, event.clientX, event.clientY);
        else if (active) hide();
    };
    const onLeave = event => {
        if (event.pointerType !== 'touch') hide();
    };
    const onFocusIn = event => {
        const column = columnOf(event);
        if (column && root.contains(column)) show(column, null, null);
    };
    const onFocusOut = event => {
        if (!(event.relatedTarget instanceof Element) || !root.contains(event.relatedTarget)) hide();
    };
    // A tap away from the chart closes a tooltip a tap opened.
    const onDocumentDown = event => {
        if (active && !(event.target instanceof Node && root.contains(event.target))) hide();
    };

    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerdown', onMove);
    root.addEventListener('pointerleave', onLeave);
    root.addEventListener('focusin', onFocusIn);
    root.addEventListener('focusout', onFocusOut);
    document.addEventListener('pointerdown', onDocumentDown);

    return {
        dispose() {
            root.removeEventListener('pointermove', onMove);
            root.removeEventListener('pointerdown', onMove);
            root.removeEventListener('pointerleave', onLeave);
            root.removeEventListener('focusin', onFocusIn);
            root.removeEventListener('focusout', onFocusOut);
            document.removeEventListener('pointerdown', onDocumentDown);
        }
    };
}

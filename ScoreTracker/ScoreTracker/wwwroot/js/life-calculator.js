// Life Calculator: the life readout counts up so a x50 press reads as a climb rather
// than a jump. The BAR sweep is a pure CSS transition (see .lc-fill) — only the digits
// need scripting. Deliberately one interop call per press: animating this in C# would be
// one round-trip per frame on a server circuit.
// docs/design/life-calculator-redesign.md

let token = 0;

export function countTo(element, from, to, ms) {
    if (!element) return;

    token++;
    const mine = token;
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reduce || ms <= 0 || from === to) {
        element.textContent = format(to);
        return;
    }

    // Seed the start value synchronously: Blazor has already rendered the final number,
    // and waiting for the first animation frame would flash it before the count begins.
    element.textContent = format(from);

    const started = performance.now();
    const step = (now) => {
        if (mine !== token) return; // a newer press superseded this one
        const t = Math.min(1, (now - started) / ms);
        element.textContent = format(Math.round(from + (to - from) * t));
        if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);

    // The readout is data, not decoration. Animation frames stop in a background tab, so
    // guarantee the true value lands even when the count never gets to run.
    setTimeout(() => {
        if (mine === token) element.textContent = format(to);
    }, ms + 60);
}

function format(value) {
    return value.toLocaleString();
}

// The level slider (UX rule 12). Blazor renders the committed level and hears one `change` per
// release; while the finger is on the thumb, the number beside it is painted here from the
// input's own value. A round trip per `input` event wrote a one-latency-old level back onto the
// thumb and pulled it backwards under the finger.
export function followLevel(input, readout) {
    if (!input || !readout || input.dataset.followed) return;
    input.dataset.followed = '1';

    input.addEventListener('input', () => {
        // Edit Blazor's own text node rather than replacing it. textContent would swap in a new
        // node, and Blazor's next write to the number (a ladder row tapped) would land on the
        // detached one and leave the readout stale.
        const text = Array.from(readout.childNodes).find((node) => node.nodeType === Node.TEXT_NODE);
        if (text) text.nodeValue = input.value;
        else readout.textContent = input.value;
    });
}

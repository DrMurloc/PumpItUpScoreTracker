// The dual-thumb range control (Components/RangeSlider.razor, docs/design/charts-srp.md).
//
// Blazor renders the committed range and hears one `change` per release. Everything between
// the first touch and that release is painted here, from the inputs' own values, because a
// server round-trip per `input` event puts the thumb's position on the wire: the answer
// carries a value one latency old, Blazor writes it back onto the input, and the thumb is
// pulled backwards under the finger for as long as the drag lasts. A drag across a wide scale
// is also hundreds of events, which is not something a phone on a slow link should be sending.
//
// Nothing here formats a value. A scale whose readout is not just a number (a clock, a grade,
// a tier name) hands down data-range-labels — C#'s wording for every stop on the track.

const DASH = ' – ';
const TRAVEL = '(100% - var(--range-thumb))';
const HALF = 'var(--range-thumb) / 2';

export function attach(root) {
    if (!root || root.dataset.rangeAttached) return;

    const inputs = root.querySelectorAll('input[type=range]');
    const fill = root.querySelector('[data-range-fill]');
    const readout = root.querySelector('[data-range-readout]');
    if (inputs.length !== 2 || !fill || !readout) return;

    root.dataset.rangeAttached = '1';

    // What Blazor last rendered into the readout, kept for a drag that nets out (see settle).
    let resting = null;
    let committed = true;
    let labels = null;
    let labelsRaw = null;

    root.addEventListener('input', () => {
        if (resting === null) resting = readout.textContent;
        committed = false;
        paint();
    });

    // A `change` means Blazor is about to re-render the readout in its own words.
    root.addEventListener('change', () => {
        committed = true;
        resting = null;
    });

    root.addEventListener('pointerup', settle);
    root.addEventListener('pointercancel', settle);
    root.addEventListener('touchend', settle);

    // A drag that ends where it started fires no `change`, so no render comes to take the
    // live pair back down — and where the caller supplied its own wording ("Any"), the pair
    // is not what belongs there.
    function settle() {
        setTimeout(() => {
            if (!committed && resting !== null) readout.textContent = resting;
            resting = null;
        }, 0);
    }

    function paint() {
        // Read the extents every time: a slider whose scale is fetched (the drawer's BPM and
        // note-count ranges) is rendered before its Min and Max are known.
        const min = Number(root.dataset.rangeMin);
        const max = Number(root.dataset.rangeMax);
        const span = max - min;

        const a = Number(inputs[0].value);
        const b = Number(inputs[1].value);
        const low = Math.min(a, b);
        const high = Math.max(a, b);

        // Both ends measure along the thumb's travel rather than the box, because a thumb's
        // centre never reaches either end of its own input. RangeSlider.razor writes the same
        // expression for the resting state, so the two cannot disagree.
        const along = (value) => (span > 0 ? (value - min) / span : 0);
        fill.style.left = `calc(${HALF} + ${along(low)} * ${TRAVEL})`;
        fill.style.right = `calc(100% - ${HALF} - ${along(high)} * ${TRAVEL})`;

        readout.textContent = label(low, min) + DASH + label(high, min);
    }

    function label(value, min) {
        const raw = root.dataset.rangeLabels;
        if (raw) {
            if (raw !== labelsRaw) {
                labelsRaw = raw;
                labels = JSON.parse(raw);
            }

            const step = Math.max(1, Number(root.dataset.rangeStep) || 1);
            const word = labels[Math.round((value - min) / step)];
            if (word !== undefined) return word;
        }

        return (root.dataset.rangePrefix || '') + value;
    }
}

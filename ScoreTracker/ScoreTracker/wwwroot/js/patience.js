// Re-rolls the patience card's step pattern at every cycle boundary.
//
// The patterns are CSS; this only decides which one is on. Every pattern begins and ends on the
// bottom-left arrow, so swapping at the boundary lands the next one mid-stride and the pad never
// appears to stop and restart.
//
// The server picks the first pattern, so a pad that never gets this script still steps a real
// chart — it just repeats the one it was given.

const PATTERNS = ['patience-mrun', 'patience-spin', 'patience-run', 'patience-cross'];

export function shuffleEachCycle(pad) {
    if (!pad) return;

    // The bottom-left panel is the clock: it carries no delay in any pattern, so its iteration
    // boundary IS the cycle boundary. Listening on the pad instead would fire five times a
    // cycle, once per panel.
    const clock = pad.querySelector('.patience-dl');
    if (!clock) return;

    // Under prefers-reduced-motion the panels have no animation, so this event never fires and
    // the pattern never changes. That is the correct behaviour and it needs no branch here.
    clock.addEventListener('animationiteration', () => {
        const next = PATTERNS[Math.floor(Math.random() * PATTERNS.length)];
        for (const pattern of PATTERNS) {
            if (pattern !== next) pad.classList.remove(pattern);
        }
        pad.classList.add(next);
    });
}

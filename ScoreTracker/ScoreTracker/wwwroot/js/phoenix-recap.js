// Scroll-reveal + count-up for the Phoenix Recap deck. Loaded as a JS module by
// PhoenixRecap.razor; everything degrades to static content without it.
export function init(deckId) {
    const deck = document.getElementById(deckId);
    if (!deck) return;
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // Stagger-reveal direct content children of each slide as it scrolls into view.
    for (const slide of deck.querySelectorAll(".precap-slide")) {
        const inner = slide.querySelector(".precap-inner");
        if (!inner) continue;
        let delay = 0;
        for (const child of inner.children) {
            child.classList.add("precap-reveal");
            child.style.transitionDelay = `${delay}ms`;
            delay += 120;
        }
    }

    const seen = new WeakSet();
    const observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
            if (!entry.isIntersecting || seen.has(entry.target)) continue;
            seen.add(entry.target);
            entry.target.classList.add("precap-in");
            if (!reduced) entry.target.querySelectorAll("[data-count]").forEach(countUp);
        }
    }, { root: deck, threshold: 0.35 });
    deck.querySelectorAll(".precap-slide").forEach(s => observer.observe(s));
}

function countUp(el) {
    const target = parseInt(el.getAttribute("data-count"), 10);
    if (!Number.isFinite(target)) return;
    const duration = 1600;
    let start = null;
    const tick = ts => {
        if (start === null) start = ts;
        const progress = Math.min((ts - start) / duration, 1);
        const eased = 1 - Math.pow(1 - progress, 3);
        el.textContent = Math.round(target * eased).toLocaleString();
        if (progress < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
}

export async function refreshLogin() {
    await fetch("/Logout/Refresh");
}


export async function downloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);

    triggerFileDownload(fileName, url);

    URL.revokeObjectURL(url);
}

// The per-mix atmosphere gradients are body-class rules with hardcoded hues (site.css), so a
// page that re-themes itself by re-emitting --mix-* has to move the class too — otherwise the
// ground keeps the previous mix's glow over the new mix's background.
export function setThemeClass(className) {
    document.body.classList.remove('theme-xx', 'theme-phoenix', 'theme-phoenix2');
    document.body.classList.add(className);
}

export function triggerFileDownload(fileName, url) {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
}

// Scroll a horizontal rail by roughly one card. Fraction of the visible width rather than a
// fixed pixel step, because the cards are clamp()-sized — a hardcoded step overshoots on a
// narrow rail and undershoots on a wide one. Snap points do the final alignment.
export function scrollRail(element, direction) {
    if (!element) return;
    element.scrollBy({ left: direction * element.clientWidth * 0.8, behavior: 'smooth' });
}

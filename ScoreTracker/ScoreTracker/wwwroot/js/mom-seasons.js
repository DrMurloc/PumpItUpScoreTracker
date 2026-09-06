// Bridge for the March of Murlocs section (docs/design/march-of-murlocs.md §11.8): the pages are
// static HTML and the Past-seasons chip is a plain button; the one island (MoMPastSeasonsIsland)
// registers itself here and this forwards the click to it. Until the island's circuit connects,
// ref is null and the chip is honest inert HTML — the same posture as the challenges hub and the
// shell's static nav. Loaded globally; harmless on pages with no [data-mom-seasons] element.
(function () {
    var ref = null;
    window.momSeasons = {
        register: function (dotNetRef) {
            ref = dotNetRef;
            document.documentElement.setAttribute('data-mom-seasons-ready', '1');
        }
    };
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-mom-seasons]');
        if (!trigger) return;
        e.preventDefault();
        if (ref) ref.invokeMethodAsync('Open');
    });
})();

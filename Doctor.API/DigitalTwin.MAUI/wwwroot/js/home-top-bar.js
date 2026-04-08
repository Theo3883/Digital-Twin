globalThis.HomeTopBar = {
    init: function (el) {
        if (!el) return;
        let onScroll = function () {
            if (globalThis.scrollY > 10) {
                el.classList.add('scrolled');
            } else {
                el.classList.remove('scrolled');
            }
        };
        globalThis.addEventListener('scroll', onScroll, { passive: true });
        onScroll();
    }
};

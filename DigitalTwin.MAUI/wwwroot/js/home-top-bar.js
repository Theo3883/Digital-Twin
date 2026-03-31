window.HomeTopBar = {
    init: function (el) {
        if (!el) return;
        var onScroll = function () {
            if (window.scrollY > 10) {
                el.classList.add('scrolled');
            } else {
                el.classList.remove('scrolled');
            }
        };
        window.addEventListener('scroll', onScroll, { passive: true });
        onScroll();
    }
};

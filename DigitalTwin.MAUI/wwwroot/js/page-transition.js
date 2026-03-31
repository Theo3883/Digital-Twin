/**
 * page-transition.js
 * Drives page-enter animation without destroying/recreating the DOM.
 * Called from MainLayout.razor via JSInterop on every navigation.
 */
window.pageTransition = {
  /**
   * @param {HTMLElement} el  The .page-content wrapper element.
   */
  animate: function (el) {
    if (!el) return;

    // 1. Remove any in-progress animation classes first.
    el.classList.remove('page-enter', 'page-exit');

    // 2. Force a reflow so the browser registers the class removal before
    //    re-adding. This is the cheapest way to restart a CSS animation.
    void el.offsetWidth;

    // 3. Add the enter class — the CSS animation fires from its @keyframes.
    el.classList.add('page-enter');

    // 4. Auto-clean after the animation ends so the class doesn't linger.
    const onEnd = () => {
      el.classList.remove('page-enter');
      el.removeEventListener('animationend', onEnd);
    };
    el.addEventListener('animationend', onEnd, { once: true });
  }
};

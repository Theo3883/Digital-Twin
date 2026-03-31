/**
 * Native-feel bottom-sheet drag-to-dismiss.
 * All touch handling stays on the GPU/main thread — Blazor is only
 * called back once when the user drags past the dismiss threshold.
 * Supports multiple concurrent sheets keyed by the sheet element.
 */
window.SheetDrag = {
    _instances: new Map(),

    init(sheetEl, overlayEl, dotNetRef, dismissThreshold) {
        if (!sheetEl || !overlayEl) return;

        // Clean up any previous instance on the same sheet
        SheetDrag.dispose(sheetEl);

        // ── Slide-up entry animation ──
        sheetEl.style.transform = 'translateY(100%)';
        overlayEl.style.opacity = '0';
        // Force layout so the browser registers the start position
        sheetEl.getBoundingClientRect();
        // Animate in
        sheetEl.style.transition = 'transform 0.35s cubic-bezier(.25,.8,.25,1)';
        sheetEl.style.transform = 'translateY(0)';
        overlayEl.style.transition = 'opacity 0.3s ease';
        overlayEl.style.opacity = '1';

        let startY = 0;
        let currentY = 0;
        let dragging = false;
        const DISMISS_PX = dismissThreshold || 120;

        function onTouchStart(e) {
            const t = e.touches[0];
            if (!t) return;
            startY = t.clientY;
            currentY = 0;
            dragging = true;
            sheetEl.style.transition = 'none';
            overlayEl.style.transition = 'none';
        }

        function onTouchMove(e) {
            if (!dragging) return;
            const t = e.touches[0];
            if (!t) return;
            const delta = t.clientY - startY;
            currentY = Math.max(0, delta);

            // Rubber-band: past threshold the sheet moves slower
            const translate = currentY < DISMISS_PX
                ? currentY
                : DISMISS_PX + (currentY - DISMISS_PX) * 0.35;

            sheetEl.style.transform = `translateY(${translate}px)`;

            // Fade overlay proportionally
            const progress = Math.min(translate / (DISMISS_PX * 2), 1);
            overlayEl.style.opacity = 1 - progress * 0.6;

            e.preventDefault(); // stop scroll
        }

        function onTouchEnd() {
            if (!dragging) return;
            dragging = false;

            if (currentY >= DISMISS_PX) {
                // Animate out then notify Blazor
                sheetEl.style.transition = 'transform 0.25s cubic-bezier(.4,.0,.2,1)';
                sheetEl.style.transform = 'translateY(100%)';
                overlayEl.style.transition = 'opacity 0.25s ease';
                overlayEl.style.opacity = '0';
                setTimeout(() => {
                    dotNetRef.invokeMethodAsync('DismissSheet');
                }, 260);
            } else {
                // Snap back
                sheetEl.style.transition = 'transform 0.3s cubic-bezier(.25,.8,.25,1)';
                sheetEl.style.transform = 'translateY(0)';
                overlayEl.style.transition = 'opacity 0.3s ease';
                overlayEl.style.opacity = '1';
            }
        }

        // Only the pill area triggers drag, but we track move/end on the whole sheet
        // so the finger can wander.  We attach to the first child (the pill).
        const pill = sheetEl.querySelector('[data-sheet-pill]');
        const target = pill || sheetEl;

        target.addEventListener('touchstart', onTouchStart, { passive: true });
        sheetEl.addEventListener('touchmove', onTouchMove, { passive: false });
        sheetEl.addEventListener('touchend', onTouchEnd, { passive: true });

        SheetDrag._instances.set(sheetEl, () => {
            target.removeEventListener('touchstart', onTouchStart);
            sheetEl.removeEventListener('touchmove', onTouchMove);
            sheetEl.removeEventListener('touchend', onTouchEnd);
        });
    },

    dispose(sheetEl) {
        if (sheetEl) {
            const cleanup = SheetDrag._instances.get(sheetEl);
            if (cleanup) { cleanup(); SheetDrag._instances.delete(sheetEl); }
        } else {
            // Dispose all
            SheetDrag._instances.forEach(fn => fn());
            SheetDrag._instances.clear();
        }
    }
};

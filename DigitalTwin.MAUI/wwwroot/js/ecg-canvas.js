// ECG Canvas renderer — hospital-monitor sweep style
// Exposed as window.EcgCanvas for Blazor JSInterop calls.

globalThis.EcgCanvas = (() => {
    const contexts = {};

    function getCtx(canvasId) {
        if (!contexts[canvasId]) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) return null;
            const ctx = canvas.getContext('2d');
            contexts[canvasId] = { canvas, ctx };
        }
        return contexts[canvasId];
    }

    return {
        init(canvasId) {
            const entry = getCtx(canvasId);
            if (!entry) return;
            const { canvas, ctx } = entry;
            ctx.clearRect(0, 0, canvas.width, canvas.height);
        },

        draw(canvasId, samples, sweepPosition) {
            const entry = getCtx(canvasId);
            if (!entry || !samples || samples.length < 2) return;
            const { canvas, ctx } = entry;

            const w = canvas.width;
            const h = canvas.height;

            // Clear only a small band ahead of the sweep cursor (hospital monitor effect)
            const sweepBandWidth = Math.max(4, w * 0.015);
            ctx.clearRect(sweepPosition, 0, sweepBandWidth, h);

            if (samples.length === 0) return;

            const min = Math.min(...samples);
            const max = Math.max(...samples);
            const range = max - min || 1;

            const xStep = w / samples.length;

            ctx.beginPath();
            ctx.strokeStyle = getComputedStyle(document.documentElement)
                .getPropertyValue('--teal-primary').trim() || '#00D4C8';
            ctx.lineWidth = 2;
            ctx.lineJoin = 'round';
            ctx.lineCap = 'round';

            for (let i = 0; i < samples.length; i++) {
                const x = i * xStep;
                const y = h - ((samples[i] - min) / range) * (h * 0.85) - h * 0.075;
                if (i === 0) ctx.moveTo(x, y);
                else ctx.lineTo(x, y);
            }

            ctx.stroke();
        },

        clear(canvasId) {
            const entry = getCtx(canvasId);
            if (!entry) return;
            const { canvas, ctx } = entry;
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            delete contexts[canvasId];
        }
    };
})();

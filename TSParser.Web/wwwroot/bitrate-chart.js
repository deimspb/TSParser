// #region agent log
window.bitrateChart = {
    getPlotWrapSize: function (element) {
        const rect = element.getBoundingClientRect();
        return {
            width: Math.floor(rect.width),
            height: Math.floor(rect.height)
        };
    },

    attachZoom: function (host) {
        if (!host) return;
        const img = host.querySelector('img.bitrate-chart-plot');
        if (!img) return;

        const revision = img.getAttribute('data-revision') ?? '';
        if (host._zoomState && host._zoomState.revision === revision) return;

        if (host._zoomState?.cleanup) host._zoomState.cleanup();

        let scale = 1;
        let panX = 0;
        let panY = 0;
        const minScale = 1;
        const maxScale = 24;

        const apply = () => {
            img.style.transformOrigin = '0 0';
            img.style.transform = `translate(${panX}px, ${panY}px) scale(${scale})`;
        };

        const onWheel = (e) => {
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
            const next = Math.min(maxScale, Math.max(minScale, scale * factor));
            if (next === scale) return;

            const rect = host.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;
            const ratio = next / scale;
            panX = mx - (mx - panX) * ratio;
            panY = my - (my - panY) * ratio;
            scale = next;
            apply();
        };

        let dragging = false;
        let lastX = 0;
        let lastY = 0;

        const onPointerDown = (e) => {
            if (e.button !== 0 || scale <= 1) return;
            dragging = true;
            lastX = e.clientX;
            lastY = e.clientY;
            host.setPointerCapture(e.pointerId);
            host.style.cursor = 'grabbing';
        };

        const onPointerMove = (e) => {
            if (!dragging) return;
            panX += e.clientX - lastX;
            panY += e.clientY - lastY;
            lastX = e.clientX;
            lastY = e.clientY;
            apply();
        };

        const onPointerUp = (e) => {
            if (!dragging) return;
            dragging = false;
            host.releasePointerCapture(e.pointerId);
            host.style.cursor = scale > 1 ? 'grab' : 'default';
        };

        const onDblClick = () => {
            scale = 1;
            panX = 0;
            panY = 0;
            img.style.transform = '';
            host.style.cursor = 'default';
        };

        host.addEventListener('wheel', onWheel, { passive: false });
        host.addEventListener('pointerdown', onPointerDown);
        host.addEventListener('pointermove', onPointerMove);
        host.addEventListener('pointerup', onPointerUp);
        host.addEventListener('pointercancel', onPointerUp);
        host.addEventListener('dblclick', onDblClick);

        host._zoomState = {
            img,
            revision,
            cleanup: () => {
                host.removeEventListener('wheel', onWheel);
                host.removeEventListener('pointerdown', onPointerDown);
                host.removeEventListener('pointermove', onPointerMove);
                host.removeEventListener('pointerup', onPointerUp);
                host.removeEventListener('pointercancel', onPointerUp);
                host.removeEventListener('dblclick', onDblClick);
            }
        };
    },

    logDom: function () {
        const wrap = document.querySelector('.bitrate-chart-plot-wrap');
        const img = wrap ? wrap.querySelector('img.bitrate-chart-plot') : null;
        const section = document.querySelector('.parser-chart-section');
        const wrapRect = wrap ? wrap.getBoundingClientRect() : null;
        const imgRect = img ? img.getBoundingClientRect() : null;
        const sectionRect = section ? section.getBoundingClientRect() : null;
        const data = {
            viewportWidth: window.innerWidth,
            sectionWidth: sectionRect ? sectionRect.width : 0,
            sectionHeight: sectionRect ? sectionRect.height : 0,
            wrapWidth: wrapRect ? wrapRect.width : 0,
            wrapHeight: wrapRect ? wrapRect.height : 0,
            imgWidth: imgRect ? imgRect.width : 0,
            imgHeight: imgRect ? imgRect.height : 0,
            imgNaturalWidth: img ? img.naturalWidth : 0,
            imgNaturalHeight: img ? img.naturalHeight : 0,
            hasImg: !!img,
            fillRatio: wrapRect && imgRect && wrapRect.width > 0 ? imgRect.width / wrapRect.width : 0
        };
        fetch('http://127.0.0.1:7361/ingest/baa66d48-4d17-45ed-b091-d2ca65d29b65', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Debug-Session-Id': '64f5ce' },
            body: JSON.stringify({
                sessionId: '64f5ce',
                runId: 'post-fix',
                hypothesisId: 'B-C-E',
                location: 'bitrate-chart.js:logDom',
                message: 'DOM layout metrics',
                data,
                timestamp: Date.now()
            })
        }).catch(() => { });
        return data;
    }
};
// #endregion

// #region agent log
window.agentDebug = {
    measureBitrateChart: function () {
        const wrap = document.querySelector('.bitrate-chart-plot-wrap');
        const plot = document.querySelector('.bitrate-chart-plot');
        const canvas = plot ? plot.querySelector('canvas') : null;
        const section = document.querySelector('.parser-chart-section');
        const wrapRect = wrap ? wrap.getBoundingClientRect() : null;
        const plotRect = plot ? plot.getBoundingClientRect() : null;
        const canvasRect = canvas ? canvas.getBoundingClientRect() : null;
        const sectionRect = section ? section.getBoundingClientRect() : null;
        return {
            viewportWidth: window.innerWidth,
            sectionWidth: sectionRect ? sectionRect.width : 0,
            sectionHeight: sectionRect ? sectionRect.height : 0,
            wrapWidth: wrapRect ? wrapRect.width : 0,
            wrapHeight: wrapRect ? wrapRect.height : 0,
            plotWidth: plotRect ? plotRect.width : 0,
            plotHeight: plotRect ? plotRect.height : 0,
            canvasWidth: canvasRect ? canvasRect.width : 0,
            canvasHeight: canvasRect ? canvasRect.height : 0,
            hasCanvas: !!canvas,
            fillRatio: wrapRect && plotRect && wrapRect.width > 0 ? plotRect.width / wrapRect.width : 0
        };
    },
    logBitrateChartDom: function () {
        const data = window.agentDebug.measureBitrateChart();
        fetch('http://127.0.0.1:7361/ingest/baa66d48-4d17-45ed-b091-d2ca65d29b65', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Debug-Session-Id': '64f5ce' },
            body: JSON.stringify({
                sessionId: '64f5ce',
                runId: 'post-fix',
                hypothesisId: 'B-C-E',
                location: 'agent-debug.js:logBitrateChartDom',
                message: 'DOM layout metrics',
                data: data,
                timestamp: Date.now()
            })
        }).catch(() => { });
        return data;
    }
};
// #endregion

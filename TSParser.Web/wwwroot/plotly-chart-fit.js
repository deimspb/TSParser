window.tsParserPlotly = {
    applyAxisRanges: function (xMin, xMax, yMin, yMax) {
        const root = document.querySelector('.bitrate-chart-plot .js-plotly-plot');
        if (!root || !window.Plotly)
            return false;

        window.Plotly.relayout(root, {
            'xaxis.autorange': false,
            'yaxis.autorange': false,
            'xaxis.range': [xMin, xMax],
            'yaxis.range': [yMin, yMax]
        });

        try {
            window.Plotly.Plots.resize(root);
        } catch (_) {
            // ignore resize failures
        }

        return true;
    }
};

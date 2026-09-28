// Lot 089: draws the home page's "files processed per day" chart with Chart.js (wwwroot/lib/chartjs,
// loaded just before this script in App.razor). Called through IActivityChartInterop once the Blazor
// circuit is interactive. Holds no text of its own: every label, tooltip title and total comes
// already translated in the model built by ActivityChartModelBuilder (C#). Colours are read from the
// theme's CSS variables when drawing, and every chart is redrawn when the light/dark theme changes.
//
// model = {
//   labels:        ["30/08", ...]                 x-axis labels, one per day
//   tooltipTitles: ["Sunday, 30 August 2026", ...] tooltip title, one per day
//   totalLabels:   ["3 files", ...]               tooltip footer, one per day
//   datasets:      [{ label: "Successful", status: "success", values: [0, 2, ...] }, ...]
// }
window.amOxoActivityChart = (function () {
    var STATUS_COLOUR_VARIABLES = {
        success: "--m3-success",
        warning: "--m3-warning",
        rejected: "--m3-danger"
    };

    // canvas id -> last model drawn, so a theme change can redraw with the new colours.
    var models = {};

    function themeValue(variable) {
        return window.getComputedStyle(document.documentElement).getPropertyValue(variable).trim();
    }

    function prefersReducedMotion() {
        return window.matchMedia !== undefined
            && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function buildConfiguration(model) {
        var textColour = themeValue("--bs-secondary-color");
        var gridColour = themeValue("--bs-border-color");
        var gapColour = themeValue("--bs-body-bg");

        return {
            type: "bar",
            data: {
                labels: model.labels,
                datasets: model.datasets.map(function (dataset) {
                    var colour = themeValue(STATUS_COLOUR_VARIABLES[dataset.status]);
                    return {
                        label: dataset.label,
                        data: dataset.values,
                        backgroundColor: colour,
                        hoverBackgroundColor: colour,
                        // A thin line in the page colour on top of each segment keeps stacked
                        // statuses apart (lot 088, D5).
                        borderColor: gapColour,
                        borderWidth: { top: 1 },
                        borderSkipped: "bottom",
                        maxBarThickness: 32
                    };
                })
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: prefersReducedMotion() ? false : { duration: 300 },
                interaction: { mode: "index", intersect: false },
                scales: {
                    x: {
                        stacked: true,
                        grid: { display: false },
                        ticks: { color: textColour, autoSkip: true, maxRotation: 0 }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        ticks: { color: textColour, precision: 0 },
                        grid: { color: gridColour }
                    }
                },
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: { color: textColour, boxWidth: 12, boxHeight: 12 }
                    },
                    tooltip: {
                        // Only the statuses present that day; a day with no file shows no tooltip.
                        filter: function (item) { return item.parsed.y > 0; },
                        callbacks: {
                            title: function (items) { return model.tooltipTitles[items[0].dataIndex]; },
                            footer: function (items) { return model.totalLabels[items[0].dataIndex]; }
                        }
                    }
                }
            }
        };
    }

    function render(canvasId, model) {
        var canvas = document.getElementById(canvasId);
        if (canvas === null) {
            return;
        }

        var existing = Chart.getChart(canvas);
        if (existing !== undefined) {
            existing.destroy();
        }

        Chart.defaults.font.family = window.getComputedStyle(document.body).fontFamily;
        new Chart(canvas, buildConfiguration(model));
        models[canvasId] = model;
    }

    function dispose(canvasId) {
        delete models[canvasId];
        var canvas = document.getElementById(canvasId);
        var existing = canvas === null ? undefined : Chart.getChart(canvas);
        if (existing !== undefined) {
            existing.destroy();
        }
    }

    new MutationObserver(function () {
        Object.keys(models).forEach(function (canvasId) {
            render(canvasId, models[canvasId]);
        });
    }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-bs-theme"] });

    return { render: render, dispose: dispose };
})();

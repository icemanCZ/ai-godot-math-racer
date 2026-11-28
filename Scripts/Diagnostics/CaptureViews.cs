using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The names of the captured views that carry a number or a model: <c>mill-5</c>, <c>plot-2-3</c>, <c>zoo-close</c>,
/// <c>plane-rear</c>. <see cref="Game"/> stages the view and <see cref="VisualCapture"/> frames it from the same name.
/// </summary>
internal static class CaptureViews
{
    /// <summary>A view such as <c>mill-5</c> or <c>tower-10</c>: the building and how many deliveries are shown.</summary>
    internal static (int Project, int Stage)? ParseBuildView(string view)
    {
        foreach (
            var (prefix, project) in new[]
            {
                ("mill-", BuildPlans.Mill),
                ("tower-", BuildPlans.Tower),
                ("jetty-", BuildPlans.Jetty),
            }
        )
            if (
                view.StartsWith(prefix)
                && int.TryParse(view[prefix.Length..], out int stage)
                && stage is >= 0 and <= BuildPlans.Trips
            )
                return (project, stage);
        return null;
    }

    /// <summary>"plot-N" shows plot N with all its stages delivered, "plot-N-S" with S of them.</summary>
    internal static (int Plot, int Stage)? ParsePlotView(string view)
    {
        if (!view.StartsWith("plot-"))
            return null;
        var parts = view["plot-".Length..].Split('-');
        if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out int plot) || plot is < 0 or >= PlotPlans.Count)
            return null;
        int stage = PlotPlans.Trips;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out stage) || stage is < 0 or > PlotPlans.Trips))
            return null;
        return (plot, stage);
    }

    /// <summary>
    /// "railway-S" shows the railway with S of its six deliveries made, "railway-close" the finished station and
    /// "railway-pet" the station one delivery short, with the pet waiting.
    /// </summary>
    internal static int? ParseRailwayView(string view) =>
        view == "railway-close" ? RailwayPlans.Trips
        : view == "railway-pet" ? RailwayPlans.Trips - 1
        : view.StartsWith("railway-")
        && int.TryParse(view["railway-".Length..], out int stage)
        && stage is >= 0 and <= RailwayPlans.Trips
            ? stage
        : null;

    /// <summary>
    /// "zoo-S" shows the zoo with S of its six deliveries made, "zoo-close" the finished zoo from the gate and "zoo-pet"
    /// the zoo one delivery short, with the pet waiting.
    /// </summary>
    internal static int? ParseZooView(string view) =>
        view == "zoo-close" ? ZooPlans.Trips
        : view == "zoo-pet" ? ZooPlans.Trips - 1
        : view.StartsWith("zoo-")
        && int.TryParse(view["zoo-".Length..], out int stage)
        && stage is >= 0 and <= ZooPlans.Trips
            ? stage
        : null;

    /// <summary>"hunter-S" shows the hunter's hut with S of its four deliveries made.</summary>
    internal static int? ParseHunterView(string view) =>
        view.StartsWith("hunter-")
        && int.TryParse(view["hunter-".Length..], out int stage)
        && stage is >= 0 and <= HutPlans.Trips
            ? stage
            : null;

    /// <summary>"playground-S" shows the playground with S of its six deliveries made.</summary>
    /// <summary>"playground-S" shows the playground with S of its six deliveries made.</summary>
    internal static int? ParsePlaygroundView(string view) =>
        view.StartsWith("playground-")
        && int.TryParse(view["playground-".Length..], out int stage)
        && stage is >= 0 and <= PlaygroundPlans.Trips
            ? stage
            : null;

    /// <summary>
    /// A view of one model, as <c>plane</c>, <c>plane-decorated</c> (every accessory on) or <c>plane-rear</c> (decorated,
    /// seen from behind); the model is the id of a <see cref="CarBody"/>.
    /// </summary>
    internal static (string Model, bool Decorated, bool Rear)? ParseModelView(string view)
    {
        bool rear = view.EndsWith("-rear"),
            decorated = rear || view.EndsWith("-decorated");
        string model =
            rear ? view[..^"-rear".Length]
            : decorated ? view[..^"-decorated".Length]
            : view;
        return CarBody.All.Any(body => body.Id == model) ? (model, decorated, rear) : null;
    }
}

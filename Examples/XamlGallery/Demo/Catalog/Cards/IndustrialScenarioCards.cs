using Demo.Views.Industrial;

namespace Demo;

/// <summary>The industrial/HMI pages that lead the scenario section.</summary>
internal static class IndustrialScenarioCards
{
    /// <summary>Creates the industrial scenario group.</summary>
    public static IReadOnlyList<GalleryCard> Create()
    {
        return
        [
            Card(
                "scenario.production-line",
                "Production line",
                "A line overview for the shop floor: OEE ring, station states, throughput trend and an alarm feed, all driven by a live simulator.",
                "x:Class=\"Demo.Views.Industrial.ProductionLineView\"",
                _ => new ProductionLineView()),
            Card(
                "scenario.process-flow",
                "Process flow",
                "A tank-and-pipe mimic: vessel levels, pump and valve states, and a flow path that animates between the stages of a batch process.",
                "<Polyline Points=\"{Binding FlowPoints}\" Stroke=\"{StaticResource Accent}\" />",
                _ => new ProcessFlowView()),
            Card(
                "scenario.alarm-center",
                "Alarm center",
                "A prioritized alarm board: severity counts, acknowledgement commands, a state filter and an event log bound to an item template.",
                "<Button Text=\"ACK\" Command=\"{Binding AckCommand}\" />",
                _ => new AlarmCenterView()),
            Card(
                "scenario.machine-panel",
                "Machine panel",
                "A CNC-style operator panel: axis DROs, spindle load, feed override and jog controls that write the position back to the view model.",
                "<Button Text=\"+X\" Click=\"OnJogXPositive\" />",
                _ => new MachinePanelView()),
            Card(
                "scenario.tear-off",
                "Tear-off panel",
                "A line-status panel that lives in the page or in its own Easel window. The same visual subtree moves between two hosts and docks back when dropped on its slot.",
                "window.Content = panel; // the same subtree, a second host",
                _ => new TearOffView()),
        ];
    }

    private static GalleryCard Card(
        string id, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, "Industrial", title, summary, usage), create);
}

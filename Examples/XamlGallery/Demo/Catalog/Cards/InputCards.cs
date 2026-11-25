using Demo.Views;

namespace Demo;

/// <summary>The input card group, authored as visual XAML views.</summary>
internal static class InputCards
{
    /// <summary>Creates the input group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "input.buttons",
            "Input",
            "Button and RepeatButton",
            "Click by pointer, Space or Enter; the repeat button fires while held.",
            "<Button Text=\"Run\" Height=\"32\" Click=\"OnRunClick\" />",
            _ => new ButtonsCard()),
        Card(
            "input.toggles",
            "Input",
            "Toggles",
            "ToggleButton, CheckBox and RadioButton share the checked pipe.",
            "<CheckBox IsChecked=\"True\" />",
            _ => new TogglesCard()),
        Card(
            "input.slider",
            "Input",
            "Slider",
            "Pointer drag and arrow keys change the bounded value.",
            "<Slider Minimum=\"0\" Maximum=\"100\" Value=\"40\" />",
            _ => new SliderCard()),
        Card(
            "input.progress",
            "Input",
            "ProgressBar",
            "A bounded range painted as a track and fill.",
            "<ProgressBar Width=\"240\" Height=\"10\" Maximum=\"100\" />",
            _ => new ProgressCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}

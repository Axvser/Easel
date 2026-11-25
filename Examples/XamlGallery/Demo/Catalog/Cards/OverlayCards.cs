using Demo.Views;

namespace Demo;

/// <summary>The overlay card group, authored as visual XAML views.</summary>
internal static class OverlayCards
{
    /// <summary>Creates the overlay group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "overlay.popup",
            "Overlay",
            "VisualPopup",
            "A child hosted in the surface overlay, above the root tree.",
            "<Button Text=\"Show popup\" Click=\"OnShowPopupClick\" />",
            _ => new PopupCard()),
        Card(
            "overlay.menu",
            "Overlay",
            "VisualMenu",
            "A menu bar with drop-down items and shortcut text.",
            "<VisualMenu><VisualMenuItem Header=\"File\" />…</VisualMenu>",
            _ => new MenuCard()),
        Card(
            "overlay.context-menu",
            "Overlay",
            "Context menu",
            "A detached menu opened at a surface point.",
            "<VisualContextMenu>…</VisualContextMenu>",
            _ => new ContextMenuCard()),
        Card(
            "overlay.dialog",
            "Overlay",
            "Dialogs",
            "A modal scrim and centred card with async message, confirm and prompt results.",
            "await DialogService.MessageAsync(Surface, \"Saved\", \"…\");",
            _ => new DialogCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}

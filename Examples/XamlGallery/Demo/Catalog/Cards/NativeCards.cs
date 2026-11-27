using Demo.Views;

namespace Demo;

/// <summary>The dedicated native WinForms group: real controls hosted in the visual layout, and visual content inside native containers.</summary>
internal static class NativeCards
{
    /// <summary>Creates the native group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "native.workspace",
            "Native",
            "Legacy workspace",
            "An order-entry workspace built from ordinary WinForms controls — a DataGridView over an editable DataTable, a TreeView, NumericUpDown, DateTimePicker and ComboBox, with a ToolStrip and StatusStrip. The visual Grid measures and arranges every island; code-behind only supplies data and events.",
            """
            <Grid Height="240">
              <native:TreeView x:Name="navTree" Width="190" />
              <native:DataGridView x:Name="ordersGrid" Grid.Column="1" />
            </Grid>
            // code-behind
            ordersGrid.Native.DataSource = orders;
            navTree.Native.Nodes.Add("Workspace");
            """,
            _ => new NativeWorkspaceCard()),
        Card(
            "native.embed",
            "Native",
            "Easel inside a native container",
            "A native GroupBox hosts an Easel StackPanel: ProgressBars and text are painted by the visual host inside the native control's display rectangle, and a native button sits in the same visual tree. Selecting a native ListBox item re-renders the visual readout, and a visual button writes back to the native list.",
            """
            <native:GroupBox Text="Live readout" Width="460" Height="260">
              <StackPanel Spacing="8">
                <TextBlock x:Name="meterName" />
                <ProgressBar x:Name="flowBar" Height="8" />
                <native:Button Text="Boost" Click="OnBoostClick" />
              </StackPanel>
            </native:GroupBox>
            """,
            _ => new NativeEmbedCard()),
        Card(
            "native.property",
            "Native",
            "Property editing",
            "A native PropertyGrid edits a settings object through its CLR properties, categories and descriptions. PropertyValueChanged updates the visual summary; the visual Reset button writes the object back and refreshes the grid.",
            """
            <native:PropertyGrid x:Name="settingsGrid" Width="360" Height="300" />
            // code-behind
            settingsGrid.Native.SelectedObject = settings;
            settingsGrid.Native.PropertyValueChanged += OnValueChanged;
            """,
            _ => new NativePropertyCard()),
        Card(
            "native.binding",
            "Native",
            "Native binding",
            "A native TextBox writes its Text two-way through the proxy dependency property. The visual header and preview read the same source, so typing in the native field repaints the Easel text.",
            """
            <native:TextBox Text="{Binding Note, Mode=TwoWay}" />
            <TextBlock Text="{Binding Note}" />
            """,
            _ => new NativeBindingCard()),
        Card(
            "native.host",
            "Native",
            "NativeControlHost",
            "The code-behind path for a control an existing factory returns already configured. NativeControlHost.Child is one rectangular island in the layout; the visual button adds rows to the native ListView and updates the visual status.",
            """
            <NativeControlHost x:Name="host" Width="520" Height="150" />
            // code-behind
            host.Child = BuildListView();
            """,
            _ => new NativeHostCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}

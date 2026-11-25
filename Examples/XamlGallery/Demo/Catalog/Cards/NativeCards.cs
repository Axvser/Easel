using Demo.Views;

namespace Demo;

/// <summary>The dedicated native WinForms group: XAML authoring, layout, binding, composition and the code-behind host.</summary>
internal static class NativeCards
{
    /// <summary>Creates the native group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "native.elements",
            "Native",
            "Native elements",
            "native: controls are wrapped in NativeElement and laid out by the same measure/arrange pass; a native container hosts visual content.",
            """
            <native:GroupBox Text="Host" Width="280" Height="156">
              <StackPanel Spacing="6">
                <TextBlock Text="Visual content in a native container." />
                <native:Button Text="{Binding ButtonText}" Click="OnNativeClick" />
                <native:Button Text="Snapshot" IsSnapshot="True" Opacity="0.5" />
              </StackPanel>
            </native:GroupBox>
            """,
            _ => new NativeElementsCard()),
        Card(
            "native.binding",
            "Native",
            "Native binding",
            "A native CLR property binds through a proxy dependency property; TextChanged writes the source back.",
            """
            <native:TextBox Width="280"
                            Text="{Binding Name, Mode=TwoWay}" />
            <TextBlock Text="{Binding Name}" />
            """,
            _ => new NativeBindingCard()),
        Card(
            "native.composition",
            "Native",
            "Native composition",
            "A live HWND rounds through a binary window region (its native frame cut at the corners); a snapshot clips anti-aliased and keeps a visual border; a popup cuts a hole through the island region.",
            """
            <native:Button Text="Rounded snapshot"
                          IsSnapshot="True"
                          CornerRadius="14"
                          BorderBrush="{StaticResource Accent}"
                          BorderThickness="1" />

            // live HWND: binary window region, the native frame is cut at the corners
            <native:Button Text="Live region" CornerRadius="14" />
            <native:Button Text="Snapshot" IsSnapshot="True" Opacity="0.55" />
            <native:Button x:Name="rotated" IsSnapshot="True" />
            <native:GroupBox x:Name="island" Width="320" Height="108">
              <StackPanel>…visual content…</StackPanel>
            </native:GroupBox>

            // code-behind
            rotated.Transform = Matrix.RotateAt(16, center);
            popup.PlacementTarget = island;
            """,
            _ => new NativeCompositionCard()),
        Card(
            "native.host",
            "Native",
            "NativeControlHost",
            "The code-behind native path: a real HWND floats above the visual tree. Use it when the control is created in code.",
            """
            <NativeControlHost x:Name="host"
                               Width="190" Height="32" />
            // code-behind
            host.Child = new System.Windows.Forms.Button { Text = "Native" };
            """,
            _ => new NativeHostCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}

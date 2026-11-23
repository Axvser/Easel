namespace Demo.Views;

/// <summary>The native-host card; only the WinForms child is created here.</summary>
public sealed partial class NativeHostCard : ThemedStackPanel
{
    /// <summary>Creates the view and attaches the native child.</summary>
    public NativeHostCard()
    {
        InitializeComponent();

        host.Child = new System.Windows.Forms.Button { Text = "Native WinForms button" };
    }
}

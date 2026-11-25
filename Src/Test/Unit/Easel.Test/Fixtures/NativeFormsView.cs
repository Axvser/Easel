using System.Windows.Forms;
using VC = Easel.Visuals.Controls;

namespace Easel.Test.Fixtures;

/// <summary>
/// The code-behind of the native-element fixture: a prefixed <c>native:</c> control with layout properties and an
/// event, and a native container whose content is a visual <see cref="VC.StackPanel"/> with a nested native label.
/// </summary>
public partial class NativeFormsView : VC.StackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public NativeFormsView()
    {
        InitializeComponent();
    }

    /// <summary>How many times the native button's Click handler has run.</summary>
    public int OkClicks { get; private set; }

    /// <summary>The native button wrapper.</summary>
    public VC.NativeElement<Button> OkButton => okButton;

    /// <summary>The native group box wrapper.</summary>
    public VC.NativeElement<GroupBox> Group => group;

    /// <summary>The visual content nested in the native group box.</summary>
    public VC.StackPanel GroupContent => groupContent;

    /// <summary>The native label nested in the visual content.</summary>
    public VC.NativeElement<Label> Leaf => leaf;

    private void OnOkClick(object? sender, EventArgs e) => OkClicks++;
}

using System.Drawing;
using System.Windows.Forms;

namespace Easel.Visuals.Controls;

/// <summary>
/// An almost invisible top-level window that gives a snapshot island real mouse, keyboard and IME input.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot itself is a bitmap in the visual pass. This proxy is a borderless owned form with
/// <see cref="Form.Opacity"/> set to <c>1/255</c>: the OS still hit-tests it (a fully transparent window is
/// click-through), so real input lands on the native control, which fills the proxy. Keyboard and IME come from
/// the real focused control. There is no message forwarding: WinForms controls do not reliably click from a
/// synthetic <c>WM_LBUTTONDOWN/UP</c> when the real pointer is elsewhere.
/// </para>
/// <para>
/// The proxy covers the element's transformed bounding box and the control is laid out at that size. Opacity and
/// translation are exact; a scale becomes a native re-layout at the scaled size (the hit areas and the snapshot
/// agree); a rotation keeps the whole bounding box interactive, so the internal hit position is approximate.
/// </para>
/// </remarks>
internal sealed class NativeSnapshotProxy : Form
{
    private readonly NativeElement _owner;
    private readonly Control _native;
    private bool _shown;

    internal NativeSnapshotProxy(NativeElement owner, Control native)
    {
        _owner = owner;
        _native = native;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = false;
        MaximizeBox = false;
        ControlBox = false;
        Opacity = 1d / 255d;
        BackColor = Color.Magenta;

        Controls.Add(native);
        native.Dock = DockStyle.None;
        native.Visible = true;
    }

    protected override bool ShowWithoutActivation => true;

    internal Control NativeControl => _native;

    internal void ShowFor(Form owner)
    {
        if (_shown || IsDisposed)
        {
            return;
        }

        Owner = owner;
        Show();
        _shown = true;
    }

    internal void Place(Rectangle screenBounds)
    {
        if (IsDisposed)
        {
            return;
        }

        if (Bounds != screenBounds)
        {
            Bounds = screenBounds;
        }

        _native.Visible = true;
        var size = screenBounds.Size;
        var target = new Rectangle(0, 0, Math.Max(1, size.Width), Math.Max(1, size.Height));
        if (_native.Bounds != target)
        {
            _native.Bounds = target;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!IsDisposed && Controls.Contains(_native))
            {
                Controls.Remove(_native);
            }
        }

        base.Dispose(disposing);
    }
}

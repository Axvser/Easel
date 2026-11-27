using System.Drawing;

namespace Easel.Visuals;

/// <summary>A XAML-declarable window that hosts one visual tree and runs with <c>Application.Run</c>.</summary>
/// <remarks>
/// <para>
/// Derive a partial class from this type, put <c>x:Class</c> on a <c>Window</c> root and call
/// <c>InitializeComponent</c> in the constructor. The generated initializer assigns the root element to
/// <see cref="Content"/>; the window derives from <see cref="VisualForm"/>, so the usual
/// <c>Application.Run(new MyWindow())</c> bootstrap applies.
/// </para>
/// <para>
/// <see cref="Title"/> maps to the native <c>Form.Text</c> caption. <see cref="Resources"/> is the hosting
/// surface's dictionary, so resources declared in <c>Window.Resources</c> are visible to the whole tree.
/// </para>
/// <para>
/// XAML <c>Width</c> and <c>Height</c> are device-independent pixels: the initializer writes
/// <see cref="WidthDips"/> and <see cref="HeightDips"/>, and the window converts them with the monitor DPI when
/// its handle is created. <see cref="System.Windows.Forms.Control.Width"/> and
/// <see cref="System.Windows.Forms.Control.Height"/> set in code stay in physical pixels.
/// </para>
/// <para>
/// The native title bar follows <see cref="Theme"/> where DWM is available; add a
/// <see cref="Controls.VisualTitleBar"/> to the content for the custom Easel chrome instead.
/// </para>
/// </remarks>
public class Window : VisualForm
{
    private double _widthDips = double.NaN;
    private double _heightDips = double.NaN;
    private int _widthBeforeDips;
    private int _heightBeforeDips;
    private bool _dipsApplied;

    /// <summary>The authored window width in device-independent pixels, or <see cref="System.Double.NaN"/> when unset.</summary>
    /// <remarks>
    /// The XAML <c>Width</c> attribute writes this property. It is converted with the monitor DPI when the handle
    /// is created; setting it on a live window applies the physical width immediately. Zero, negative and
    /// non-finite values clear the authored width.
    /// </remarks>
    public double WidthDips
    {
        get => _widthDips;
        set
        {
            var normalized = NormalizeDips(value);
            if (SameDips(_widthDips, normalized))
            {
                return;
            }

            _widthDips = normalized;
            if (double.IsNaN(normalized))
            {
                return;
            }

            if (IsHandleCreated)
            {
                ApplyDipsSize(width: true, height: false);
            }
            else
            {
                _widthBeforeDips = Width;
            }
        }
    }

    /// <summary>The authored window height in device-independent pixels, or <see cref="System.Double.NaN"/> when unset.</summary>
    /// <remarks>XAML <c>Height</c> writes this property; it is applied like <see cref="WidthDips"/>.</remarks>
    public double HeightDips
    {
        get => _heightDips;
        set
        {
            var normalized = NormalizeDips(value);
            if (SameDips(_heightDips, normalized))
            {
                return;
            }

            _heightDips = normalized;
            if (double.IsNaN(normalized))
            {
                return;
            }

            if (IsHandleCreated)
            {
                ApplyDipsSize(width: false, height: true);
            }
            else
            {
                _heightBeforeDips = Height;
            }
        }
    }

    /// <summary>The visual root declared under the window, or <see langword="null"/>.</summary>
    public VisualElement? Content
    {
        get => Root;
        set => Root = value;
    }

    /// <summary>The window caption.</summary>
    public string Title
    {
        get => Text;
        set => Text = value;
    }

    /// <summary>The resource dictionary offered to every element in this window's tree.</summary>
    public ResourceDictionary Resources => Surface.Resources;

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_dipsApplied)
        {
            return;
        }

        _dipsApplied = true;

        // 只接管 XAML 设置后没有被 code-behind 改过的物理尺寸；显式 Width/Height 仍按物理像素生效。
        var applyWidth = !double.IsNaN(_widthDips) && Width == _widthBeforeDips;
        var applyHeight = !double.IsNaN(_heightDips) && Height == _heightBeforeDips;
        if (applyWidth || applyHeight)
        {
            ApplyDipsSize(applyWidth, applyHeight);
        }
    }

    private void ApplyDipsSize(bool width, bool height)
    {
        var scale = CurrentDpiScale;
        var newWidth = width ? Math.Max(1, (int)Math.Round(_widthDips * scale)) : Width;
        var newHeight = height ? Math.Max(1, (int)Math.Round(_heightDips * scale)) : Height;
        if (newWidth != Width || newHeight != Height)
        {
            Size = new Size(newWidth, newHeight);
        }
    }

    private float CurrentDpiScale
    {
        get
        {
            var overridden = Surface.DpiScaleOverride;
            if (overridden > 0f)
            {
                return overridden;
            }

#if NETFRAMEWORK
            using var graphics = Graphics.FromHwnd(Handle);
            return graphics.DpiX / 96f;
#else
            return DeviceDpi / 96f;
#endif
        }
    }

    private static double NormalizeDips(double value)
        => double.IsNaN(value) || double.IsInfinity(value) || value <= 0d ? double.NaN : value;

    private static bool SameDips(double left, double right)
        => (double.IsNaN(left) && double.IsNaN(right)) || Math.Abs(left - right) < 0.0001d;
}

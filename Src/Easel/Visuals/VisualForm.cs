using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Easel.Visuals.Controls;

namespace Easel.Visuals;

/// <summary>A WinForms window that hosts one Easel visual tree without manual VisualHost wiring.</summary>
/// <remarks>
/// The form owns one dock-filled <see cref="VisualHost"/>. Set <see cref="Root"/> to any
/// <see cref="VisualElement"/>; a non-container root is wrapped in a dock panel so it fills the client area.
/// The root is not disposed when it is replaced; the caller owns the view and can move it to another host.
/// The native title bar follows <see cref="Theme"/> where DWM is available.
/// <see cref="Window"/> derives from this type so a window can be declared as a XAML root.
/// </remarks>
public class VisualForm : Form
{
    private readonly VisualHost _surface;
    private VisualElement? _root;

    /// <summary>Creates a window with a dark/light-aware surface and no visual root.</summary>
    public VisualForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Theme.Current.WindowBackground;
        _surface = new VisualHost
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Current.WindowBackground,
        };
        Controls.Add(_surface);
        Theme.Changed += OnThemeChanged;
        DataContextChanged += OnDataContextChanged;
        _surface.DataContext = DataContext;
    }

    /// <summary>The single surface hosted by this window.</summary>
    public VisualHost Surface => _surface;

#if !NET8_0_OR_GREATER
    private object? _dataContext;

    /// <summary>The data context inherited by the hosted visual tree.</summary>
    /// <remarks>
    /// On .NET 8 and later this is <see cref="System.Windows.Forms.Control.DataContext"/>. Older target
    /// frameworks expose the same property, event and behaviour here so XAML binds identically on every target.
    /// </remarks>
    public object? DataContext
    {
        get => _dataContext;
        set
        {
            if (ReferenceEquals(_dataContext, value))
            {
                return;
            }

            _dataContext = value;
            DataContextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when <see cref="DataContext"/> changes.</summary>
    public event EventHandler? DataContextChanged;
#endif

    /// <summary>The visual root shown by this window, or <see langword="null"/>.</summary>
    public VisualElement? Root
    {
        get => _root;
        set
        {
            if (ReferenceEquals(_root, value))
            {
                return;
            }

            if (value is not null && (value.ParentElement is not null || value.Surface is not null))
            {
                throw new InvalidOperationException("The visual root is already attached to a visual tree.");
            }

            _root = value;
            _surface.Root = value is null ? null : VisualBootRoot.Wrap(value);
        }
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // 设计器里没有真实的宿主进程，诊断只会误报。
        if (!DesignMode)
        {
            EaselApplication.WarnIfNotPerMonitorV2();
            NativeWindowTheme.ApplyDarkMode(this);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Theme.Changed -= OnThemeChanged;
            DataContextChanged -= OnDataContextChanged;
        }

        base.Dispose(disposing);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        BackColor = Theme.Current.WindowBackground;
        _surface.BackColor = Theme.Current.WindowBackground;
        NativeWindowTheme.ApplyDarkMode(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _surface.DataContext = DataContext;
    }
}

/// <summary>Adds Easel roots to native WinForms control collections without a manual host.</summary>
public static class VisualControlCollectionExtensions
{
    private static readonly ConditionalWeakTable<Control.ControlCollection, Dictionary<VisualElement, VisualHost>> Hosts = new();

    /// <summary>Creates a dock-filled host for <paramref name="root"/> and adds it to the collection.</summary>
    /// <param name="controls">The native control collection.</param>
    /// <param name="root">The Easel root to show; it must not already belong to a visual tree.</param>
    /// <returns>The created host.</returns>
    /// <exception cref="InvalidOperationException">The collection already contains a host, or the root is attached.</exception>
    public static VisualHost Add(this Control.ControlCollection controls, VisualElement root)
    {
        if (controls is null) throw new ArgumentNullException(nameof(controls));
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (root.ParentElement is not null || root.Surface is not null)
        {
            throw new InvalidOperationException("The visual root is already attached to a visual tree.");
        }

        if (FindHost(controls) is not null)
        {
            throw new InvalidOperationException(
                "This native container already hosts an Easel surface; set that surface's Root instead of adding another host.");
        }

        var host = new VisualHost
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Current.WindowBackground,
        };
        try
        {
            host.Root = VisualBootRoot.Wrap(root);
            controls.Add(host);
        }
        catch
        {
            host.Dispose();
            throw;
        }

        Hosts.GetOrCreateValue(controls)[root] = host;
        return host;
    }

    /// <summary>Removes and disposes the host created for <paramref name="root"/>.</summary>
    /// <param name="controls">The native control collection.</param>
    /// <param name="root">The root previously passed to <see cref="Add(Control.ControlCollection, VisualElement)"/>.</param>
    /// <returns><see langword="true"/> when the root had a host in this collection.</returns>
    public static bool Remove(this Control.ControlCollection controls, VisualElement root)
    {
        if (controls is null) throw new ArgumentNullException(nameof(controls));
        if (root is null) throw new ArgumentNullException(nameof(root));

        if (!Hosts.TryGetValue(controls, out var hosts) || !hosts.TryGetValue(root, out var host))
        {
            return false;
        }

        hosts.Remove(root);
        controls.Remove(host);
        host.Dispose();
        return true;
    }

    private static VisualHost? FindHost(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            if (control is VisualHost host)
            {
                return host;
            }
        }

        return null;
    }
}

/// <summary>Wraps a non-container visual so it can be a <see cref="VisualHost.Root"/>.</summary>
internal static class VisualBootRoot
{
    public static ContainerVisual Wrap(VisualElement root)
    {
        if (root is ContainerVisual container)
        {
            return container;
        }

        var panel = new DockPanel { LastChildFill = true };
        panel.Add(root);
        return panel;
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Easel.Visuals;

namespace Easel;

/// <summary>Configures a Windows Forms process that hosts Easel windows.</summary>
/// <remarks>
/// <para>
/// Call <see cref="Initialize"/> once before the first window is created when the project does not use the
/// generated <c>ApplicationConfiguration.Initialize()</c> bootstrap. On .NET Core targets the method selects
/// per-monitor v2 DPI awareness, enables visual styles and selects GDI+ text rendering.
/// </para>
/// <para>
/// DPI awareness is process-wide and can only be selected before the first window exists. On .NET Framework the
/// method cannot change it at runtime; use an application manifest that declares <c>PerMonitorV2</c>.
/// </para>
/// <para>
/// Windows Forms gates <c>Control.DeviceDpi</c> and DPI change notifications on the process awareness, so a
/// per-window <c>SetThreadDpiAwarenessContext</c> call does not make an Easel window follow monitors.
/// </para>
/// </remarks>
public static class EaselApplication
{
    private const int PerMonitorV2Context = -4;
    private static int _dpiWarningReported;

    /// <summary>Resources shared by every Easel window and element in this process.</summary>
    /// <remarks>
    /// The lookup chain is element, ancestors, host, then this dictionary. Merge application dictionaries here
    /// before creating the first window, so XAML <c>{StaticResource}</c> resolves while the tree is built. The
    /// host's built-in theme lives in <see cref="VisualHost.Resources"/>, so an application key does not replace
    /// a palette key of the same name.
    /// </remarks>
    public static ResourceDictionary Resources { get; } = new();

    /// <summary>Implicit styles shared by every Easel element in this process.</summary>
    /// <remarks>
    /// An element's own <see cref="VisualElement.Styles"/> and its ancestors' collections win over the host's
    /// <see cref="VisualHost.Styles"/>, which wins over this collection; the theme default is the final fallback.
    /// Add styles before creating the first window: collections are resolved when the tree is built.
    /// </remarks>
    public static VisualStyleCollection Styles { get; } = new();

    /// <summary>Initializes the Windows Forms process for an Easel window.</summary>
    /// <remarks>
    /// Call it before the first window is created. On .NET Core targets this selects
    /// <c>HighDpiMode.PerMonitorV2</c>; a DPI mode selected after a window exists has no effect. On .NET Framework
    /// only the visual-style and text-rendering settings are applied.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The first window already exists.</exception>
    public static void Initialize()
    {
#if !NETFRAMEWORK
        // 已被 manifest 或 ApplicationConfiguration 设过时返回 false；此时不改动调用方已经定下的模式。
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#endif
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }

    // 返回进程是否真的处于 PMv2：WinForms 的 DeviceDpi 与 WM_DPICHANGED 处理都以进程意识为门控。
    internal static bool IsPerMonitorV2Active()
    {
        try
        {
            if (GetProcessDpiAwareness(IntPtr.Zero, out var awareness) != 0
                || awareness != ProcessDpiAwareness.PerMonitor)
            {
                return false;
            }

            var context = GetThreadDpiAwarenessContext();
            return context != IntPtr.Zero
                   && AreDpiAwarenessContextsEqual(context, new IntPtr(PerMonitorV2Context));
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    // 每个进程只报一次；Trace 不改变调用方行为，只让错配可见。
    internal static void WarnIfNotPerMonitorV2()
    {
        if (Interlocked.Exchange(ref _dpiWarningReported, 1) != 0)
        {
            return;
        }

        if (IsPerMonitorV2Active())
        {
            return;
        }

        Trace.TraceWarning(
            "Easel: the process is not per-monitor v2 DPI aware. Set <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode> "
            + "or call EaselApplication.Initialize() before the first window; on .NET Framework use an application manifest. "
            + "Windows Forms does not follow monitor DPI changes in any other mode.");
    }

    private enum ProcessDpiAwareness
    {
        Unaware = 0,
        SystemAware = 1,
        PerMonitor = 2,
    }

    [DllImport("shcore.dll")]
    private static extern int GetProcessDpiAwareness(IntPtr process, out ProcessDpiAwareness awareness);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
}

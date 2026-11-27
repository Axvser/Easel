using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Easel.Visuals;

/// <summary>Applies the Easel theme to a window's native chrome where Windows exposes it.</summary>
internal static class NativeWindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeOld = 19;

    /// <summary>Switches the native title bar between dark and light to match <see cref="Theme.EffectiveMode"/>.</summary>
    /// <param name="form">The window whose title bar should follow the theme.</param>
    /// <remarks>
    /// The attribute exists from Windows 10 1903 (20) or 1809 (19); both are tried because the newer value fails
    /// on older builds. Without DWM (Server Core, older systems) the call is a silent no-op.
    /// </remarks>
    public static void ApplyDarkMode(Form form)
    {
        if (!form.IsHandleCreated || form.IsDisposed)
        {
            return;
        }

        var dark = Theme.EffectiveMode == ThemeMode.Light ? 0 : 1;
        try
        {
            if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeOld, ref dark, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // 没有 DWM 时保持系统默认标题栏；窗口其余行为不受影响。
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

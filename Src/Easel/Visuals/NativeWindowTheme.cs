using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Easel.Visuals;

/// <summary>Applies the Easel theme to a window's native chrome where Windows exposes it.</summary>
internal static class NativeWindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeOld = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    // DWM 默认色：让系统自己决定，用于 Light+ 与高对比度。
    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);

    // DWMWA_BORDER_COLOR / CAPTION_COLOR / TEXT_COLOR 从 Windows 11 (22000) 起才有。
    private const int Windows11Build = 22000;

    // 让原生标题栏跟随主题；Windows 11 上同时固定标题、边框与文字颜色，首帧不会先画系统浅色。
    // 20 是 Win10 1903 / Win11，19 是 1809，20 失败时再试 19；没有 DWM 时静默跳过。
    public static void ApplyDarkMode(Form form)
    {
        if (!form.IsHandleCreated || form.IsDisposed)
        {
            return;
        }

        var mode = Theme.EffectiveMode;
        var dark = mode == ThemeMode.Light ? 0 : 1;
        try
        {
            if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeOld, ref dark, sizeof(int));
            }

            ApplyFrameColors(form.Handle, mode);
        }
        catch (DllNotFoundException)
        {
            // 没有 DWM 时保持系统默认标题栏；窗口其余行为不受影响。
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    // 只给 Dark+ 显式上色：Light+ 与高对比度交回系统；切回浅色时用默认值清掉先前写入的颜色。
    private static void ApplyFrameColors(IntPtr hwnd, ThemeMode mode)
    {
        if (Environment.OSVersion.Version.Build < Windows11Build)
        {
            return;
        }

        int border;
        int caption;
        int text;
        if (mode == ThemeMode.Dark)
        {
            var palette = Theme.Current;
            border = ToColorRef(palette.WindowBackground);
            caption = border;
            text = ToColorRef(palette.Foreground);
        }
        else
        {
            border = DwmwaColorDefault;
            caption = DwmwaColorDefault;
            text = DwmwaColorDefault;
        }

        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
    }

    // DWM 的窗口颜色是 COLORREF：0x00BBGGRR，不是 .NET 的 0xAARRGGBB。
    private static int ToColorRef(Color color)
        => (color.R & 0xFF) | ((color.G & 0xFF) << 8) | ((color.B & 0xFF) << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

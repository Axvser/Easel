using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Easel.Visuals;
using IndustrialWorkbench.Controls;
using IndustrialWorkbench.ViewModels;

namespace IndustrialWorkbench;

// 开发用启动选项：--smoke 显示一次后自行关闭，--capture 抓一张图后关闭，--selftest 注入指针验证交互。
internal static class WorkbenchCapture
{
    /// <summary>Schedules the startup automation selected by the command line.</summary>
    public static void Configure(MainForm form, string[] args)
    {
        if (ConfigureCapture(form, args) || ConfigureSelfTest(form, args))
        {
            return;
        }

        ConfigureSmoke(form, args);
    }

    // --selftest：注入拖动/点击，确认自绘控件、双向绑定和报警确认都真的生效。
    private static bool ConfigureSelfTest(MainForm form, string[] args)
    {
        if (!HasFlag(args, "--selftest"))
        {
            return false;
        }

        var directory = ReadOption(args, "--selftest") ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(directory);
        form.Shown += (_, _) => form.BeginInvoke(() =>
        {
            var report = new StringBuilder();
            try
            {
                if (form.Surface.Root is not WorkbenchShell shell)
                {
                    report.AppendLine("FAIL: shell missing");
                }
                else
                {
                    shell.SelectEntry("control.knob");
                    Pump(form, 8);
                    var knob = Find<Knob>(form.Surface.Root as VisualElement);
                    if (knob is null)
                    {
                        report.AppendLine("FAIL: knob not found");
                    }
                    else
                    {
                        var before = form.Model.Setpoint;
                        var bounds = knob.AbsoluteBounds;
                        var scale = form.Surface.DpiScale;
                        var cx = bounds.X + (bounds.Width / 2f);
                        var cy = bounds.Y + (bounds.Height / 2f);
                        var radius = Math.Min(bounds.Width, bounds.Height) * 0.32f;
                        var left = new PointF((cx - radius) * scale, cy * scale);
                        var top = new PointF(cx * scale, (cy - radius) * scale);
                        var down = form.Surface.ProcessPointerDown(left, MouseButtons.Left);
                        form.Surface.ProcessPointerMove(top);
                        form.Surface.ProcessPointerUp(top, MouseButtons.Left);
                        Pump(form, 4);
                        report.AppendLine(
                            $"knob hit={down} before={before:0.0} after={form.Model.Setpoint:0.0} " +
                            $"value={knob.Value:0.0} twoWay={Math.Abs(knob.Value - form.Model.Setpoint) < 0.01}");
                    }

                    shell.SelectEntry(ControlCatalog.OverviewId);
                    Pump(form, 8);
                    var toggle = Find<ToggleSwitch>(form.Surface.Root as VisualElement);
                    if (toggle is null)
                    {
                        report.AppendLine("FAIL: toggle not found");
                    }
                    else
                    {
                        var before = form.Model.PumpOn;
                        Click(form, toggle.AbsoluteBounds);
                        Pump(form, 3);
                        report.AppendLine($"toggle before={before} after={form.Model.PumpOn} flipped={before != form.Model.PumpOn}");
                    }

                    var valve = Find<Valve>(form.Surface.Root as VisualElement);
                    if (valve is null)
                    {
                        report.AppendLine("FAIL: valve not found");
                    }
                    else
                    {
                        var before = form.Model.ValveOpen;
                        Click(form, valve.AbsoluteBounds);
                        Pump(form, 3);
                        report.AppendLine($"valve before={before} after={form.Model.ValveOpen} flipped={before != form.Model.ValveOpen}");
                    }

                    var banner = Find<AlarmBanner>(form.Surface.Root as VisualElement);
                    if (banner is null)
                    {
                        report.AppendLine("FAIL: banner not found");
                    }
                    else
                    {
                        var before = form.Model.AlarmActive;
                        Click(form, banner.AbsoluteBounds);
                        Pump(form, 3);
                        report.AppendLine($"alarm before={before} after={form.Model.AlarmActive} acked={form.Model.AlarmAcknowledged}");
                    }
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("FAIL: " + ex);
            }

            File.WriteAllText(Path.Combine(directory, "selftest.txt"), report.ToString());
            form.Close();
        });

        return true;
    }

    private static void Click(MainForm form, RectangleF logicalBounds)
    {
        var scale = form.Surface.DpiScale;
        var center = new PointF(
            (logicalBounds.X + (logicalBounds.Width / 2f)) * scale,
            (logicalBounds.Y + (logicalBounds.Height / 2f)) * scale);
        form.Surface.ProcessPointerDown(center, MouseButtons.Left);
        form.Surface.ProcessPointerUp(center, MouseButtons.Left);
    }

    private static T? Find<T>(VisualElement? root) where T : VisualElement
    {
        if (root is T match)
        {
            return match;
        }

        if (root is null)
        {
            return null;
        }

        foreach (var child in root.Children)
        {
            if (child is VisualElement element && Find<T>(element) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // 冒烟模式：窗口显示一次，1.2 秒后自行关闭，运行完不留窗口。
    private static void ConfigureSmoke(MainForm form, string[] args)
    {
        if (!HasFlag(args, "--smoke"))
        {
            return;
        }

        form.Shown += (_, _) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                form.Close();
            };
            timer.Start();
        };
    }

    // --capture [dir] [--entry id]：抓一张 overview.png 或某控件详情页后关闭。
    private static bool ConfigureCapture(MainForm form, string[] args)
    {
        var start = Array.FindIndex(args, a => string.Equals(a, "--capture", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return false;
        }

        var directory = start + 1 < args.Length && !args[start + 1].StartsWith('-')
            ? args[start + 1]
            : Path.Combine(AppContext.BaseDirectory, "captures");
        Directory.CreateDirectory(directory);

        var entry = ReadOption(args, "--entry");
        var file = string.IsNullOrEmpty(entry) ? "overview" : SafeName(entry);

        form.Shown += (_, _) => form.BeginInvoke(() =>
        {
            try
            {
                if (!string.IsNullOrEmpty(entry) && form.Surface.Root is WorkbenchShell shell)
                {
                    shell.SelectEntry(entry);
                }

                Pump(form, 24);
                Capture(form, Path.Combine(directory, file + ".png"));
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(directory, "capture-error.txt"), ex.ToString());
            }
            finally
            {
                form.Close();
            }
        });

        return true;
    }

    private static void Pump(MainForm form, int frames)
    {
        form.Activate();
        for (var frame = 0; frame < frames; frame++)
        {
            Application.DoEvents();
            Thread.Sleep(30);
        }
    }

    private static void Capture(MainForm form, string path)
    {
        // PrintWindow 直接让窗口画进内存 DC，不依赖桌面坐标；失败时退回视觉树离屏渲染。
        var bounds = form.Bounds;
        using var bitmap = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var hdc = graphics.GetHdc();
            bool captured;
            try
            {
                captured = PrintWindow(form.Handle, hdc, 2);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            if (!captured)
            {
                graphics.Clear(form.Surface.BackColor);
                form.Surface.RenderTo(graphics, form.Surface.ClientSize);
            }
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith('-')
            ? args[index + 1]
            : null;
    }

    private static bool HasFlag(string[] args, string name)
        => Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string SafeName(string id)
        => string.Join('-', id.Split(['.', '/', '\\', ' ', ':'], StringSplitOptions.RemoveEmptyEntries));

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}

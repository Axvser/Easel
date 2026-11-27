using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Easel.Visuals;

namespace Demo;

// 开发用截图/启动选项：--capture 抓图后自行关闭，--smoke 显示一次后关闭，其余选项在窗口显示后应用。
internal static class GalleryCapture
{
    /// <summary>Schedules the startup automation selected by the command line.</summary>
    public static void Configure(MainForm form, string[] args)
    {
        if (ConfigureCapture(form, args))
        {
            return;
        }

        ConfigureSmoke(form, args);
        ConfigureSelection(form, args);
    }

    // --entry / --group / --scroll / --preview-only / --toggle-theme 用于交互启动。
    private static void ConfigureSelection(MainForm form, string[] args)
    {
        var session = form.Session;
        var id = ReadEntryId(session, args);
        var scroll = ReadInt(args, "--scroll", 0);
        var previewOnly = HasFlag(args, "--preview-only");
        if (id is null && scroll <= 0 && !previewOnly)
        {
            return;
        }

        form.Shown += (_, _) => form.BeginInvoke(() =>
        {
            if (form.IsDisposed)
            {
                return;
            }

            if (previewOnly)
            {
                session.SetChromeVisible(false);
            }

            if (HasFlag(args, "--toggle-theme"))
            {
                session.ToggleTheme();
            }

            if (id is not null)
            {
                session.SelectEntry(id);
            }

            if (scroll > 0)
            {
                session.SetScrollOffset(scroll);
            }

            form.Surface.InvalidateSurface();
        });
    }

    // 冒烟模式：窗口真正显示一次，1.2 秒后自己关掉，运行完不留窗口。
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

    // 截图模式：窗口显示后逐条抓图，抓完自己关掉。
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

        var session = form.Session;
        var targets = ResolveTargets(session, args);
        var scroll = ReadInt(args, "--scroll", 0);
        var dump = HasFlag(args, "--dump");
        var previewOnly = HasFlag(args, "--preview-only");
        var themeCycle = ReadIntList(args, "--theme-cycle");

        PlaceOnCaptureScreen(form);
        form.TopMost = true;

        form.Shown += (_, _) => form.BeginInvoke(() =>
        {
            try
            {
                if (previewOnly)
                {
                    session.SetChromeVisible(false);
                }

                if (HasFlag(args, "--toggle-theme"))
                {
                    session.ToggleTheme();
                    Pump(form, 4);
                }

                for (var index = 0; index < targets.Count; index++)
                {
                    var id = targets[index];
                    if (!session.SelectEntry(id))
                    {
                        File.WriteAllText(
                            Path.Combine(directory, $"capture-error-{SafeName(id)}.txt"),
                            $"Entry '{id}' was not found.");
                        continue;
                    }

                    Pump(form, index == 0 ? 24 : 8);

                    // 先让新卡完成布局，尺寸算出后偏移才有可夹取的范围。
                    if (scroll > 0 && targets.Count == 1)
                    {
                        session.SetScrollOffset(scroll);
                        Pump(form, 4);
                    }

                    // 同一进程里按给定顺序切主题，验证的是原地切换而不是重建后的新控件。
                    if (themeCycle.Count > 0)
                    {
                        for (var step = 0; step < themeCycle.Count; step++)
                        {
                            var themeIndex = themeCycle[step];
                            session.SelectTheme(themeIndex);
                            Pump(form, 8);

                            var themed = targets.Count == 1
                                ? $"{SafeName(id)}-theme{step}-{themeIndex}"
                                : $"{index:00}-{SafeName(id)}-theme{step}-{themeIndex}";
                            Capture(form, Path.Combine(directory, themed + ".png"));
                            if (dump)
                            {
                                WriteDump(form, Path.Combine(directory, themed + ".txt"));
                            }
                        }

                        continue;
                    }

                    var name = targets.Count == 1 ? SafeName(id) : $"{index:00}-{SafeName(id)}";
                    Capture(form, Path.Combine(directory, name + ".png"));

                    if (dump)
                    {
                        WriteDump(form, Path.Combine(directory, name + ".txt"));
                    }
                }
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

    private static List<string> ResolveTargets(GallerySession session, string[] args)
    {
        var cards = session.Cards;
        if (HasFlag(args, "--all"))
        {
            return cards.Select(static card => card.Id).ToList();
        }

        var group = ReadOption(args, "--group");
        if (group is not null)
        {
            return cards
                .Where(card => InSectionOrGroup(card, group))
                .Select(static card => card.Id)
                .ToList();
        }

        var entry = ReadOption(args, "--entry");
        if (entry is not null)
        {
            return [entry];
        }

        return cards.Count > 0 ? [cards[0].Id] : [];
    }

    private static string? ReadEntryId(GallerySession session, string[] args)
    {
        var entry = ReadOption(args, "--entry");
        if (entry is not null)
        {
            return entry;
        }

        var group = ReadOption(args, "--group");
        if (group is null)
        {
            return null;
        }

        return session.Cards
            .FirstOrDefault(card => InSectionOrGroup(card, group))
            ?.Id;
    }

    // --group 接受分区名（Scenarios / Easel native / WinForms native）或组名（Industrial / Data …）。
    private static bool InSectionOrGroup(GalleryCard card, string name)
        => string.Equals(card.Group, name, StringComparison.OrdinalIgnoreCase)
           || string.Equals(GalleryTaxonomy.SectionOf(card.Group), name, StringComparison.OrdinalIgnoreCase);

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith('-')
            ? args[index + 1]
            : null;
    }

    private static int ReadInt(string[] args, string name, int fallback)
    {
        var text = ReadOption(args, name);
        return int.TryParse(text, out var value) ? value : fallback;
    }

    private static List<int> ReadIntList(string[] args, string name)
    {
        var text = ReadOption(args, name);
        if (text is null)
        {
            return [];
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => int.TryParse(part, out var value) ? value : -1)
            .Where(static value => value >= 0)
            .ToList();
    }

    private static void WriteDump(MainForm form, string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"window {form.Bounds}");
        if (form.Surface.Root is VisualElement root)
        {
            Dump(root, builder, 0);
        }

        File.WriteAllText(path, builder.ToString());
    }

    private static bool HasFlag(string[] args, string name)
        => Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static void PlaceOnCaptureScreen(MainForm form)
    {
        var screen = Screen.AllScreens.FirstOrDefault(static candidate => !candidate.Primary)
                     ?? Screen.PrimaryScreen
                     ?? Screen.AllScreens[0];
        var area = screen.WorkingArea;

        // 句柄创建时 Window 才套用 DIP 尺寸；先建句柄，后面按抓图屏裁出的物理矩形才不会被覆盖。
        _ = form.Handle;

        // 窗口尺寸按主屏算，抓图屏可能更小；不裁到目标屏就会截到未绘制的窗框区域。
        var width = Math.Min(form.Width, Math.Max(900, area.Width - 24));
        var height = Math.Min(form.Height, Math.Max(600, area.Height - 24));

        form.StartPosition = FormStartPosition.Manual;
        form.Bounds = new Rectangle(
            area.X + Math.Max(0, (area.Width - width) / 2),
            area.Y + Math.Max(0, (area.Height - height) / 2),
            width,
            height);
    }

    private static string SafeName(string id)
        => string.Join('-', id.Split(['.', '/', '\\', ' ', ':'], StringSplitOptions.RemoveEmptyEntries));

    private static void Pump(MainForm form, int frames)
    {
        form.Activate();
        for (var frame = 0; frame < frames; frame++)
        {
            Application.DoEvents();
            Thread.Sleep(frames > 8 ? 40 : 30);
        }
    }

    private static void Capture(MainForm form, string path)
    {
        // PrintWindow 直接让窗口画进内存 DC，不依赖桌面坐标；DPI 缩放下不会截到窗口外。
        var bounds = form.Bounds;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    private static void Dump(VisualElement element, StringBuilder builder, int depth)
    {
        builder.Append(new string(' ', depth * 2));
        builder.Append(element.GetType().Name);
        if (!string.IsNullOrEmpty(element.Name))
        {
            builder.Append(' ').Append('#').Append(element.Name);
        }

        builder.Append(' ').Append(element.AbsoluteBounds);
        builder.Append(" vis=").Append(element.Visibility);
        builder.Append(" r=").Append(element.CornerRadius);
        builder.Append(" min=").Append(element.MinHeight);
        if (element.Background is { } background)
        {
            builder.Append(" bg=").Append(background.ApproximateColor.ToArgb().ToString("X8"));
        }

        if (element.BorderBrush is { } border)
        {
            builder.Append(" border=").Append(border.ApproximateColor.ToArgb().ToString("X8"));
        }

        builder.AppendLine();
        foreach (var child in element.Children)
        {
            if (child is VisualElement visual)
            {
                Dump(visual, builder, depth + 1);
            }
        }
    }
}

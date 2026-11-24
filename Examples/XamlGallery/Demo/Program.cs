using System.Windows.Forms;
using Easel;

namespace Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // VeloxDev.WinForms 用模块初始化器把过渡引擎注入 Easel，而模块初始化器只在程序集真正被加载时运行。
        // 引用它不等于加载它，所以先按名字碰一下；gallery 只用了适配器的动画后端，这里显式登记一次。
        VeloxDev.WinForms.XamlAnimation.EaselAnimationBootstrap.EnsureRegistered();

        // WinForms 的原生暗色在句柄创建时决定；--light / --contrast 让整个进程从一开始就用那套调色板。
        var mode = args.Contains("--system", StringComparer.OrdinalIgnoreCase)
            ? ThemeMode.System
            : args.Contains("--contrast", StringComparer.OrdinalIgnoreCase)
                ? ThemeMode.HighContrast
                : args.Contains("--light", StringComparer.OrdinalIgnoreCase)
                    ? ThemeMode.Light
                    : ThemeMode.Dark;
        Theme.Mode = mode;

        Application.SetColorMode(
            Theme.EffectiveMode == ThemeMode.Light ? SystemColorMode.Classic : SystemColorMode.Dark);

        using var form = new GalleryForm(args);
        GalleryCapture.ApplyStartup(form, args);
        if (GalleryCapture.TryRun(form, args))
        {
            // 截图模式：窗口显示后逐条抓图，抓完自己关掉。
            Application.Run(form);
            return;
        }

        if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
        {
            // 冒烟模式：窗口真正显示一次，1.2 秒后自己关掉，运行完不留窗口。
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                form.Close();
            };
            form.Shown += (_, _) => timer.Start();
            Application.Run(form);
            Environment.ExitCode = 0;
            return;
        }

        Application.Run(form);
    }
}

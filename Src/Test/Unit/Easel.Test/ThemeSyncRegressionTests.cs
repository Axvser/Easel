using System.Drawing;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The palette toggle syncs theme resources in place and keeps cached and direct frames identical.</summary>
[STATestClass]
public sealed class ThemeSyncRegressionTests
{
    [TestMethod]
    public void SyncResourceDictionary_MutatesBrushesInPlaceWithoutRaisingChanged()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            var resources = new ResourceDictionary();
            Theme.SyncResourceDictionary(resources);

            var window = (resources["Window"] as SolidColorBrush)!;
            var darkWindow = window.Color;

            var changes = 0;
            resources.Changed += _ => changes++;

            Theme.Mode = ThemeMode.Light;
            Theme.SyncResourceDictionary(resources);

            Assert.AreEqual(0, changes, "unchanged keys raise no dictionary Changed");
            Assert.AreSame(window, resources["Window"], "the brush instance survives the switch");
            Assert.AreNotEqual(darkWindow.ToArgb(), window.Color.ToArgb(), "the shared brush changed colour in place");
            Assert.AreEqual(Theme.CreateLight().WindowBackground.ToArgb(), window.Color.ToArgb());

            Theme.Mode = ThemeMode.HighContrast;
            Theme.SyncResourceDictionary(resources);
            Assert.AreSame(window, resources["Window"]);
            Assert.AreEqual(Color.Black.ToArgb(), window.Color.ToArgb());
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    [TestMethod]
    public void SyncResourceDictionary_DoesNotCorruptTheStaticThemeBrushes()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            var target = new ResourceDictionary();
            Theme.SyncResourceDictionary(target);

            Theme.Mode = ThemeMode.Light;
            Theme.SyncResourceDictionary(target);
            ((SolidColorBrush)target["Window"]!).Color = Color.Magenta;

            Assert.AreEqual(
                Color.FromArgb(0xFF, 0x14, 0x14, 0x14).ToArgb(),
                Theme.CreateDark().WindowBackground.ToArgb(),
                "the caller's brush edits never reach the static theme source");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    [TestMethod]
    public void PaletteToggle_DirectFrameMatchesTheCachedFrameWithinAlphaRounding()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var host = new VisualHost { Size = new Size(200, 80), DpiScaleOverride = 1f };
            host.Root = new VC.Button { Width = 140, Height = 40, Text = "go" };

            // 第一次整帧走直画路径；切主题后再次整帧，随后一次无脏区的渲染会补建缓存并贴回。
            using var direct = Render(host, 200, 80);
            Theme.Mode = ThemeMode.Light;
            using var directAfterSwitch = Render(host, 200, 80);
            using var cachedAfterSwitch = Render(host, 200, 80);

            AssertBitmapsEqual(directAfterSwitch, cachedAfterSwitch);
            Assert.IsFalse(SamePixels(direct, directAfterSwitch), "the palette switch is visible in the frame");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    [TestMethod]
    public void PaletteToggle_KeepsPartialFramesCorrectAfterADirectFullFrame()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var host = new VisualHost { Size = new Size(200, 80), DpiScaleOverride = 1f };
            var button = new VC.Button { Width = 140, Height = 40, Text = "go" };
            host.Root = button;

            using var dark = Render(host, 200, 80);
            using var darkCached = Render(host, 200, 80);
            Theme.Mode = ThemeMode.Light;
            using var light = Render(host, 200, 80);

            // 直画整帧后缓存被标为过期；局部脏区帧必须先补建缓存，结果仍应反映当前主题与局部变化。
            button.Width = 120;
            using var resized = Render(host, 200, 80);
            Assert.IsFalse(SamePixels(light, resized), "the partial frame carries the resize");

            Theme.Mode = ThemeMode.Dark;
            using var darkAgain = Render(host, 200, 80);
            Assert.IsFalse(SamePixels(light, darkAgain), "the next export shows the new palette");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    private static Bitmap Render(VisualHost host, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        host.RenderTo(graphics, new Size(width, height));
        return bitmap;
    }

    private static void AssertBitmapsEqual(Bitmap expected, Bitmap actual)
    {
        Assert.AreEqual(expected.Width, actual.Width);
        Assert.AreEqual(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var left = expected.GetPixel(x, y);
                var right = actual.GetPixel(x, y);
                // 直画进调用方位图与经 PArgb 缓存贴回在抗锯齿边缘可能差一级（预乘存储的舍入）。
                Assert.IsTrue(
                    Math.Abs(left.R - right.R) <= 1
                    && Math.Abs(left.G - right.G) <= 1
                    && Math.Abs(left.B - right.B) <= 1
                    && Math.Abs(left.A - right.A) <= 1,
                    $"pixel ({x},{y}) differs by more than one level between the direct and cached frames");
            }
        }
    }

    private static bool SamePixels(Bitmap left, Bitmap right)
    {
        if (left.Width != right.Width || left.Height != right.Height)
        {
            return false;
        }

        for (var y = 0; y < left.Height; y++)
        {
            for (var x = 0; x < left.Width; x++)
            {
                if (left.GetPixel(x, y).ToArgb() != right.GetPixel(x, y).ToArgb())
                {
                    return false;
                }
            }
        }

        return true;
    }
}

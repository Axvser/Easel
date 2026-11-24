using System.Drawing;

namespace Easel;

// 每帧的纯色填充不建新的 GDI+ 画笔：颜色是小集合，画笔按颜色缓存到当前线程。
// 缓存项不归调用方所有，不能 Dispose；超出上限时退回一次性的画笔，由调用方释放。
internal static class NativeBrushPool
{
    private const int MaxEntries = 128;

    [ThreadStatic]
    private static Dictionary<int, SolidBrush>? cache;

    /// <summary>Returns a brush for <paramref name="color"/>; dispose it only when <paramref name="owns"/> is true.</summary>
    internal static SolidBrush Get(Color color, out bool owns)
    {
        owns = false;
        if (color.A == 0)
        {
            owns = true;
            return new SolidBrush(Color.Transparent);
        }

        var map = cache;
        if (map is null)
        {
            map = [];
            cache = map;
        }

        var key = color.ToArgb();
        if (map.TryGetValue(key, out var brush))
        {
            return brush;
        }

        if (map.Count >= MaxEntries)
        {
            owns = true;
            return new SolidBrush(color);
        }

        brush = new SolidBrush(color);
        map[key] = brush;
        return brush;
    }

    /// <summary>Returns a cached brush for a fully opaque or translucent colour.</summary>
    internal static SolidBrush Get(Color color) => Get(color, out _);
}

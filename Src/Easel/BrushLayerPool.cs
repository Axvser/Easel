using System.Drawing;
using System.Drawing.Imaging;

namespace Easel;

/// <summary>Reuses the offscreen layers <see cref="CrossFadeBrush"/> composes into.</summary>
/// <remarks>
/// A composed brush needs two layers per paint, and a running animation paints every frame; without reuse that is
/// two full-size allocations per frame. Layers are matched by size, cleared on rent and only retained while the
/// pool stays under both its entry and byte caps, so a one-off large surface does not pin memory.
/// </remarks>
internal static class BrushLayerPool
{
    private const int MaxRetainedBitmaps = 4;
    private const long MaxRetainedBytes = 8L * 1024 * 1024;

    private static readonly List<Bitmap> Bitmaps = [];
    private static readonly object Sync = new();
    private static long _retainedBytes;

    /// <summary>Returns a transparent layer of exactly <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
    /// <param name="width">The layer width in pixels.</param>
    /// <param name="height">The layer height in pixels.</param>
    public static Bitmap Rent(int width, int height)
    {
        var pixelWidth = Math.Max(1, width);
        var pixelHeight = Math.Max(1, height);

        lock (Sync)
        {
            for (var index = Bitmaps.Count - 1; index >= 0; index--)
            {
                var candidate = Bitmaps[index];
                if (candidate.Width != pixelWidth || candidate.Height != pixelHeight)
                {
                    continue;
                }

                Bitmaps.RemoveAt(index);
                _retainedBytes -= ByteSize(candidate);
                Clear(candidate);
                return candidate;
            }
        }

        return new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppPArgb);
    }

    /// <summary>Returns a layer to the pool, or disposes it when the pool is full.</summary>
    /// <param name="bitmap">The layer to return; <see langword="null"/> is ignored.</param>
    public static void Return(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        var size = ByteSize(bitmap);
        if (size > MaxRetainedBytes)
        {
            bitmap.Dispose();
            return;
        }

        lock (Sync)
        {
            // 满了就淘汰最早的一张：动画期间反复用到的总是最近那个尺寸，留下的应该是它。
            while (Bitmaps.Count >= MaxRetainedBitmaps
                   || (_retainedBytes + size > MaxRetainedBytes && Bitmaps.Count > 0))
            {
                var oldest = Bitmaps[0];
                Bitmaps.RemoveAt(0);
                _retainedBytes -= ByteSize(oldest);
                oldest.Dispose();
            }

            Bitmaps.Add(bitmap);
            _retainedBytes += size;
        }
    }

    private static void Clear(Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
    }

    private static long ByteSize(Bitmap bitmap) => (long)bitmap.Width * bitmap.Height * 4;
}

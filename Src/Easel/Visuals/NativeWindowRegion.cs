using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Easel.Visuals;

/// <summary>Clips a native island's window to a rounded rectangle with optional rectangular holes.</summary>
/// <remarks>
/// A child HWND cannot be transformed or alpha-blended by the visual tree, but its window region can still exclude
/// the parts a popup covers and round its corners. Regions are binary: edges are aliased, and the holes are
/// rectangular. The system owns a region passed to <c>SetWindowRgn</c>; this helper only deletes regions that were
/// never handed over.
/// </remarks>
internal static class NativeWindowRegion
{
    private const int RgnAnd = 1;
    private const int RgnDiff = 4;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

    [DllImport("gdi32.dll")]
    private static extern bool PtInRegion(IntPtr hRgn, int x, int y);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePolygonRgn([In] Point[] points, int count, int fillMode);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>Applies a rounded region and removes every <paramref name="cuts"/> rectangle from it.</summary>
    /// <param name="child">The native window to clip.</param>
    /// <param name="cornerRadius">The corner radius in physical pixels; zero keeps square corners.</param>
    /// <param name="cuts">Rectangles in <paramref name="child"/> client coordinates to exclude.</param>
    /// <param name="visible">
    /// The visible part of the window in client coordinates, or <see langword="null"/> when the whole window shows.
    /// Ancestor clips are expressed this way instead of shrinking the window, so native content keeps its layout size.
    /// </param>
    internal static void Apply(Control child, double cornerRadius, Rectangle[] cuts, Rectangle? visible = null)
    {
        if (!child.IsHandleCreated)
        {
            return;
        }

        var width = child.Width;
        var height = child.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var window = visible ?? new Rectangle(0, 0, width, height);
        var fullVisible = visible is null
                          || (window.Left <= 0 && window.Top <= 0 && window.Right >= width && window.Bottom >= height);
        if (cuts.Length == 0 && cornerRadius <= 0d && fullVisible)
        {
            Restore(child);
            return;
        }

        var radius = (int)Math.Ceiling(cornerRadius * 2d);
        var region = cornerRadius > 0d
            ? CreateRoundRectRgn(0, 0, width + 1, height + 1, radius, radius)
            : CreateRectRgn(0, 0, width + 1, height + 1);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (!fullVisible)
        {
            var shown = CreateRectRgn(window.Left, window.Top, window.Right, window.Bottom);
            if (shown != IntPtr.Zero)
            {
                CombineRgn(region, region, shown, RgnAnd);
                DeleteObject(shown);
            }
        }

        foreach (var cut in cuts)
        {
            var hole = CreateRectRgn(cut.Left, cut.Top, cut.Right, cut.Bottom);
            if (hole == IntPtr.Zero)
            {
                continue;
            }

            CombineRgn(region, region, hole, RgnDiff);
            DeleteObject(hole);
        }

        if (SetWindowRgn(child.Handle, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    /// <summary>Clips the window to a polygon (the transformed element outline).</summary>
    internal static void SetPolygon(Control child, Point[] points)
    {
        const int winding = 2;
        if (!child.IsHandleCreated || points.Length < 3)
        {
            return;
        }

        var region = CreatePolygonRgn(points, points.Length, winding);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(child.Handle, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    /// <summary>Removes the window region, restoring the full rectangular window.</summary>
    internal static void Restore(Control child)
    {
        if (child.IsHandleCreated)
        {
            SetWindowRgn(child.Handle, IntPtr.Zero, true);
        }
    }

    /// <summary>Returns whether <paramref name="point"/> (client coordinates) is inside the window's current region.</summary>
    internal static bool Contains(Control child, Point point)
    {
        if (!child.IsHandleCreated)
        {
            return false;
        }

        var region = CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return GetWindowRgn(child.Handle, region) != 0 && PtInRegion(region, point.X, point.Y);
        }
        finally
        {
            DeleteObject(region);
        }
    }
}

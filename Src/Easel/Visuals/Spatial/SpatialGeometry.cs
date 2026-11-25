using System.Drawing;

namespace Easel.Visuals.Spatial;

// RectangleF.IsEmpty 只在四个值全为零时为真；空间索引把零宽/零高/NaN 都当作"尚未测量"。
internal static class SpatialGeometry
{
    internal static bool IsEmpty(RectangleF bounds)
        => float.IsNaN(bounds.X)
           || float.IsNaN(bounds.Y)
           || float.IsNaN(bounds.Width)
           || float.IsNaN(bounds.Height)
           || bounds.Width <= 0f
           || bounds.Height <= 0f;

    internal static bool Intersects(RectangleF a, RectangleF b)
        => a.X < b.X + b.Width
           && a.X + a.Width > b.X
           && a.Y < b.Y + b.Height
           && a.Y + a.Height > b.Y;

    internal static bool Contains(RectangleF bounds, float x, float y)
        => x >= bounds.X && x < bounds.X + bounds.Width
           && y >= bounds.Y && y < bounds.Y + bounds.Height;

    internal static RectangleF Union(RectangleF a, RectangleF b)
    {
        if (IsEmpty(a))
        {
            return b;
        }

        if (IsEmpty(b))
        {
            return a;
        }

        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    internal static bool Equals(RectangleF a, RectangleF b)
        => a.X.Equals(b.X) && a.Y.Equals(b.Y) && a.Width.Equals(b.Width) && a.Height.Equals(b.Height);
}

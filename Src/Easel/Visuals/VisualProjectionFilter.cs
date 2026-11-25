using System.Drawing;

namespace Easel.Visuals;

// 投影位图的局部重采样：按逆映射足迹在面积、双线性、Catmull-Rom 之间切换。
// 足迹取自 TryUnmapNormalized 的前向差分；源矩形边界仍走原来的 2x2 覆盖超采样。
// 缩小用各向异性的目标空间盒式采样（每轴样本数独立，透视前后景各自合适），
// 放大用 Catmull-Rom，接近 1:1 时双线性最省且足够。
internal static class VisualProjectionFilter
{
    // 足迹 >= 1.1 源像素/目标像素才算缩小，<= 0.9 才算放大；两者之间走双线性，
    // 免得为轻微缩放付出面积或三次采样的代价。
    private const double AreaThreshold = 1.1d;

    private const double CubicThreshold = 0.9d;

    // 面积采样每轴的上限。4x4 = 16 个双线性子样本，已能压住常见缩小；再高只加成本。
    private const int MaxSamplesPerAxis = 4;

    // 源矩形边界外扩 1.5 源像素做覆盖率超采样，与原 DrawProjected 的 edgeU/edgeV 一致。
    private const double EdgeWidth = 1.5d;

    // 采样一个目标像素。返回 false 表示该像素完全在投影四边形外，调用方保留透明。
    internal static bool Sample(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        Projection3D projection,
        double sourceLeft,
        double sourceTop,
        double sourceWidth,
        double sourceHeight,
        double centerX,
        double centerY,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        blue = 0d;
        green = 0d;
        red = 0d;
        alpha = 0d;

        if (!projection.TryUnmapNormalized(new PointF((float)centerX, (float)centerY), out var u, out var v))
        {
            return false;
        }

        var edgeU = EdgeWidth / Math.Max(1d, sourceWidth);
        var edgeV = EdgeWidth / Math.Max(1d, sourceHeight);
        var nearEdge = u >= -edgeU && u <= 1d + edgeU && v >= -edgeV && v <= 1d + edgeV
                       && (u < edgeU || u > 1d - edgeU || v < edgeV || v > 1d - edgeV);
        if (nearEdge)
        {
            return SampleCoverage(
                pixels, stride, width, height, bottomUp,
                projection, sourceLeft, sourceTop, sourceWidth, sourceHeight,
                centerX, centerY,
                out blue, out green, out red, out alpha);
        }

        if (u < 0d || u > 1d || v < 0d || v > 1d)
        {
            return false;
        }

        var sourceX = sourceLeft + (u * sourceWidth);
        var sourceY = sourceTop + (v * sourceHeight);
        if (!TryGetJacobian(
                projection, u, v, sourceWidth, sourceHeight, centerX, centerY,
                out var dXdx, out var dYdx, out var dXdy, out var dYdy))
        {
            // 逆映射在邻点失效（极端退化）：退回双线性，别把这一像素丢掉。
            SampleBilinear(
                pixels, stride, width, height, bottomUp, sourceX, sourceY,
                out blue, out green, out red, out alpha);
            return true;
        }

        var footprintX = Math.Sqrt((dXdx * dXdx) + (dYdx * dYdx));
        var footprintY = Math.Sqrt((dXdy * dXdy) + (dYdy * dYdy));
        if (footprintX >= AreaThreshold || footprintY >= AreaThreshold)
        {
            SampleArea(
                pixels, stride, width, height, bottomUp,
                sourceX, sourceY, dXdx, dYdx, dXdy, dYdy, footprintX, footprintY,
                out blue, out green, out red, out alpha);
            return true;
        }

        if (footprintX <= CubicThreshold && footprintY <= CubicThreshold)
        {
            SampleBicubic(
                pixels, stride, width, height, bottomUp, sourceX, sourceY,
                out blue, out green, out red, out alpha);
            return true;
        }

        SampleBilinear(
            pixels, stride, width, height, bottomUp, sourceX, sourceY,
            out blue, out green, out red, out alpha);
        return true;
    }

    // 目标像素中心的逆映射局部雅可比：目标空间沿 x/y 各走一格，落到源空间的列向量。
    // 两支列范数就是该方向每目标像素覆盖的源像素数，旋转、透视都成立。
    private static bool TryGetJacobian(
        Projection3D projection,
        double u,
        double v,
        double sourceWidth,
        double sourceHeight,
        double centerX,
        double centerY,
        out double dXdx,
        out double dYdx,
        out double dXdy,
        out double dYdy)
    {
        dXdx = 0d;
        dYdx = 0d;
        dXdy = 0d;
        dYdy = 0d;

        if (!projection.TryUnmapNormalized(new PointF((float)(centerX + 1d), (float)centerY), out var ux, out var vx)
            || !projection.TryUnmapNormalized(new PointF((float)centerX, (float)(centerY + 1d)), out var uy, out var vy))
        {
            return false;
        }

        dXdx = (ux - u) * sourceWidth;
        dYdx = (vx - v) * sourceHeight;
        dXdy = (uy - u) * sourceWidth;
        dYdy = (vy - v) * sourceHeight;
        return XamlMath.IsFinite(dXdx)
               && XamlMath.IsFinite(dYdx)
               && XamlMath.IsFinite(dXdy)
               && XamlMath.IsFinite(dYdy);
    }

    // 供测试与诊断：返回目标点处的源空间足迹（源像素/目标像素），不要求点在源矩形内。
    internal static bool TryGetFootprint(
        Projection3D projection,
        double sourceWidth,
        double sourceHeight,
        double centerX,
        double centerY,
        out double footprintX,
        out double footprintY)
    {
        footprintX = 0d;
        footprintY = 0d;
        if (!projection.TryUnmapNormalized(new PointF((float)centerX, (float)centerY), out var u, out var v)
            || !TryGetJacobian(
                projection, u, v, sourceWidth, sourceHeight, centerX, centerY,
                out var dXdx, out var dYdx, out var dXdy, out var dYdy))
        {
            return false;
        }

        footprintX = Math.Sqrt((dXdx * dXdx) + (dYdx * dYdx));
        footprintY = Math.Sqrt((dXdy * dXdy) + (dYdy * dYdy));
        return true;
    }

    // 面积采样：以中心雅可比把目标空间的小方格线性映到源空间（含交叉项），
    // 每轴样本数按各自足迹取整。缩小的那一轴多取，放大的那一轴只取 1 个。
    private static void SampleArea(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        double sourceX,
        double sourceY,
        double dXdx,
        double dYdx,
        double dXdy,
        double dYdy,
        double footprintX,
        double footprintY,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        // 减一个极小量再向上取整：整数倍缩小（如 0.5x → 足迹 2.0）不应因浮点误差多取一行样本。
        var samplesX = XamlMath.Clamp((int)Math.Ceiling(footprintX - 1e-3d), 1, MaxSamplesPerAxis);
        var samplesY = XamlMath.Clamp((int)Math.Ceiling(footprintY - 1e-3d), 1, MaxSamplesPerAxis);

        blue = 0d;
        green = 0d;
        red = 0d;
        alpha = 0d;
        for (var subY = 0; subY < samplesY; subY++)
        {
            var offsetY = ((subY + 0.5d) / samplesY) - 0.5d;
            for (var subX = 0; subX < samplesX; subX++)
            {
                var offsetX = ((subX + 0.5d) / samplesX) - 0.5d;
                SampleBilinear(
                    pixels, stride, width, height, bottomUp,
                    sourceX + (offsetX * dXdx) + (offsetY * dXdy),
                    sourceY + (offsetX * dYdx) + (offsetY * dYdy),
                    out var sb, out var sg, out var sr, out var sa);
                blue += sb;
                green += sg;
                red += sr;
                alpha += sa;
            }
        }

        var divisor = (double)(samplesX * samplesY);
        blue /= divisor;
        green /= divisor;
        red /= divisor;
        alpha /= divisor;
    }

    // 源矩形边界：2x2 子样本，未覆盖的子样本贡献 0，除以 4 就是覆盖率加权的平均色。
    private static bool SampleCoverage(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        Projection3D projection,
        double sourceLeft,
        double sourceTop,
        double sourceWidth,
        double sourceHeight,
        double centerX,
        double centerY,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        blue = 0d;
        green = 0d;
        red = 0d;
        alpha = 0d;
        var samples = 0;
        for (var subY = 0; subY < 2; subY++)
        {
            var sampleY = centerY - 0.25d + (subY * 0.5d);
            for (var subX = 0; subX < 2; subX++)
            {
                var sampleX = centerX - 0.25d + (subX * 0.5d);
                if (!projection.TryUnmapNormalized(new PointF((float)sampleX, (float)sampleY), out var u, out var v)
                    || u < 0d || u > 1d || v < 0d || v > 1d)
                {
                    continue;
                }

                samples++;
                SampleBilinear(
                    pixels, stride, width, height, bottomUp,
                    sourceLeft + (u * sourceWidth), sourceTop + (v * sourceHeight),
                    out var sb, out var sg, out var sr, out var sa);
                blue += sb;
                green += sg;
                red += sr;
                alpha += sa;
            }
        }

        if (samples == 0)
        {
            return false;
        }

        blue /= 4d;
        green /= 4d;
        red /= 4d;
        alpha /= 4d;
        return true;
    }

    private static void SampleBilinear(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        double x,
        double y,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        var sampleX = x - 0.5d;
        var sampleY = y - 0.5d;
        var x0 = (int)Math.Floor(sampleX);
        var y0 = (int)Math.Floor(sampleY);
        var fx = sampleX - x0;
        var fy = sampleY - y0;

        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0, y0, out var b00, out var g00, out var r00, out var a00);
        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0 + 1, y0, out var b10, out var g10, out var r10, out var a10);
        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0, y0 + 1, out var b01, out var g01, out var r01, out var a01);
        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0 + 1, y0 + 1, out var b11, out var g11, out var r11, out var a11);

        var w00 = (1d - fx) * (1d - fy);
        var w10 = fx * (1d - fy);
        var w01 = (1d - fx) * fy;
        var w11 = fx * fy;
        blue = (b00 * w00) + (b10 * w10) + (b01 * w01) + (b11 * w11);
        green = (g00 * w00) + (g10 * w10) + (g01 * w01) + (g11 * w11);
        red = (r00 * w00) + (r10 * w10) + (r01 * w01) + (r11 * w11);
        alpha = (a00 * w00) + (a10 * w10) + (a01 * w01) + (a11 * w11);
    }

    // Catmull-Rom（a = -0.5）。放大时插值而非面积平均，避免最近邻的块状感。
    private static void SampleBicubic(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        double x,
        double y,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        var sampleX = x - 0.5d;
        var sampleY = y - 0.5d;
        var x0 = (int)Math.Floor(sampleX);
        var y0 = (int)Math.Floor(sampleY);
        var fx = sampleX - x0;
        var fy = sampleY - y0;

        blue = 0d;
        green = 0d;
        red = 0d;
        alpha = 0d;
        for (var j = -1; j <= 2; j++)
        {
            var wy = Cubic(fy - j);
            for (var i = -1; i <= 2; i++)
            {
                var weight = Cubic(i - fx) * wy;
                ReadPremultiplied(
                    pixels, stride, width, height, bottomUp, x0 + i, y0 + j,
                    out var sb, out var sg, out var sr, out var sa);
                blue += sb * weight;
                green += sg * weight;
                red += sr * weight;
                alpha += sa * weight;
            }
        }
    }

    private static double Cubic(double x)
    {
        x = Math.Abs(x);
        return x <= 1d
            ? (1.5d * x * x * x) - (2.5d * x * x) + 1d
            : x < 2d
                ? (-0.5d * x * x * x) + (2.5d * x * x) - (4d * x) + 2d
                : 0d;
    }

    private static void ReadPremultiplied(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        int x,
        int y,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        x = x < 0 ? 0 : x >= width ? width - 1 : x;
        y = y < 0 ? 0 : y >= height ? height - 1 : y;
        var row = bottomUp ? height - 1 - y : y;
        var offset = (row * stride) + (x * 4);
        blue = pixels[offset];
        green = pixels[offset + 1];
        red = pixels[offset + 2];
        alpha = pixels[offset + 3];
    }
}

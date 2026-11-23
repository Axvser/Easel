using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Easel.Visuals;

/// <summary>Multiplies a composited subtree by a rendered opacity mask.</summary>
internal static class OpacityMaskCompositor
{
    /// <summary>Applies <paramref name="mask"/>'s alpha channel to <paramref name="source"/> in place.</summary>
    /// <param name="source">The premultiplied 32-bit bitmap to mask.</param>
    /// <param name="mask">A brush painted into a bitmap of the same size; only its alpha is used.</param>
    internal static void Apply(Bitmap source, Bitmap mask) => Apply(source, mask, OpacityMaskMode.Alpha);

    /// <summary>Applies <paramref name="mask"/> to <paramref name="source"/> in place.</summary>
    /// <param name="source">The premultiplied 32-bit bitmap to mask.</param>
    /// <param name="mask">A brush painted into a bitmap of the same size.</param>
    /// <param name="mode">Whether the mask's alpha or its luminance (times alpha) drives coverage.</param>
    internal static void Apply(Bitmap source, Bitmap mask, OpacityMaskMode mode)
    {
        if (source.Width != mask.Width || source.Height != mask.Height)
        {
            return;
        }

        var width = source.Width;
        var height = source.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bounds = new Rectangle(0, 0, width, height);
        var sourceData = source.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        var maskData = mask.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var sourceStride = Math.Abs(sourceData.Stride);
            var maskStride = Math.Abs(maskData.Stride);
            var sourceBytes = new byte[sourceStride * height];
            var maskBytes = new byte[maskStride * height];
            Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);
            Marshal.Copy(maskData.Scan0, maskBytes, 0, maskBytes.Length);

            var touched = false;
            for (var row = 0; row < height; row++)
            {
                // 负 stride 是自底向上的位图：逻辑第 0 行在缓冲区的末尾。
                var sourceRow = sourceData.Stride >= 0 ? row : height - 1 - row;
                var maskRow = maskData.Stride >= 0 ? row : height - 1 - row;
                var sourceOffset = sourceRow * sourceStride;
                var maskOffset = maskRow * maskStride;
                for (var column = 0; column < width; column++, sourceOffset += 4, maskOffset += 4)
                {
                    // 遮罩位图是预乘 alpha 的：白色 alpha=255 为 1，黑色为 0，白色 alpha=128 为 0.5。
                    double factor;
                    if (mode == OpacityMaskMode.Luminance)
                    {
                        factor = ((maskBytes[maskOffset + 2] * 0.2126d)
                                  + (maskBytes[maskOffset + 1] * 0.7152d)
                                  + (maskBytes[maskOffset] * 0.0722d)) / 255d;
                    }
                    else
                    {
                        factor = maskBytes[maskOffset + 3] / 255d;
                    }

                    if (factor >= 0.999d)
                    {
                        continue;
                    }

                    touched = true;
                    if (factor <= 0d)
                    {
                        sourceBytes[sourceOffset] = 0;
                        sourceBytes[sourceOffset + 1] = 0;
                        sourceBytes[sourceOffset + 2] = 0;
                        sourceBytes[sourceOffset + 3] = 0;
                        continue;
                    }

                    // 源位图是预乘 alpha 的：四个通道同乘覆盖系数才保持预乘关系。
                    sourceBytes[sourceOffset] = (byte)(sourceBytes[sourceOffset] * factor);
                    sourceBytes[sourceOffset + 1] = (byte)(sourceBytes[sourceOffset + 1] * factor);
                    sourceBytes[sourceOffset + 2] = (byte)(sourceBytes[sourceOffset + 2] * factor);
                    sourceBytes[sourceOffset + 3] = (byte)(sourceBytes[sourceOffset + 3] * factor);
                }
            }

            if (touched)
            {
                Marshal.Copy(sourceBytes, 0, sourceData.Scan0, sourceBytes.Length);
            }
        }
        finally
        {
            source.UnlockBits(sourceData);
            mask.UnlockBits(maskData);
        }
    }
}

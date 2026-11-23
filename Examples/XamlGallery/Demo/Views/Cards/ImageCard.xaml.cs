using System.Drawing;
using System.Drawing.Drawing2D;

namespace Demo.Views;

/// <summary>The Image card; the bitmap is drawn here and assigned after initialization.</summary>
public sealed partial class ImageCard : ThemedStackPanel
{
    /// <summary>Creates the view, runs the generated initializer and assigns the bitmap.</summary>
    public ImageCard()
    {
        InitializeComponent();
        image.Source = CreateBitmap();
    }

    private static Bitmap CreateBitmap()
    {
        var bitmap = new Bitmap(96, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new LinearGradientBrush(
                   new Rectangle(0, 0, 96, 64),
                   Color.FromArgb(0xFF, 0x5B, 0x8C, 0xFF),
                   Color.FromArgb(0xFF, 0xA8, 0x4B, 0xF0),
                   45f))
        {
            graphics.FillRectangle(background, 0, 0, 96, 64);
        }

        using var pen = new Pen(Color.White, 3f);
        graphics.DrawEllipse(pen, 28f, 12f, 40f, 40f);
        return bitmap;
    }
}

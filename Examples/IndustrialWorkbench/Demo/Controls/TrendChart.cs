using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A real-time trend / oscilloscope with a grid, two series, an area fill and a live head marker.</summary>
public sealed class TrendChart : VisualElement
{
    /// <summary>The primary series; the newest sample is the last element.</summary>
    public static readonly DependencyProperty SeriesAProperty = DependencyProperty.Register(
        nameof(SeriesA), typeof(double[]), typeof(TrendChart),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The secondary series.</summary>
    public static readonly DependencyProperty SeriesBProperty = DependencyProperty.Register(
        nameof(SeriesB), typeof(double[]), typeof(TrendChart),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The bottom of the value range.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(TrendChart),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The top of the value range.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(TrendChart),
        new PropertyMetadata(100d) { AffectsRender = true });

    /// <summary>The primary line colour.</summary>
    public static readonly DependencyProperty SeriesABrushProperty = DependencyProperty.Register(
        nameof(SeriesABrush), typeof(XamlBrush), typeof(TrendChart),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The secondary line colour.</summary>
    public static readonly DependencyProperty SeriesBBrushProperty = DependencyProperty.Register(
        nameof(SeriesBBrush), typeof(XamlBrush), typeof(TrendChart),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The grid colour.</summary>
    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(XamlBrush), typeof(TrendChart),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The area fill colour under the primary series.</summary>
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(XamlBrush), typeof(TrendChart),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The chart title.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(TrendChart),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The unit shown with the head value.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(TrendChart),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>Creates a non-interactive chart.</summary>
    public TrendChart()
    {
        IsHitTestVisible = false;
        MinWidth = 160f;
        MinHeight = 90f;
        SeriesABrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        SeriesBBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        GridBrush = new SolidColorBrush(Color.FromArgb(0x24, 0x25, 0xD0, 0xEC));
        FillBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x25, 0xD0, 0xEC));
    }

    /// <summary>The primary series.</summary>
    public double[]? SeriesA
    {
        get => (double[]?)GetValue(SeriesAProperty);
        set => SetValue(SeriesAProperty, value);
    }

    /// <summary>The secondary series.</summary>
    public double[]? SeriesB
    {
        get => (double[]?)GetValue(SeriesBProperty);
        set => SetValue(SeriesBProperty, value);
    }

    /// <summary>The bottom of the value range.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty)!;
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The top of the value range.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty)!;
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The primary line colour.</summary>
    public XamlBrush? SeriesABrush
    {
        get => (XamlBrush?)GetValue(SeriesABrushProperty);
        set => SetValue(SeriesABrushProperty, value);
    }

    /// <summary>The secondary line colour.</summary>
    public XamlBrush? SeriesBBrush
    {
        get => (XamlBrush?)GetValue(SeriesBBrushProperty);
        set => SetValue(SeriesBBrushProperty, value);
    }

    /// <summary>The grid colour.</summary>
    public XamlBrush? GridBrush
    {
        get => (XamlBrush?)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    /// <summary>The area fill colour.</summary>
    public XamlBrush? FillBrush
    {
        get => (XamlBrush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    /// <summary>The chart title.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The unit shown with the head value.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty)!;
        set => SetValue(UnitProperty, value ?? string.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 320;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 150;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 24f || height <= 24f)
        {
            return;
        }

        var seriesA = SeriesA;
        var seriesB = SeriesB;
        var hasA = seriesA is { Length: >= 2 };
        var hasB = seriesB is { Length: >= 2 };

        var lineA = HmiPaint.ColorOf(SeriesABrush, Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        var lineB = HmiPaint.ColorOf(SeriesBBrush, Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        var grid = HmiPaint.ColorOf(GridBrush, Color.FromArgb(0x30, 0x25, 0xD0, 0xEC));
        var fill = HmiPaint.ColorOf(FillBrush, HmiPaint.Fade(lineA, 0.18));
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);
        var text = Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0);

        var hasTitle = !string.IsNullOrEmpty(Title);
        var top = hasTitle ? Math.Min(18f, height * 0.14f) : 4f;
        var left = 30f;
        var plot = new RectangleF(left, top + 2f, Math.Max(4f, width - left - 6f), Math.Max(4f, height - top - 8f));

        // Grid and Y labels.
        for (var index = 0; index <= 4; index++)
        {
            var y = plot.Bottom - (plot.Height * index / 4f);
            using (var pen = new Pen(grid, index is 0 or 4 ? 1.3f : 1f))
            {
                graphics.DrawLine(pen, plot.Left, y, plot.Right, y);
            }

            var label = (Minimum + ((Maximum - Minimum) * index / 4d)).ToString("0");
            HmiPaint.Text(
                graphics, label, HmiPaint.Mono(Math.Max(6f, height * 0.05f)), muted,
                new RectangleF(0f, y - 7f, left - 4f, 14f), StringAlignment.Far, StringAlignment.Center);
        }

        for (var index = 0; index <= 8; index++)
        {
            var x = plot.Left + (plot.Width * index / 8f);
            using var pen = new Pen(HmiPaint.Fade(grid, 0.6), 1f);
            graphics.DrawLine(pen, x, plot.Top, x, plot.Bottom);
        }

        var range = Maximum - Minimum;
        if (range <= 0d)
        {
            range = 1d;
        }

        float PointY(double value)
            => plot.Bottom - (plot.Height * (float)Math.Clamp((value - Minimum) / range, 0d, 1d));

        PointF[] BuildPoints(double[] values)
        {
            var result = new PointF[values.Length];
            var step = values.Length > 1 ? plot.Width / (values.Length - 1) : 0f;
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = new PointF(plot.Left + (step * index), PointY(values[index]));
            }

            return result;
        }

        if (hasA)
        {
            var points = BuildPoints(seriesA!);
            // Area under A.
            using var area = new GraphicsPath();
            area.AddLine(plot.Left, plot.Bottom, points[0].X, points[0].Y);
            area.AddLines(points);
            area.AddLine(points[^1].X, plot.Bottom, plot.Left, plot.Bottom);
            area.CloseFigure();
            using (var areaBrush = new SolidBrush(fill))
            {
                graphics.FillPath(areaBrush, area);
            }
        }

        if (hasB)
        {
            var points = BuildPoints(seriesB!);
            using var pen = new Pen(HmiPaint.Fade(lineB, 0.9), Math.Max(1.2f, height * 0.012f));
            graphics.DrawLines(pen, points);
        }

        if (hasA)
        {
            var points = BuildPoints(seriesA!);
            using (var pen = new Pen(lineA, Math.Max(1.5f, height * 0.016f)) { LineJoin = LineJoin.Round })
            {
                graphics.DrawLines(pen, points);
            }

            var head = points[^1];
            using (var glow = new SolidBrush(HmiPaint.Fade(lineA, 0.25)))
            {
                graphics.FillEllipse(glow, head.X - 6f, head.Y - 6f, 12f, 12f);
            }
            using (var dot = new SolidBrush(lineA))
            {
                graphics.FillEllipse(dot, head.X - 2.6f, head.Y - 2.6f, 5.2f, 5.2f);
            }

            var headText = seriesA![^1].ToString("0.0") + (string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit);
            var labelWidth = Math.Min(plot.Width * 0.4f, 70f);
            var labelRect = new RectangleF(plot.Right - labelWidth, Math.Max(plot.Top, head.Y - 18f), labelWidth, 14f);
            HmiPaint.FillRounded(graphics, Color.FromArgb(0xCC, 0x07, 0x0C, 0x12), labelRect, 3f);
            HmiPaint.Text(graphics, headText, HmiPaint.Mono(Math.Max(6f, height * 0.055f)), text,
                labelRect, StringAlignment.Center, StringAlignment.Center);
        }

        // Border.
        using (var border = new Pen(grid, 1.2f))
        {
            graphics.DrawRectangle(border, plot.X, plot.Y, plot.Width, plot.Height);
        }

        if (hasTitle)
        {
            HmiPaint.Text(
                graphics, Title, HmiPaint.Ui(Math.Max(7f, height * 0.06f)), muted,
                new RectangleF(left, 0f, width - left, top), StringAlignment.Near, StringAlignment.Center);
        }
    }

}

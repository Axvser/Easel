using System.Drawing;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A seven-segment numeric display with sign, fixed decimals, an optional unit and glowing segments.</summary>
public sealed class SevenSegmentDisplay : VisualElement
{
    private static readonly Dictionary<char, string> SegmentMap = new()
    {
        ['0'] = "abcdef", ['1'] = "bc", ['2'] = "abdeg", ['3'] = "abcdg", ['4'] = "bcfg",
        ['5'] = "acdfg", ['6'] = "acdefg", ['7'] = "abc", ['8'] = "abcdefg", ['9'] = "abcdfg",
        ['-'] = "g",
    };

    /// <summary>The numeric value to show.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SevenSegmentDisplay),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The count of fraction digits.</summary>
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(SevenSegmentDisplay),
        new PropertyMetadata(1) { AffectsRender = true });

    /// <summary>The count of digit cells for the integer part plus fraction.</summary>
    public static readonly DependencyProperty DigitsProperty = DependencyProperty.Register(
        nameof(Digits), typeof(int), typeof(SevenSegmentDisplay),
        new PropertyMetadata(4) { AffectsRender = true });

    /// <summary>The unit suffix.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(SevenSegmentDisplay),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The lit segment colour.</summary>
    public static readonly DependencyProperty SegmentBrushProperty = DependencyProperty.Register(
        nameof(SegmentBrush), typeof(XamlBrush), typeof(SevenSegmentDisplay),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The unlit segment colour.</summary>
    public static readonly DependencyProperty DimBrushProperty = DependencyProperty.Register(
        nameof(DimBrush), typeof(XamlBrush), typeof(SevenSegmentDisplay),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Whether the unit suffix is drawn.</summary>
    public static readonly DependencyProperty ShowUnitProperty = DependencyProperty.Register(
        nameof(ShowUnit), typeof(bool), typeof(SevenSegmentDisplay),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>Creates a non-interactive display.</summary>
    public SevenSegmentDisplay()
    {
        IsHitTestVisible = false;
        MinWidth = 100f;
        MinHeight = 36f;
        SegmentBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        DimBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xB0, 0x20));
    }

    /// <summary>The numeric value to show.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The count of fraction digits.</summary>
    public int Decimals
    {
        get => (int)GetValue(DecimalsProperty)!;
        set => SetValue(DecimalsProperty, Math.Clamp(value, 0, 6));
    }

    /// <summary>The count of digit cells.</summary>
    public int Digits
    {
        get => (int)GetValue(DigitsProperty)!;
        set => SetValue(DigitsProperty, Math.Clamp(value, 1, 12));
    }

    /// <summary>The unit suffix.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty)!;
        set => SetValue(UnitProperty, value ?? string.Empty);
    }

    /// <summary>The lit segment colour.</summary>
    public XamlBrush? SegmentBrush
    {
        get => (XamlBrush?)GetValue(SegmentBrushProperty);
        set => SetValue(SegmentBrushProperty, value);
    }

    /// <summary>The unlit segment colour.</summary>
    public XamlBrush? DimBrush
    {
        get => (XamlBrush?)GetValue(DimBrushProperty);
        set => SetValue(DimBrushProperty, value);
    }

    /// <summary>Whether the unit suffix is drawn.</summary>
    public bool ShowUnit
    {
        get => (bool)GetValue(ShowUnitProperty)!;
        set => SetValue(ShowUnitProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 160;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 44;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 6f || height <= 6f)
        {
            return;
        }

        var lit = HmiPaint.ColorOf(SegmentBrush, Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        var dim = HmiPaint.ColorOf(DimBrush, HmiPaint.Fade(lit, 0.14));
        var showUnit = ShowUnit && !string.IsNullOrEmpty(Unit);
        var unitWidth = showUnit ? Math.Min(width * 0.22f, height * 0.9f) : 0f;
        var area = new RectangleF(2f, 2f, Math.Max(0f, width - 6f - unitWidth), Math.Max(0f, height - 4f));
        var text = BuildText();

        // Cell metrics: a digit is about 0.62 of the height wide.
        var digitHeight = area.Height * 0.82f;
        var top = area.Top + ((area.Height - digitHeight) / 2f);
        var cellWidth = Math.Max(4f, digitHeight * 0.62f);
        var dotWidth = Math.Max(3f, digitHeight * 0.20f);
        var gap = Math.Max(1f, digitHeight * 0.08f);

        var totalWidth = 0f;
        foreach (var ch in text)
        {
            totalWidth += (ch == '.' ? dotWidth : cellWidth) + gap;
        }

        totalWidth -= gap;
        if (totalWidth > area.Width && totalWidth > 0f)
        {
            // Shrink the cells so a long reading never spills outside the control.
            var fit = area.Width / totalWidth;
            cellWidth *= fit;
            dotWidth *= fit;
            gap *= fit;
            totalWidth = area.Width;
        }

        var x = area.Left + Math.Max(0f, (area.Width - totalWidth) / 2f);
        var thickness = Math.Max(2f, digitHeight * 0.115f);

        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                x += cellWidth + gap;
                continue;
            }

            if (ch == '.')
            {
                DrawDot(graphics, x, top + digitHeight - (thickness * 0.75f), dotWidth, lit);
                x += dotWidth + gap;
                continue;
            }

            DrawDigit(graphics, ch, new RectangleF(x, top, cellWidth, digitHeight), thickness, lit, dim);
            x += cellWidth + gap;
        }

        if (showUnit)
        {
            HmiPaint.Text(
                graphics, Unit, HmiPaint.Ui(Math.Max(7f, height * 0.20f)),
                Color.FromArgb(0xFF, 0x8F, 0xA6, 0xBA),
                new RectangleF(width - unitWidth - 2f, 0f, unitWidth, height),
                StringAlignment.Center, StringAlignment.Center);
        }
    }

    private string BuildText()
    {
        var decimals = Math.Clamp(Decimals, 0, 6);
        var total = Math.Clamp(Digits, 1, 12);
        var integerCells = Math.Max(1, total - decimals);
        var magnitude = Math.Abs(Value);
        var limit = Math.Pow(10d, integerCells) - (decimals > 0 ? Math.Pow(10d, -decimals) : 0d);
        if (magnitude > limit)
        {
            magnitude = limit;
        }

        var negative = Value < 0d && magnitude >= Math.Pow(10d, -decimals) / 2d;
        var intPart = Math.Truncate(magnitude).ToString("0");
        if (intPart.Length > integerCells)
        {
            intPart = intPart[..integerCells];
        }

        intPart = intPart.PadLeft(integerCells, ' ');
        var fraction = decimals > 0
            ? magnitude.ToString("F" + decimals).Split('.')[1]
            : string.Empty;
        var sign = negative ? "-" : string.Empty;
        return sign + intPart + (decimals > 0 ? "." + fraction : string.Empty);
    }

    private static void DrawDigit(Graphics graphics, char ch, RectangleF box, float thickness, Color lit, Color dim)
    {
        var segments = SegmentMap.TryGetValue(ch, out var map) ? map : string.Empty;
        foreach (var segment in "abcdefg")
        {
            var rect = SegmentRect(box, segment, thickness);
            var isLit = segments.Contains(segment, StringComparison.Ordinal);
            if (isLit)
            {
                HmiPaint.FillRounded(graphics, HmiPaint.Fade(lit, 0.18), Inflate(rect, thickness * 0.5f), thickness * 0.5f);
                HmiPaint.FillRounded(graphics, lit, rect, thickness * 0.35f);
            }
            else
            {
                HmiPaint.FillRounded(graphics, dim, rect, thickness * 0.35f);
            }
        }
    }

    private static RectangleF SegmentRect(RectangleF box, char segment, float t)
    {
        var x = box.X;
        var y = box.Y;
        var w = box.Width;
        var h = box.Height;
        var inset = t * 0.55f;
        var bar = w - (t * 1.1f);
        return segment switch
        {
            'a' => new RectangleF(x + inset, y, bar, t),
            'd' => new RectangleF(x + inset, y + h - t, bar, t),
            'g' => new RectangleF(x + inset, y + ((h - t) / 2f), bar, t),
            'f' => new RectangleF(x, y + inset, t, (h / 2f) - (t * 0.85f)),
            'b' => new RectangleF(x + w - t, y + inset, t, (h / 2f) - (t * 0.85f)),
            'e' => new RectangleF(x, y + (h / 2f) + (t * 0.3f), t, (h / 2f) - (t * 0.85f)),
            'c' => new RectangleF(x + w - t, y + (h / 2f) + (t * 0.3f), t, (h / 2f) - (t * 0.85f)),
            _ => RectangleF.Empty,
        };
    }

    private static void DrawDot(Graphics graphics, float x, float y, float size, Color color)
    {
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, x, y, size, size);
    }

    private static RectangleF Inflate(RectangleF rect, float amount)
        => new(rect.X - amount, rect.Y - amount, rect.Width + (amount * 2f), rect.Height + (amount * 2f));
}

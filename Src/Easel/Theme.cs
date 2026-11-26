using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls;
using Microsoft.Win32;

namespace Easel;

/// <summary>Which palette the default control styles use.</summary>
public enum ThemeMode
{
    /// <summary>The Visual Studio Code Light+ palette.</summary>
    Light,

    /// <summary>The Visual Studio Code Dark+ palette.</summary>
    Dark,

    /// <summary>A high-contrast black palette with an amber accent.</summary>
    HighContrast,

    /// <summary>Follow the Windows app theme (Dark+/Light+), or high contrast while Windows high contrast is on.</summary>
    System,
}

/// <summary>The colours and metrics a default control style reads.</summary>
/// <remarks>
/// The built-in palettes follow Visual Studio Code's Dark+ and Light+ themes, plus a high-contrast black palette:
/// near-grey surfaces, a single accent, and four-pixel corners. A host may replace any entry by setting an explicit
/// value or an implicit style.
/// </remarks>
public sealed class ThemePalette
{
    /// <summary>The application background behind panels.</summary>
    public Color WindowBackground { get; set; }

    /// <summary>A raised surface such as a panel or card.</summary>
    public Color Surface { get; set; }

    /// <summary>A second surface level for headers, tracks and inset areas.</summary>
    public Color SurfaceAlt { get; set; }

    /// <summary>The default background of an interactive control.</summary>
    public Color ControlBackground { get; set; }

    /// <summary>The default border of an interactive control.</summary>
    public Color ControlBorder { get; set; }

    /// <summary>The hover background of an interactive control.</summary>
    public Color ControlHover { get; set; }

    /// <summary>The pressed background of an interactive control.</summary>
    public Color ControlPressed { get; set; }

    /// <summary>The primary text and glyph colour.</summary>
    public Color Foreground { get; set; }

    /// <summary>A secondary text colour for labels and placeholders.</summary>
    public Color MutedForeground { get; set; }

    /// <summary>The accent used by checked, selected and primary states.</summary>
    public Color Accent { get; set; }

    /// <summary>The text or glyph drawn on <see cref="Accent"/>.</summary>
    public Color AccentForeground { get; set; }

    /// <summary>The focus ring colour.</summary>
    public Color FocusRing { get; set; }

    /// <summary>The default caret colour of text fields; falls back to <see cref="FocusRing"/>.</summary>
    public Color Caret { get; set; }

    /// <summary>The text colour of a disabled control.</summary>
    public Color DisabledForeground { get; set; }

    /// <summary>The background of a disabled control.</summary>
    public Color DisabledBackground { get; set; }

    /// <summary>The row or item selection background.</summary>
    public Color Selection { get; set; }

    /// <summary>The scroll-bar thumb colour at rest.</summary>
    public Color ScrollBarThumb { get; set; } = Color.FromArgb(0x66, 0xA0, 0xA0, 0xA0);

    /// <summary>The scroll-bar thumb colour under the pointer or while dragged.</summary>
    public Color ScrollBarThumbHover { get; set; } = Color.FromArgb(0xCC, 0xE7, 0xE7, 0xE7);

    /// <summary>The default stroke of a <see cref="Easel.Visuals.ShapeVisual"/> when none is assigned.</summary>
    public Color ShapeStroke { get; set; } = Color.FromArgb(0x88, 0x5B, 0x8C, 0xFF);

    /// <summary>The default stroke width of a <see cref="Easel.Visuals.CurveBatchVisual"/>.</summary>
    public float ShapeStrokeThickness { get; set; } = 1.5f;

    /// <summary>The default colour of a <see cref="DropShadowEffect"/>.</summary>
    public Color ShadowColor { get; set; } = Color.Black;

    /// <summary>The default offset of a <see cref="DropShadowEffect"/>.</summary>
    public PointF ShadowOffset { get; set; } = new(4f, 6f);

    /// <summary>The default blur radius of a <see cref="DropShadowEffect"/>.</summary>
    public float ShadowBlur { get; set; } = 6f;

    /// <summary>The default opacity of a <see cref="DropShadowEffect"/>.</summary>
    public double ShadowOpacity { get; set; } = 0.45d;

    /// <summary>The default foreground of a lightweight <see cref="Easel.Visuals.TextVisual"/>.</summary>
    public Color TextVisualForeground { get; set; } = Color.White;

    /// <summary>The default font family of a lightweight <see cref="Easel.Visuals.TextVisual"/>.</summary>
    public string TextVisualFontFamily
    {
        get => _textVisualFontFamily;
        set
        {
            _textVisualFontFamily = value;
            _textVisualFont = null;
        }
    }

    /// <summary>The default font size of a lightweight <see cref="Easel.Visuals.TextVisual"/>.</summary>
    public float TextVisualFontSize
    {
        get => _textVisualFontSize;
        set
        {
            _textVisualFontSize = value;
            _textVisualFont = null;
        }
    }

    /// <summary>The default font weight of a lightweight <see cref="Easel.Visuals.TextVisual"/>.</summary>
    public Easel.Visuals.FontWeight TextVisualFontWeight
    {
        get => _textVisualFontWeight;
        set
        {
            _textVisualFontWeight = value;
            _textVisualFont = null;
        }
    }

    private string _textVisualFontFamily = "Segoe UI";
    private float _textVisualFontSize = 9f;
    private Easel.Visuals.FontWeight _textVisualFontWeight = Easel.Visuals.FontWeight.Normal;
    private Font? _textVisualFont;

    /// <summary>The font a <see cref="Easel.Visuals.TextVisual"/> uses when its own font is not set.</summary>
    public Font TextVisualFont => _textVisualFont ??= new Font(
        string.IsNullOrWhiteSpace(TextVisualFontFamily) ? "Segoe UI" : TextVisualFontFamily,
        TextVisualFontSize > 0f ? TextVisualFontSize : 9f,
        TextVisualFontWeight >= Easel.Visuals.FontWeight.SemiBold ? FontStyle.Bold : FontStyle.Regular);

    /// <summary>The default text family for controls that do not set a font.</summary>
    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            _defaultFont = null;
        }
    }

    /// <summary>The default text size in points for controls that do not set a font.</summary>
    public float FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            _defaultFont = null;
        }
    }

    /// <summary>The default text weight for controls that do not set a font.</summary>
    public Easel.Visuals.FontWeight FontWeight
    {
        get => _fontWeight;
        set
        {
            _fontWeight = value;
            _defaultFont = null;
        }
    }

    private string _fontFamily = "Segoe UI";
    private float _fontSize = 9.5f;
    private Easel.Visuals.FontWeight _fontWeight = Easel.Visuals.FontWeight.Normal;
    private Font? _defaultFont;

    /// <summary>The font controls use when their own <c>Font</c> is not set.</summary>
    public Font DefaultFont => _defaultFont ??= new Font(
        string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily,
        FontSize > 0f ? FontSize : 9.5f,
        FontWeight >= Easel.Visuals.FontWeight.SemiBold ? FontStyle.Bold : FontStyle.Regular);

    /// <summary>The default corner radius in pixels.</summary>
    public float CornerRadius { get; set; } = 4f;

    /// <summary>The default interactive control height in pixels.</summary>
    public int ControlHeight { get; set; } = 28;

    /// <summary>The default border thickness of a self-drawn control.</summary>
    public Easel.Controls.Thickness ControlBorderThickness { get; set; } = new(1f);

    /// <summary>The focus-ring pen width.</summary>
    public float FocusThickness { get; set; } = 1.6f;

    /// <summary>A slightly lighter focus-ring pen width used by range controls.</summary>
    public float FocusThicknessMedium { get; set; } = 1.4f;

    /// <summary>The thin focus-ring pen width used inside dense lists.</summary>
    public float FocusThicknessThin { get; set; } = 1f;

    /// <summary>The corner radius of the default scroll-bar thumb.</summary>
    public float ScrollBarThumbRadius { get; set; } = 3f;

    /// <summary>Builds a palette from a theme resource dictionary using the standard theme keys.</summary>
    /// <param name="resources">A dictionary with the keys used by the built-in theme dictionaries.</param>
    /// <returns>A new palette; missing keys become transparent.</returns>
    public static ThemePalette FromResourceDictionary(ResourceDictionary resources)
    {
        if (resources is null) throw new ArgumentNullException(nameof(resources));

        Color Read(string key)
            => resources[key] is Brush brush ? brush.ApproximateColor : Color.Empty;

        Color Optional(string key, Color fallback)
            => resources[key] is Brush brush ? brush.ApproximateColor : fallback;

        var foreground = Read("Text");
        var muted = Read("Muted");

        return new ThemePalette
        {
            WindowBackground = Read("Window"),
            Surface = Read("Surface"),
            SurfaceAlt = Read("Raised"),
            ControlBackground = Read("Input"),
            ControlBorder = Read("Border"),
            ControlHover = Read("Hover"),
            ControlPressed = Read("Pressed"),
            Foreground = foreground,
            MutedForeground = muted,
            Accent = Read("Accent"),
            AccentForeground = Read("AccentText"),
            FocusRing = Read("Focus"),
            Caret = Optional("Caret", Read("Focus")),
            DisabledForeground = Read("DisabledText"),
            DisabledBackground = Read("Disabled"),
            Selection = Read("Selection"),
            ScrollBarThumb = Optional(
                "ScrollBarThumb",
                Color.FromArgb(0x66, muted.R, muted.G, muted.B)),
            ScrollBarThumbHover = Optional(
                "ScrollBarThumbHover",
                Color.FromArgb(0xCC, foreground.R, foreground.G, foreground.B)),
            ShapeStroke = Optional("ShapeStroke", Color.FromArgb(0x88, 0x5B, 0x8C, 0xFF)),
            ShapeStrokeThickness = resources["ShapeStrokeThickness"] is double strokeThickness
                ? (float)strokeThickness
                : 1.5f,
            ShadowColor = Optional("ShadowColor", Color.Black),
            ShadowOffset = resources["ShadowOffset"] is PointF offset ? offset : new PointF(4f, 6f),
            ShadowBlur = resources["ShadowBlur"] is double shadowBlur ? (float)shadowBlur : 6f,
            ShadowOpacity = resources["ShadowOpacity"] is double shadowOpacity ? shadowOpacity : 0.45d,
            TextVisualForeground = Optional("TextVisualForeground", Color.White),
            TextVisualFontFamily = resources["TextVisualFontFamily"] as string ?? "Segoe UI",
            TextVisualFontSize = resources["TextVisualFontSize"] is double textVisualSize ? (float)textVisualSize : 9f,
            TextVisualFontWeight = resources["TextVisualFontWeight"] is Easel.Visuals.FontWeight textVisualWeight
                ? textVisualWeight
                : Easel.Visuals.FontWeight.Normal,
            FontFamily = resources["FontFamily"] as string ?? "Segoe UI",
            FontSize = resources["FontSize"] is double size ? (float)size : 9.5f,
            FontWeight = resources["FontWeight"] is Easel.Visuals.FontWeight weight
                ? weight
                : Easel.Visuals.FontWeight.Normal,
            CornerRadius = resources["CornerRadius"] is double radius ? (float)radius : 4f,
            ControlHeight = resources["ControlHeight"] is int height ? height : 28,
            ControlBorderThickness = resources["ControlBorderThickness"] is Easel.Controls.Thickness thickness
                ? thickness
                : new Easel.Controls.Thickness(1f),
            FocusThickness = resources["FocusThickness"] is double focus ? (float)focus : 1.6f,
            FocusThicknessMedium = resources["FocusThicknessMedium"] is double medium ? (float)medium : 1.4f,
            FocusThicknessThin = resources["FocusThicknessThin"] is double thin ? (float)thin : 1f,
            ScrollBarThumbRadius = resources["ScrollBarThumbRadius"] is double thumbRadius ? (float)thumbRadius : 3f,
        };
    }
}

/// <summary>The active default palette for controls that do not set an explicit colour.</summary>
public static class Theme
{
    // The built-in palettes are XAML resources in Themes/; the static sources are created once and shared.
    private static readonly Themes.DarkTheme DarkSource = new();
    private static readonly Themes.LightTheme LightSource = new();
    private static readonly ResourceDictionary HighContrastSource = CreatePaletteResourceDictionary(CreateHighContrast());

    private static ThemeMode _mode = ThemeMode.Dark;
    private static ThemeMode _systemMode = ThemeMode.Dark;
    private static ThemePalette _current = CreateDark();
    private static bool _systemWatcherAttached;
    private static SynchronizationContext? _systemContext;

    /// <summary>Raised after <see cref="Mode"/>, <see cref="Current"/> or the followed system theme changes.</summary>
    public static event EventHandler? Changed;

    /// <summary>The active mode; setting it swaps in the matching built-in palette.</summary>
    /// <remarks>
    /// <see cref="ThemeMode.System"/> resolves to Dark+ / Light+ from the Windows app theme, or to the
    /// high-contrast palette while Windows high contrast is enabled. The first system mode assignment attaches a
    /// listener; later Windows theme changes raise <see cref="Changed"/> on the assigning thread's context.
    /// </remarks>
    public static ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                if (value == ThemeMode.System)
                {
                    RefreshSystemTheme();
                }

                return;
            }

            _mode = value;
            if (value == ThemeMode.System)
            {
                EnsureSystemWatcher();
                _systemMode = SystemThemeResolver();
            }

            _current = CreatePalette(value == ThemeMode.System ? _systemMode : value);
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>The palette actually in use; <see cref="ThemeMode.System"/> resolves to a concrete mode.</summary>
    public static ThemeMode EffectiveMode => _mode == ThemeMode.System ? _systemMode : _mode;

    /// <summary>The active palette. Assigning a custom palette raises <see cref="Changed"/>.</summary>
    public static ThemePalette Current
    {
        get => _current;
        set
        {
            _current = value ?? throw new ArgumentNullException(nameof(value));
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Applies a host-defined palette for a mode without changing the built-in palette sources.</summary>
    /// <param name="mode">The mode the host wants the selector and effective-mode checks to report.</param>
    /// <param name="palette">The palette to activate.</param>
    /// <remarks>Use this for custom themes: build a <see cref="ThemePalette"/> (for example with
    /// <see cref="ThemePalette.FromResourceDictionary"/>) and pass it with the closest built-in mode. The
    /// system watcher is unaffected unless <paramref name="mode"/> is <see cref="ThemeMode.System"/>.</remarks>
    public static void ApplyPalette(ThemeMode mode, ThemePalette palette)
    {
        _mode = mode;
        _current = palette ?? throw new ArgumentNullException(nameof(palette));
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Re-reads the Windows theme while following the system.</summary>
    /// <remarks>Does nothing unless <see cref="Mode"/> is <see cref="ThemeMode.System"/>. A change swaps the
    /// palette and raises <see cref="Changed"/>; no event is raised when the effective mode is unchanged.</remarks>
    public static void RefreshSystemTheme()
    {
        if (_mode != ThemeMode.System)
        {
            return;
        }

        var next = SystemThemeResolver();
        if (next == _systemMode)
        {
            return;
        }

        _systemMode = next;
        _current = CreatePalette(next);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The system-theme resolver; tests replace it to simulate Windows settings.</summary>
    internal static Func<ThemeMode> SystemThemeResolver { get; set; } = ResolveSystemTheme;

    private static ThemePalette CreatePalette(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => CreateLight(),
        ThemeMode.HighContrast => CreateHighContrast(),
        _ => CreateDark(),
    };

    private static ThemeMode ResolveSystemTheme()
    {
        bool highContrast;
        try
        {
            highContrast = SystemInformation.HighContrast;
        }
        catch
        {
            highContrast = false;
        }

        if (highContrast)
        {
            return ThemeMode.HighContrast;
        }

        return ReadSystemUsesLightTheme() ? ThemeMode.Light : ThemeMode.Dark;
    }

    private static void EnsureSystemWatcher()
    {
        if (_systemWatcherAttached)
        {
            return;
        }

        _systemWatcherAttached = true;
        _systemContext = SynchronizationContext.Current;
        try
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        catch
        {
            // A session without a message loop cannot observe system changes; the explicit mode still works.
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode != ThemeMode.System)
        {
            return;
        }

        if (_systemContext is { } context)
        {
            context.Post(static _ => RefreshSystemTheme(), null);
        }
        else
        {
            RefreshSystemTheme();
        }
    }

    private static bool ReadSystemUsesLightTheme()
    {
        var size = sizeof(int);
        var status = RegGetValueW(
            HkeyCurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            RrfRtRegDword,
            out _,
            out var value,
            ref size);
        return status == 0 && value != 0;
    }

    private static readonly IntPtr HkeyCurrentUser = new(unchecked((int)0x80000001));
    private const uint RrfRtRegDword = 0x00000010;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegGetValueW(
        IntPtr hkey,
        string? subKey,
        string? value,
        uint flags,
        out uint type,
        out int data,
        ref int dataSize);

    /// <summary>The built-in dark palette, matching the VS Code workbench and the Scribalix tokens.</summary>
    public static ThemePalette CreateDark() => ThemePalette.FromResourceDictionary(DarkSource);

    /// <summary>The built-in light palette, matching Visual Studio Code Light+.</summary>
    public static ThemePalette CreateLight() => ThemePalette.FromResourceDictionary(LightSource);

    /// <summary>The built-in high-contrast palette: pure black surfaces, white text and an amber accent.</summary>
    public static ThemePalette CreateHighContrast() => new()
    {
        WindowBackground = Color.Black,
        Surface = Color.FromArgb(0xFF, 0x0A, 0x0A, 0x0A),
        SurfaceAlt = Color.FromArgb(0xFF, 0x16, 0x16, 0x16),
        ControlBackground = Color.FromArgb(0xFF, 0x10, 0x10, 0x10),
        ControlBorder = Color.FromArgb(0xFF, 0x6F, 0x6F, 0x6F),
        ControlHover = Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F),
        ControlPressed = Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E),
        Foreground = Color.White,
        MutedForeground = Color.FromArgb(0xFF, 0xCF, 0xCF, 0xCF),
        Accent = Color.FromArgb(0xFF, 0xFF, 0xB0, 0x00),
        AccentForeground = Color.Black,
        FocusRing = Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00),
        Caret = Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00),
        DisabledForeground = Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A),
        DisabledBackground = Color.FromArgb(0xFF, 0x05, 0x05, 0x05),
        Selection = Color.FromArgb(0xFF, 0x6B, 0x46, 0x00),
        ScrollBarThumb = Color.FromArgb(0x66, 0xCF, 0xCF, 0xCF),
        ScrollBarThumbHover = Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
        ShapeStroke = Color.FromArgb(0x88, 0x5B, 0x8C, 0xFF),
        ShadowColor = Color.Black,
        FontFamily = "Segoe UI",
        FontSize = 9.5f,
        FontWeight = FontWeight.Normal,
        CornerRadius = 2f,
        ControlHeight = 30,
    };

    /// <summary>Creates a copy of a built-in theme's resource dictionary (palette brushes and typography styles).</summary>
    /// <param name="mode">The theme; <see cref="ThemeMode.System"/> uses <see cref="EffectiveMode"/>.</param>
    /// <returns>A new dictionary the caller may add to or merge.</returns>
    /// <remarks>Dark+ and Light+ come from the library's XAML dictionaries. High contrast is a runtime palette
    /// and its dictionary is generated; applications that want their own high-contrast theme should author a
    /// <c>ResourceDictionary</c> like the gallery does.</remarks>
    public static ResourceDictionary CreateResourceDictionary(ThemeMode mode)
    {
        if (mode == ThemeMode.HighContrast)
        {
            return CreateHighContrastResourceDictionary();
        }

        var source = SourceFor(mode == ThemeMode.System ? EffectiveMode : mode);
        var resources = new ResourceDictionary();
        foreach (var pair in source)
        {
            // 拷贝字典拥有自己的画笔：宿主切主题时会原地改色，不能让所有副本共享静态主题里的 Draw 刷。
            resources[pair.Key] = pair.Value is SolidColorBrush brush
                ? new SolidColorBrush(brush.Color)
                : pair.Value;
        }

        return resources;
    }

    internal static ResourceDictionary DefaultStyles { get; } = new Themes.DefaultStyles();

    // 把当前模式的资源原地同步进宿主的字典：画笔只改颜色，非画笔值不变就不写。
    internal static void SyncResourceDictionary(ResourceDictionary target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));

        var mode = EffectiveMode;
        var source = mode == ThemeMode.HighContrast ? HighContrastSource : SourceFor(mode);
        foreach (var pair in source)
        {
            if (pair.Value is SolidColorBrush sourceBrush)
            {
                if (target.TryGetValue(pair.Key, out var existingBrush) && existingBrush is SolidColorBrush targetBrush)
                {
                    // 原地改色让模板持有的同一支画笔跟着变；只赋值会让每个 DynamicResource 收到一次 Changed。
                    if (targetBrush.Color != sourceBrush.Color)
                    {
                        targetBrush.Color = sourceBrush.Color;
                    }
                }
                else
                {
                    target[pair.Key] = new SolidColorBrush(sourceBrush.Color);
                }

                continue;
            }

            if (target.TryGetValue(pair.Key, out var existing) && SameResourceValue(existing, pair.Value))
            {
                continue;
            }

            target[pair.Key] = pair.Value;
        }
    }

    // 主题里排版样式是两份独立实例但取值相同；按内容比较才能避免每次切换都重写并触发 Changed。
    private static bool SameResourceValue(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is SolidColorBrush leftBrush && right is SolidColorBrush rightBrush)
        {
            return leftBrush.Color == rightBrush.Color;
        }

        if (left is VisualStyle leftStyle && right is VisualStyle rightStyle)
        {
            return SameStyle(leftStyle, rightStyle);
        }

        return Equals(left, right);
    }

    private static bool SameStyle(VisualStyle left, VisualStyle right)
        => SameStyle(left, right, new HashSet<(VisualStyle Left, VisualStyle Right)>());

    private static bool SameStyle(
        VisualStyle left,
        VisualStyle right,
        HashSet<(VisualStyle Left, VisualStyle Right)> visited)
    {
        if (!visited.Add((left, right)))
        {
            return true;
        }

        // 主题里的样式是两份独立实例但链相同；BasedOn 也按内容递归比较，不能按引用或直接判不同。
        if (left.TargetType != right.TargetType
            || left.Template is not null
            || right.Template is not null
            || left.Triggers.Count != 0
            || right.Triggers.Count != 0
            || left.Setters.Count != right.Setters.Count)
        {
            return false;
        }

        if ((left.BasedOn is null) != (right.BasedOn is null))
        {
            return false;
        }

        if (left.BasedOn is not null && !SameStyle(left.BasedOn, right.BasedOn!, visited))
        {
            return false;
        }

        for (var index = 0; index < left.Setters.Count; index++)
        {
            var leftSetter = left.Setters[index];
            var rightSetter = right.Setters[index];
            if (!ReferenceEquals(leftSetter.Property, rightSetter.Property)
                || leftSetter.TargetName != rightSetter.TargetName
                || leftSetter.Binding is not null
                || rightSetter.Binding is not null
                || !SameResourceValue(leftSetter.Value, rightSetter.Value))
            {
                return false;
            }
        }

        return true;
    }

    internal static VisualStyle? FindDefaultStyle(VisualElement element)
    {
        // 隐式默认样式按确切类型匹配（WPF 语义）；派生类型要自己的样式或 BasedOn。
        return DefaultStyles.TryGetValue(element.GetType(), out var value)
               && value is VisualStyle style
               && style.Matches(element)
            ? style
            : null;
    }

    private static ResourceDictionary SourceFor(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => LightSource,
        _ => DarkSource,
    };

    private static ResourceDictionary CreateHighContrastResourceDictionary()
        => CreatePaletteResourceDictionary(CreateHighContrast());

    internal static ResourceDictionary CreatePaletteResourceDictionary(ThemePalette palette)
    {
        var resources = new ResourceDictionary();

        void Brush(string key, Color color) => resources[key] = new SolidColorBrush(color);

        Brush("Window", palette.WindowBackground);
        Brush("Surface", palette.Surface);
        Brush("Raised", palette.SurfaceAlt);
        Brush("Input", palette.ControlBackground);
        Brush("Border", palette.ControlBorder);
        Brush("Hover", palette.ControlHover);
        Brush("Pressed", palette.ControlPressed);
        Brush("Text", palette.Foreground);
        Brush("Muted", palette.MutedForeground);
        Brush("Accent", palette.Accent);
        Brush("AccentText", palette.AccentForeground);
        Brush("Focus", palette.FocusRing);
        Brush("Caret", palette.Caret);
        Brush("Selection", palette.Selection);
        Brush("SelectedText", palette.Foreground);
        Brush("Disabled", palette.DisabledBackground);
        Brush("DisabledText", palette.DisabledForeground);
        Brush("SurfaceAlt", palette.SurfaceAlt);
        Brush("ScrollBarThumb", palette.ScrollBarThumb);
        Brush("ScrollBarThumbHover", palette.ScrollBarThumbHover);
        Brush("ShapeStroke", palette.ShapeStroke);
        Brush("ShadowColor", palette.ShadowColor);

        resources["ShapeStrokeThickness"] = (double)palette.ShapeStrokeThickness;
        resources["ShadowOffset"] = palette.ShadowOffset;
        resources["ShadowBlur"] = (double)palette.ShadowBlur;
        resources["ShadowOpacity"] = palette.ShadowOpacity;
        Brush("TextVisualForeground", palette.TextVisualForeground);
        resources["TextVisualFontFamily"] = palette.TextVisualFontFamily;
        resources["TextVisualFontSize"] = (double)palette.TextVisualFontSize;
        resources["TextVisualFontWeight"] = palette.TextVisualFontWeight;

        resources["FontFamily"] = palette.FontFamily;
        resources["FontSize"] = (double)palette.FontSize;
        resources["FontWeight"] = palette.FontWeight;
        resources["CornerRadius"] = (double)palette.CornerRadius;
        resources["ControlHeight"] = palette.ControlHeight;
        resources["ControlBorderThickness"] = palette.ControlBorderThickness;
        resources["FocusThickness"] = (double)palette.FocusThickness;
        resources["FocusThicknessMedium"] = (double)palette.FocusThicknessMedium;
        resources["FocusThicknessThin"] = (double)palette.FocusThicknessThin;
        resources["ScrollBarThumbRadius"] = (double)palette.ScrollBarThumbRadius;
        AddTypographyStyles(resources, palette.Foreground, palette.MutedForeground, palette.FontFamily);
        return resources;
    }

    private static void AddTypographyStyles(ResourceDictionary resources, Color text, Color muted, string family)
    {
        VisualStyle Style(double size, FontWeight weight, Color color)
        {
            var style = new VisualStyle { TargetType = typeof(TextBlock) };
            style.Setters.Add(new VisualSetter(VisualElement.FontFamilyProperty, family));
            style.Setters.Add(new VisualSetter(VisualElement.FontSizeProperty, size));
            style.Setters.Add(new VisualSetter(VisualElement.FontWeightProperty, weight));
            style.Setters.Add(new VisualSetter(TextBlock.ForegroundProperty, new SolidColorBrush(color)));
            return style;
        }

        resources["Heading"] = Style(19d, FontWeight.SemiBold, text);
        resources["Subheading"] = Style(14d, FontWeight.SemiBold, text);
        resources["Body"] = Style(10d, FontWeight.Normal, text);
        resources["Caption"] = Style(9d, FontWeight.Normal, muted);
    }
}

# Visuals, brushes and composition

Two layers exist:

1. **Controls** (`VisualElement`) have layout, input and self-drawn chrome. Use them for anything interactive.
2. **Visuals** (`Visual`, `ContainerVisual`, `ShapeVisual`, `TextVisual`, `CurveBatchVisual`) are a retained,
   layout-free decoration tree. They are cheap for bulk shapes/text and are hosted by `VisualPresenter`.
   They have no input, focus or measure.

A `ContainerVisual` tree can also be the `VisualHost.Root`; a `VisualElement` is itself a `ContainerVisual`.

## Brushes

| Brush | Properties |
| --- | --- |
| `SolidColorBrush` | `Color`, `Opacity` |
| `LinearGradientBrush` | `StartPoint`/`EndPoint` (relative to the painted rect), `GradientStops`, `Opacity` |
| `RadialGradientBrush` | `Center` (relative), `Radius` (fraction of the shorter side), `GradientStops`, `Opacity` |
| `ImageBrush` | `ImageSource`, `Stretch`, `Opacity` |

`GradientStop` is `Color` + `Offset` (0..1). `Brush` has an implicit conversion from `Color` and to `Color`;
a null brush reads as transparent. Any brush can paint a fill, border or text.

```xml
<Border Width="320" Height="120" CornerRadius="12">
  <Border.Background>
    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
      <LinearGradientBrush.GradientStops>
        <GradientStop Color="#FF00D2FF" Offset="0" />
        <GradientStop Color="#FF7B2FF7" Offset="1" />
      </LinearGradientBrush.GradientStops>
    </LinearGradientBrush>
  </Border.Background>
</Border>

<Ellipse Width="120" Height="120">
  <Ellipse.Fill>
    <RadialGradientBrush Center="0.4,0.35" Radius="0.65">
      <RadialGradientBrush.GradientStops>
        <GradientStop Color="#FFFFE29F" Offset="0" />
        <GradientStop Color="#FFFF512F" Offset="1" />
      </RadialGradientBrush.GradientStops>
    </RadialGradientBrush>
  </Ellipse.Fill>
</Ellipse>
```

Text keeps a gradient: `TextBlock.Foreground` stores a Brush and `Graphics.DrawString` receives the native
gradient brush. A `TextVisual` also accepts a Brush. A multiline `TextBox` restarts a gradient per line.

## Geometry and Pen

`Geometry` subclasses: `LineGeometry` (`StartPoint`/`EndPoint`), `RectangleGeometry` (`Rect`, `RadiusX/Y`),
`EllipseGeometry` (`Rect`), `PathGeometry` (`Data` SVG subset), `GeometryGroup` (`Children`, `FillRule`).
`Geometry.Parse("M…")` creates a `PathGeometry`.

`Pen` (`Brush`, `Thickness`, `DashPattern`, line caps/joins) draws a `GraphicsPath`; `ShapeVisual.Stroke` is a
`Pen`, `ShapeElement.Stroke` is a Brush (controls use `StrokeThickness`).

## Lightweight Visual tree

```csharp
var layer = new ContainerVisual { Opacity = 0.88, CacheAsBitmap = true };
layer.Add(new ShapeVisual
{
    Bounds = new RectangleF(16, 16, 140, 140),
    Geometry = new EllipseGeometry(),
    Fill = new SolidColorBrush(Theme.Current.Accent),
    Effect = new DropShadowEffect { Offset = new PointF(6, 8), Blur = 10, Opacity = 0.5 },
});
layer.Add(new ShapeVisual
{
    Bounds = new RectangleF(110, 60, 170, 120),
    Geometry = new RectangleGeometry(),
    Fill = new LinearGradientBrush
    {
        GradientStops =
        {
            new GradientStop(Color.Cyan, 0),
            new GradientStop(Color.Magenta, 1),
        },
    },
});
layer.Add(new TextVisual
{
    Bounds = new RectangleF(16, 190, 320, 24),
    Text = "ContainerVisual · ShapeVisual · TextVisual",
    Foreground = new SolidColorBrush(Theme.Current.Foreground),
});

var presenter = new VisualPresenter { Content = layer, Height = 240, ClipToBounds = true };
```

- `Bounds` is in the parent's coordinates; content paints in `0..Width/Height`.
- `Transform` is a GDI+ `Matrix` in local coordinates; `Opacity`, `Clip`, `ClipToBounds`, `Effect`,
  `CacheAsBitmap`, `OpacityMask` compose the subtree.
- `ShapeVisual` (`Geometry`, `Fill`, `Stroke`), `TextVisual` (`Text`, `Font`, `Foreground`,
  `TextAlignment`, `Wrap`), `CurveBatchVisual` (`Stroke`, `Curves`, `Add`, `Clear`).
- `VisualPresenter` gives the visual tree a layout slot; it is non-hit-testable by default. A `Visual`
  content keeps its own bounds; a `VisualElement` content is measured/arranged like a child.
- `Visual` has `HitTestVisual(PointF)`, `HitTest(PointF)` and `InvalidateCache()`. Custom visuals must call
  `InvalidateCache` after changing what they draw.
- `UnboundedHitTest` lets a camera host's children hit outside the container's layout box.

Use the lightweight layer for decoration/charts/connectors; use a control when you need a layout slot, input,
focus, accessibility, templating or a scroll surface.

## Composition

| Property | Effect |
| --- | --- |
| `Opacity` | Composes the whole subtree through one bitmap, then applies alpha (children do not fade individually). |
| `Clip` | `RectangleF?` in local coordinates; clips paint and hit-test. |
| `ClipToBounds` | Clips children to the element's own rectangle. Required when a child can transform/rotate outside its slot. |
| `OpacityMask` | `Brush?`; multiplies the composed subtree by the brush alpha or luminance. |
| `OpacityMaskMode` | `Alpha` (default; WPF semantics) or `Luminance` (premultiplied luminance × alpha; white covers, black hides). |
| `Effect` | `VisualEffect`; `DropShadowEffect` (`Offset`, `Blur`, `Color`, `Opacity`). Effects disable bitmap cache. |
| `CacheAsBitmap` | Reuses the local subtree bitmap. DP changes inside mark the nearest cache dirty; custom visuals call `InvalidateCache`. |
| `Rendered` chrome | `VisualElement.Background`, `BorderBrush`, `BorderThickness`, `CornerRadius` are self-drawn. |

A gradient opacity mask over a group:

```xml
<Border Background="{StaticResource AuroraGradient}"
        OpacityMask="{StaticResource FadeMask}" />
<!-- Luminance: the mask's black side hides even though it is opaque -->
<Border Background="{StaticResource AuroraGradient}"
        OpacityMask="{StaticResource LuminanceMask}"
        OpacityMaskMode="Luminance" />
```

`AuroraGradient`, `FadeMask` and `LuminanceMask` are gallery demo resources (built in `GalleryTheme.Fill`),
not part of the theme dictionary; declare equivalent brushes in your own dictionary.

**Clip a transformed layer.** A visual that rotates/scales can paint outside its presenter; without
`ClipToBounds` on the stage and presenter, invalidation leaves stale pixels. This applies to `Visual.Transform`
and `Transform3D`.

## 3D transforms

`Transform3D` is available on every `VisualElement` (and every `Visual`):

| Type | Members |
| --- | --- |
| `Rotate3DTransform` | `AngleX`, `AngleY`, `AngleZ`, `CenterX/Y/Z`, `Depth` (camera distance; default 500; `<= 0` is orthographic) |
| `Scale3DTransform` | `ScaleX/Y/Z`, `CenterX/Y/Z` |
| `Translate3DTransform` | `X`, `Y`, `Z` |
| `MatrixTransform3D` | `M11..M44`, `Depth` |
| `Transform3DGroup` | `Children` composed in order |

```xml
<Border Width="240" Height="160" CornerRadius="14"
        Background="{StaticResource AuroraGradient}"
        CacheAsBitmap="True">
  <Border.Transform3D>
    <Rotate3DTransform AngleX="14" AngleY="-22" CenterX="120" CenterY="80" Depth="700" />
  </Border.Transform3D>
  <TextBlock Text="Perspective" Foreground="{StaticResource AccentText}" />
</Border>
```

- The subtree is composed to a bitmap and projected through a perspective-correct homography (per-pixel
  inverse sampling; bilinear). Hit testing and `AbsoluteBounds` use the same inverse map.
- `CacheAsBitmap` caches the **pre-warp** layer; transform-only changes reuse it, content changes invalidate it.
- Changing `Transform3D` invalidates the parent so old pixels are covered; the parent should also clip if the
  projection can leave the slot.
- Large projections fall back to a coarse cell warp to bound memory; avoid animating very large 3D subtrees.
- Layout still uses the element's untransformed `Width`/`Height`; the 3D projection does not change measure.

## Hit testing and Z order

`Visual.HitTestVisual(point)` returns the deepest hit; `ContainerVisual` honors `Panel.ZIndex` (higher on top;
ties use declaration order). `VisualElement.HitTestContent` returns `IsHitTestVisible`; a `Background` on a
`Border` is hit-testable even when transparent, matching WPF. Overlay popups hit before the root tree.

## Drawing on a custom Visual

```csharp
public sealed class Sparkline : Visual
{
    public Brush? Stroke { get; set; } // your own API
    protected override void RenderContent(Graphics graphics)
    {
        using var pen = new Pen(new SolidBrush(Theme.Current.Accent), 2f);
        graphics.DrawLines(pen, points);
    }
}
```

Call `InvalidateCache()` after changing the data. Draw text with `Graphics.DrawString` (the visual transform is
honored); `TextRenderer` writes directly to the HDC and ignores it. The runtime's `VisualText.Draw` is internal
and not available to application visuals.

# Animation

Easel ships its own animation engine. Trigger actions and `VisualTransition` interpolate values, pace
frames and hold the end value themselves; there is no backend to register and no seam to inject. Requesting
an animation on a type the engine cannot interpolate throws a clear `NotSupportedException` instead of
silently doing nothing.

## Timing

```csharp
using Easel.Animation;

public sealed class AnimationSpec
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(240);
    public IEase Ease { get; set; } = Eases.Cubic.Out;
    public int FPS { get; set; } = 60;
    public int LoopTime { get; set; }
    public bool IsAutoReverse { get; set; }
}
```

| `AnimationSpec` | Meaning                                                                                                                                     |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `Duration`      | Total duration; default 240 ms.                                                                                                             |
| `Ease`          | `IEase`; default `Eases.Cubic.Out`. `Eases` provides Sine/Quad/Cubic/Quart/Quint/Expo/Circ/Back/Elastic/Bounce with `In/Out/InOut`. |
| `FPS`           | Target frame rate; default 60.                                                                                                              |
| `LoopTime`      | Additional full passes; 0 once, `int.MaxValue` forever.                                                                                   |
| `IsAutoReverse` | Each pass also runs backwards.                                                                                                              |

The engine writes at `BindingPriority.Animation` while running and supersedes an animation on the same property.
When an explicit animation completes or is stopped, it holds the reached value at local precedence and clears the
animation layer; an implicit element transition only releases the layer, because its new value already lives in the
trigger, style or local layer. A superseded animation simply ends and the newer one takes over. The value is
converted to the DP type before it reaches the engine.

## Markup actions

Inside a style/template trigger:

```xml
<Trigger Property="IsEnabled" Value="False">
  <Trigger.EnterAction>
    <TransitionAction Property="Opacity" To="0.6" Duration="0:0:0.18" Ease="SineOut" />
  </Trigger.EnterAction>
  <Trigger.ExitAction>
    <KeyFrameAnimation Property="Opacity" Duration="0:0:0.3" Ease="CubicOut">
      <KeyFrame Offset="0" Value="0.6" />
      <KeyFrame Offset="1" Value="1" />
    </KeyFrameAnimation>
  </Trigger.ExitAction>
</Trigger>
```

- `TransitionAction`: `Property` (DP), `To`, plus optional `Duration`, `Ease`, `LoopTime`, `IsAutoReverse`
  attributes that the compiler folds into its `Effect` (`AnimationSpec`).
- `KeyFrameAnimation`: `Property`, `Duration`, `Ease`, `LoopTime`, `IsAutoReverse`, `KeyFrames`
  (`Offset` 0..1 + `Value`, sorted before invocation).
- `TriggerActionGroup`: `Actions` run in order.
- A custom action derives `VisualTriggerAction` and overrides `Invoke(VisualElement target)`. In a template
  trigger the target is the `TargetName` part, or the templated control when no part name is set.
- A trigger's enter/exit actions run once per state change, not on every repaint.

`TransitionAction`/`KeyFrameAnimation` call `VisualTransition.Animate`/`AnimateKeyFrames`, which reject a
property the engine cannot interpolate (`NotSupportedException`) rather than running an empty animation.

## Element transitions

A `VisualElement` exposes a `Transitions` collection; each `Transition` names one DP on that element:

```xml
<Border.Transitions>
  <Transition Property="Background" Duration="0:0:0.25" Ease="CubicOut" />
  <Transition Property="CornerRadius" Duration="0:0:0.25" Ease="CubicOut" />
</Border.Transitions>
```

Every change of a listed DP animates from the old value to the new value, whatever its source — a style
setter, a trigger setter, a local write or a binding. The transition sees the change, not the cause, so no
enter/exit action pair is needed: when a trigger exits, its setter falls back to the baseline and the same
transition animates back. `Duration` defaults to 240 ms and `Ease` to `CubicOut`.

- Writes made by the animation itself, including the final layer release, do not re-trigger it.
- A property with no interpolation, or a change whose old or new value is `null`, is skipped.
- A change that arrives mid-flight retargets the running transition from its current frame to the new target.
  This includes a trigger or style value changing underneath the animation layer.

The default `ComboBox` rings use it: the state triggers set ring `Opacity` to 1 (baseline 0) and each
ring's `Transitions` collection fades the change both ways.

## Code API

```csharp
using Easel.Animation;
using Easel.Visuals;

VisualTransition.Animate(
    card,
    VisualElement.OpacityProperty,
    0.6,
    new AnimationSpec { Duration = TimeSpan.FromMilliseconds(180), Ease = Eases.Sine.Out });

VisualTransition.AnimateKeyFrames(
    card,
    VisualElement.OpacityProperty,
    [
        new KeyFrame { Offset = 0, Value = 1 },
        new KeyFrame { Offset = 1, Value = 0.6 },
    ],
    TimeSpan.FromMilliseconds(240));

VisualTransition.Stop(card);
```

`VisualTransition.Stop` ends every animation on the element. An explicit animation writes the reached value at
local precedence and clears the animation layer, so a later local/style write can take over; an implicit element
transition only releases the layer.

## How the engine runs

`VisualTransition` registers the animation per `(element, dependency property)`. A newer animation on the
same property supersedes the older one; animations on different properties run independently, so a
`TriggerActionGroup` can animate several properties at once.

- Frames are computed on a timer at `FPS` and written at `BindingPriority.Animation`.
- When the element is hosted, frame writes and the final hold are marshalled to its `VisualHost` handle
  thread; an unparented element is written in place.
- When an explicit animation completes or is stopped, the reached value is written at `BindingPriority.LocalValue`
  and the animation layer is cleared; an implicit element transition only releases the layer. A superseded
  animation ends without a hold; the newer run owns the property.
- Key frames form one chain: each adjacent pair is a segment whose duration is proportional to its offset
  slice, and `LoopTime`/`IsAutoReverse` apply to each segment.
- A live native island cannot be transformed or alpha-blended: animating `Opacity` or `Transform` on a
  `NativeElement`/`NativeControlHost` switches it to the snapshot path automatically. A focused `TextBox` with
  `UseNativeEditor=true` is a child HWND and cannot follow.

## Animatable properties

Animate **DP-backed** properties: opacity, margin/padding, width/height, corner radius, background/foreground
brushes, `Transform`, `Transform3D`, `Clip`, a custom `[XamlProperty]`, etc. A property whose CLR type has no
interpolation is rejected by `VisualTransition`; do not rely on a silent no-op.

| Group      | Types                                                                                                 |
| ---------- | ----------------------------------------------------------------------------------------------------- |
| numbers    | `double`, `float`, `int`, `long`, `short`, `byte`, `decimal`                            |
| geometry   | `Point`, `PointF`, `Size`, `SizeF`, `Rectangle`, `RectangleF`, `Padding`, `Thickness` |
| transforms | `Matrix` (2D), `Matrix4x4`, `Transform3D`                                                       |
| colour     | `Color`, `Brush`                                                                                  |

Nullable value types of these are supported. Brush and `Transform3D` interpolation is **structural**:

- solid↔solid lerps the colour and the brush opacity;
- gradient↔gradient lerps the stop list, the geometry and the opacity; a solid against a gradient is lifted to
  the gradient kind and both sides are sampled at the union of their stop offsets, so both endpoints stay exact;
- `Transform3D` of the same kind lerps its own angles/centres/scale/depth; mixed kinds blend the composed matrix;
- a pair with no shared structure (linear↔radial, any `ImageBrush`) returns a `CrossFadeBrush`: each side is
  rendered into its own layer and the layers are blended, so the transition is a real cross-fade.

Not animatable, by design: `bool`, enums (`Visibility`, alignments, `FontWeight`, `OpacityMaskMode`), `string`,
`Cursor` and `VisualEffect`. `VisualTransition` throws `NotSupportedException` naming the property.

`Visual.Opacity`, `Visual.Transform`, `Visual.Clip` and `Visual.Effect` are plain visual properties with no
change notification. On a `VisualElement`, the `Opacity`/`Transform`/`Clip`/`Effect` DPs are available; on a
custom lightweight `Visual`, expose a DP on a visual element that feeds it, or call `InvalidateVisual` yourself.

## Composition timing

- Start animations on the UI thread; a hosted element's frames are marshalled to its handle thread.
- A later animation on the same property supersedes the earlier one; an interrupted animation leaves the
  value where it is.
- When a trigger exits, its exit action starts a new animation; `Stop` is for teardown or a hard cancel.
- `CacheAsBitmap` does not cache animation frames; an animation inside a cached subtree invalidates the nearest
  cache. Keep animated groups small.
- `Transform3D` animation re-warps the composed bitmap each frame; prefer a cached pre-warp layer when only the
  transform changes.
- If the view is replaced, dispose it; pending animations on the old tree are stopped by
  `VisualElement.Dispose`.

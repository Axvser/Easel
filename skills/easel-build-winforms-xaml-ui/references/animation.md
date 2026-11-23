# Animation contract

Easel owns the **animation contract**, not an engine. Trigger actions and `VisualTransition` call
`AnimationRuntime.Animator`; the host injects an `IVisualAnimator` implementation. Until then, requesting an
animation throws a clear `InvalidOperationException` instead of silently doing nothing.

## The contract

```csharp
using Easel.Animation;

public static class AnimationRuntime
{
    public static IVisualAnimator Animator { get; set; } // null restores the no-op backend
    public static bool HasAnimator { get; }
}

public interface IVisualAnimator
{
    bool CanAnimate(VisualElement target, DependencyProperty property);
    void Animate(VisualElement target, DependencyProperty property, object? to, AnimationSpec spec);
    void AnimateKeyFrames(VisualElement target, DependencyProperty property,
        IReadOnlyList<KeyFrame> frames, AnimationSpec spec);
    void Stop(VisualElement target);
    bool HasActive(VisualElement target);
}
```

| `AnimationSpec` | Meaning |
| --- | --- |
| `Duration` | Total duration; default 240 ms. |
| `Ease` | `IEase`; default `Eases.Cubic.Out`. `Eases` provides Sine/Quad/Cubic/Quart/Quint/Expo/Circ/Back/Elastic/Bounce with `In/Out/InOut`. |
| `FPS` | Target frame rate; default 60. |
| `LoopTime` | Additional full passes; 0 once, `int.MaxValue` forever. |
| `IsAutoReverse` | Each pass also runs backwards. |

The animator must write at `BindingPriority.Animation` while running, supersede an animation on the same
property, and hold the final value at local precedence when it completes, is superseded or is stopped. The
value is converted to the DP type before it reaches the backend.

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
property the backend cannot sample (`NotSupportedException`) rather than running an empty animation.

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

`VisualTransition.Stop` freezes the reached value at local precedence and clears the animation layer, so a
later local/style write can take over.

## Injecting an engine

A host package assigns the implementation before any trigger runs. VeloxDev.WinForms supplies an adapter that
bridges Easel's DP animation to VeloxDev's transition engine:

```csharp
// conceptual host wiring; the adapter package does this in its module initializer
AnimationRuntime.Animator = myAnimator;
```

**If the application accepts the VeloxDev dependency, install `VeloxDev.WinForms` version `12.0.0` or later.**
That package's module initializer assigns `AnimationRuntime.Animator = new EaselVisualAnimator()` as soon as the
assembly loads — no separate animation package, no manual registration, and no code in the UI layer. The
earliest supported version for automatic Easel animation wiring is **V12**.

- The adapter owns samplers, frame pacing and easing. Easel does not ship samplers for arbitrary types.
- `CanAnimate` decides whether a DP is accepted. A backend may support only its sampler table; a custom type
  needs a sampler registration in that backend.
- Without an injected backend, `AnimationRuntime.HasAnimator` is false and animation requests throw with a
  message pointing at `AnimationRuntime.Animator`.
- Native islands (the focused `TextBox` editor, `NativeControlHost`) are not part of the visual animation.

## Animatable properties

Animate **DP-backed** properties: opacity, margin/padding, width/height, corner radius, background/foreground
brushes, a custom `[XamlProperty]`, etc. The backend determines which CLR types it can interpolate. A property
with no sampler is rejected by `VisualTransition`; do not rely on a silent no-op.

`Visual.Opacity`, `Visual.Transform`, `Visual.Clip` and `Visual.Effect` are plain visual properties with no
change notification. On a `VisualElement`, the `Opacity`/`Transform`/`Clip`/`Effect` DPs are available; on a
custom lightweight `Visual`, expose a DP on a visual element that feeds it, or call `InvalidateVisual` yourself.

## Composition timing

- Start animations on the UI thread; frames must be delivered through the host's message loop.
- A later animation on the same target supersedes the earlier one; an interrupted animation should leave the
  value where it is, then continue from there if the backend supports it.
- When a trigger exits, its exit action starts a new animation; `Stop` is for teardown or a hard cancel.
- `CacheAsBitmap` does not cache animation frames; an animation inside a cached subtree invalidates the nearest
  cache. Keep animated groups small.
- `Transform3D` animation re-warps the composed bitmap each frame; prefer a cached pre-warp layer when only the
  transform changes.
- If the view is replaced, dispose it; pending backend animations on the old tree should be stopped by the host
  or the backend's lifecycle.

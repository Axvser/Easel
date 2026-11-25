using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Easel.Visuals.Controls;

/// <summary>Proxy dependency properties that let the binding engine drive a native control's CLR properties.</summary>
/// <remarks>
/// <para>
/// A native control has no dependency properties, so a binding cannot target it directly. This helper registers one
/// attached property per (native type, property) pair. The binding writes that property on the
/// <see cref="NativeElement"/> wrapper (which is an <see cref="IWinformsObject"/>), the change callback copies the
/// value to the control, and the binding engine keeps its usual source/update semantics.
/// </para>
/// <para>
/// A two-way binding also needs the control to report its own changes. <see cref="TrackTwoWay"/> subscribes to
/// <see cref="INotifyPropertyChanged"/> or the conventional <c>XxxChanged</c> event when the control exposes one.
/// A control that exposes neither reports its changes through <see cref="NotifyChanged"/>; the call reads the native
/// property and writes the proxy dependency property, so the binding updates its source. Nothing polls.
/// </para>
/// </remarks>
public static class NativePropertyBinding
{
    private static readonly object Gate = new();
    private static readonly Dictionary<(Type Type, string Name), DependencyProperty> Cache = [];
    private static readonly ConditionalWeakTable<Control, Dictionary<string, List<NativeTarget>>> Targets = new();

    /// <summary>Returns the proxy dependency property for <paramref name="propertyName"/> on <paramref name="nativeType"/>.</summary>
    /// <param name="nativeType">The native control type, for example <c>typeof(System.Windows.Forms.Button)</c>.</param>
    /// <param name="propertyName">The writable public instance property to forward values to.</param>
    /// <returns>The cached proxy property.</returns>
    /// <exception cref="InvalidOperationException">The type has no such property, or the property is read-only.</exception>
    public static DependencyProperty For(Type nativeType, string propertyName)
    {
        if (nativeType is null) throw new ArgumentNullException(nameof(nativeType));
        if (string.IsNullOrEmpty(propertyName)) throw new ArgumentException("A native binding needs a property name.", nameof(propertyName));

        lock (Gate)
        {
            if (Cache.TryGetValue((nativeType, propertyName), out var cached))
            {
                return cached;
            }

            var property = nativeType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"'{nativeType.FullName}' has no public instance property '{propertyName}'.");
            if (property.SetMethod is null)
            {
                throw new InvalidOperationException(
                    $"'{nativeType.FullName}.{propertyName}' is read-only; a native binding needs a writable property.");
            }

            var defaultValue = property.PropertyType.IsValueType
                               && Nullable.GetUnderlyingType(property.PropertyType) is null
                ? Activator.CreateInstance(property.PropertyType)
                : null;
            var typeKey = (nativeType.FullName ?? nativeType.Name).Replace('.', '_').Replace('+', '_');
            var dependencyProperty = DependencyProperty.RegisterAttached(
                "Native_" + typeKey + "_" + propertyName,
                property.PropertyType,
                typeof(NativePropertyBinding),
                new PropertyMetadata(defaultValue)
                {
                    // 值只写给原生控件，包装元素的绘制/布局不因它变化。
                    AffectsRender = false,
                    VisualPropertyChangedCallback = (target, e) => Apply(target, propertyName, e),
                });

            Cache[(nativeType, propertyName)] = dependencyProperty;
            return dependencyProperty;
        }
    }

    /// <summary>Subscribes to the native change notifications available for a two-way binding.</summary>
    /// <param name="element">The wrapper whose proxy property receives the native value.</param>
    /// <param name="property">The proxy property returned by <see cref="For"/>.</param>
    /// <param name="propertyName">The native property name.</param>
    /// <remarks>
    /// The subscription follows the element's current native control and is replaced when that control is replaced;
    /// it is released when the element is disposed. When the control exposes neither
    /// <see cref="INotifyPropertyChanged"/> nor a conventional <c>XxxChanged</c> event, the binding stays registered
    /// and waits for <see cref="NotifyChanged"/> instead of polling.
    /// </remarks>
    public static void TrackTwoWay(NativeElement element, DependencyProperty property, string propertyName)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (string.IsNullOrEmpty(propertyName)) throw new ArgumentException("A native binding needs a property name.", nameof(propertyName));

        element.TrackTwoWay(propertyName, property);
    }

    /// <summary>Pushes the current native value of <paramref name="propertyName"/> into every two-way binding on <paramref name="native"/>.</summary>
    /// <param name="native">The native control whose property changed.</param>
    /// <param name="propertyName">The native property name, for example <c>nameof(TextBox.Text)</c>.</param>
    /// <remarks>
    /// Call this after changing a native property from a control that exposes neither
    /// <see cref="INotifyPropertyChanged"/> nor a conventional <c>XxxChanged</c> event. The value is written to the
    /// proxy dependency property and the binding updates its source; an unbound or unchanged value is a no-op.
    /// Controls that do expose those notifications do not need the call.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="native"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
    public static void NotifyChanged(Control native, string propertyName)
    {
        if (native is null) throw new ArgumentNullException(nameof(native));
        if (string.IsNullOrEmpty(propertyName)) throw new ArgumentException("A native binding needs a property name.", nameof(propertyName));
        if (native.IsDisposed)
        {
            return;
        }

        if (!Targets.TryGetValue(native, out var byName)
            || !byName.TryGetValue(propertyName, out var targets))
        {
            return;
        }

        NativeTarget[] snapshot;
        lock (byName)
        {
            snapshot = [.. targets];
        }

        foreach (var target in snapshot)
        {
            if (target.Element.TryGetTarget(out var element)
                && ReferenceEquals(element.Native, native))
            {
                WriteBack(element, native, propertyName, target.Property);
                continue;
            }

            UnregisterTarget(native, propertyName, target);
        }
    }

    internal static void Apply(IWinformsObject target, string propertyName, DependencyPropertyChangedEventArgs e)
    {
        if (target is not NativeElement element || element.Native is not { } native)
        {
            return;
        }

        var property = native.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.SetMethod is null)
        {
            return;
        }

        var value = e.NewValue;
        if (value is null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
        {
            return;
        }

        try
        {
            if (Equals(property.GetValue(native), value))
            {
                return;
            }

            property.SetValue(native, value);
        }
        catch (Exception)
        {
            // 绑定转换按代理属性类型做过；这里失败通常是目标属性拒绝了该值，保持原生值不动。
        }
    }

    internal static IDisposable? Subscribe(
        NativeElement element,
        Control native,
        string propertyName,
        DependencyProperty property)
    {
        if (native.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetMethod is null)
        {
            return null;
        }

        var target = RegisterTarget(native, propertyName, element, property);
        IDisposable? notification = null;

        // 原生控件的两种自动回报渠道，按可靠性从高到低。
        if (native is INotifyPropertyChanged notifier)
        {
            void OnNotified(object? sender, PropertyChangedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.PropertyName)
                    && !string.Equals(e.PropertyName, propertyName, StringComparison.Ordinal))
                {
                    return;
                }

                WriteBack(element, native, propertyName, property);
            }

            notifier.PropertyChanged += OnNotified;
            notification = new Subscription(() => notifier.PropertyChanged -= OnNotified);
        }
        else
        {
            var eventInfo = native.GetType().GetEvent(propertyName + "Changed", BindingFlags.Public | BindingFlags.Instance);
            if (eventInfo?.EventHandlerType == typeof(EventHandler))
            {
                EventHandler handler = (_, _) => WriteBack(element, native, propertyName, property);
                eventInfo.AddEventHandler(native, handler);
                notification = new Subscription(() => eventInfo.RemoveEventHandler(native, handler));
            }
        }

        // 没有事件也不轮询：宿主在值变化后调用 NotifyChanged 显式上报。
        return new Subscription(() =>
        {
            notification?.Dispose();
            UnregisterTarget(native, propertyName, target);
        });
    }

    private static NativeTarget RegisterTarget(Control native, string propertyName, NativeElement element, DependencyProperty property)
    {
        var target = new NativeTarget(element, property);
        var byName = Targets.GetOrCreateValue(native);
        lock (byName)
        {
            if (!byName.TryGetValue(propertyName, out var list))
            {
                byName[propertyName] = list = [];
            }

            list.Add(target);
        }

        return target;
    }

    private static void UnregisterTarget(Control native, string propertyName, NativeTarget target)
    {
        if (!Targets.TryGetValue(native, out var byName))
        {
            return;
        }

        lock (byName)
        {
            if (byName.TryGetValue(propertyName, out var list))
            {
                list.Remove(target);
                if (list.Count == 0)
                {
                    byName.Remove(propertyName);
                }
            }
        }
    }

    private static void WriteBack(NativeElement element, Control native, string propertyName, DependencyProperty property)
    {
        if (native.IsDisposed)
        {
            return;
        }

        var nativeProperty = native.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (nativeProperty?.GetMethod is null)
        {
            return;
        }

        object? value;
        try
        {
            value = nativeProperty.GetValue(native);
        }
        catch (Exception)
        {
            return;
        }

        if (Equals(element.GetValue(property), value))
        {
            return;
        }

        // 本地层变化会触发 TwoWay 绑定把值写回源；写回过程中绑定自身会挡住回环。
        element.SetValue(property, value);
    }

    private sealed class NativeTarget(NativeElement element, DependencyProperty property)
    {
        internal WeakReference<NativeElement> Element { get; } = new(element);

        internal DependencyProperty Property { get; } = property;
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            unsubscribe();
        }
    }
}

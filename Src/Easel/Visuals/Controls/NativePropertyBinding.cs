using System.ComponentModel;
using System.Reflection;
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
/// A two-way binding also needs the control to report its own changes. <see cref="TrackTwoWay"/> subscribes to the
/// conventional <c>XxxChanged</c> event (for example <c>Text</c> → <c>TextChanged</c>); when the control changes, the
/// proxy property is written and the binding updates its source. A property without a matching
/// <see cref="EventHandler"/> stays one-way.
/// </para>
/// </remarks>
public static class NativePropertyBinding
{
    private static readonly object Gate = new();
    private static readonly Dictionary<(Type Type, string Name), DependencyProperty> Cache = [];
    private static readonly object PollGate = new();
    private static readonly List<PolledBinding> Polled = [];
    private static System.Windows.Forms.Timer? _pollTimer;
    private static bool _idleHooked;

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

    /// <summary>Subscribes to the native <c>XxxChanged</c> event so a two-way binding can update its source.</summary>
    /// <param name="element">The wrapper whose proxy property receives the native value.</param>
    /// <param name="property">The proxy property returned by <see cref="For"/>.</param>
    /// <param name="propertyName">The native property name.</param>
    /// <remarks>
    /// The subscription follows the element's current native control and is replaced when that control is replaced;
    /// it is released when the element is disposed.
    /// </remarks>
    public static void TrackTwoWay(NativeElement element, DependencyProperty property, string propertyName)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (string.IsNullOrEmpty(propertyName)) throw new ArgumentException("A native binding needs a property name.", nameof(propertyName));

        element.TrackTwoWay(propertyName, property);
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

        // 原生控件的三种回报渠道，按可靠性从高到低。
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
            return new Subscription(() => notifier.PropertyChanged -= OnNotified);
        }

        var eventInfo = native.GetType().GetEvent(propertyName + "Changed", BindingFlags.Public | BindingFlags.Instance);
        if (eventInfo?.EventHandlerType == typeof(EventHandler))
        {
            EventHandler handler = (_, _) => WriteBack(element, native, propertyName, property);
            eventInfo.AddEventHandler(native, handler);
            return new Subscription(() => eventInfo.RemoveEventHandler(native, handler));
        }

        // 既没有 INotifyPropertyChanged 也没有 XxxChanged：用共享定时器轮询，消灭“只能单向”的语义差。
        return StartPolling(element, native, propertyName, property);
    }

    internal static void PollAll()
    {
        PolledBinding[] snapshot;
        lock (PollGate)
        {
            snapshot = [.. Polled];
        }

        foreach (var binding in snapshot)
        {
            binding.Poll();
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

    private static IDisposable StartPolling(NativeElement element, Control native, string propertyName, DependencyProperty property)
    {
        var binding = new PolledBinding(element, native, propertyName, property);
        lock (PollGate)
        {
            Polled.Add(binding);
        }

        EnsurePollTimer();
        return new Subscription(() =>
        {
            lock (PollGate)
            {
                Polled.Remove(binding);
            }

            StopPollTimerIfEmpty();
        });
    }

    private static void EnsurePollTimer()
    {
        if (_pollTimer is not null)
        {
            if (!_pollTimer.Enabled)
            {
                _pollTimer.Start();
            }
        }
        else
        {
            // 兜底：没有窗口消息的静默赋值在这个周期内被读到。
            _pollTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _pollTimer.Tick += (_, _) => PollAll();
            _pollTimer.Start();
        }

        if (!_idleHooked)
        {
            // UI 驱动的变化在消息队列一空就同步回读，不再等定时器。
            Application.Idle += OnApplicationIdle;
            _idleHooked = true;
        }
    }

    private static void OnApplicationIdle(object? sender, EventArgs e) => PollAll();

    private static void StopPollTimerIfEmpty()
    {
        if (_pollTimer is null)
        {
            return;
        }

        lock (PollGate)
        {
            if (Polled.Count > 0)
            {
                return;
            }
        }

        _pollTimer.Stop();
        if (_idleHooked)
        {
            Application.Idle -= OnApplicationIdle;
            _idleHooked = false;
        }
    }

    private sealed class PolledBinding(NativeElement element, Control native, string propertyName, DependencyProperty property)
    {
        public void Poll()
        {
            if (native.IsDisposed)
            {
                return;
            }

            WriteBack(element, native, propertyName, property);
        }
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

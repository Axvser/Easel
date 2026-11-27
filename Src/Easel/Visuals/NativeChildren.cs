using System.Collections;
using System.Windows.Forms;
using Easel.Visuals.Controls;

namespace Easel.Visuals;

/// <summary>
/// The native WinForms controls hosted by one <see cref="VisualElement"/> as islands above its visual content.
/// </summary>
/// <remarks>
/// <para>
/// Each entry is a <see cref="NativeElement"/> that wraps the caller's <see cref="Control"/>: the control keeps its
/// own HWND, input, focus and IME, while the wrapper participates in the visual tree (resources, inherited
/// <see cref="VisualElement.DataContext"/>, ancestor clips, theme). Adding creates the wrapper; removing detaches
/// and disposes it, and disposes the control only when it was added with <c>ownsNative</c>.
/// </para>
/// <para>
/// Placement follows the layout contract of the host: when the host lays out its children (a
/// <see cref="Panel"/>), the island is one of those children; otherwise it fills the host's content box and
/// honours the wrapper's <see cref="VisualElement.Margin"/>, <see cref="VisualElement.Width"/>,
/// <see cref="VisualElement.Height"/> and alignment. The list order is the z-order among the host's islands.
/// </para>
/// </remarks>
public sealed class NativeChildrenCollection : IReadOnlyList<NativeElement>
{
    private readonly VisualElement _owner;
    private readonly List<NativeElement> _items = [];

    internal NativeChildrenCollection(VisualElement owner) => _owner = owner;

    /// <summary>The number of hosted native controls.</summary>
    public int Count => _items.Count;

    /// <summary>The wrapper at <paramref name="index"/>, in z-order.</summary>
    /// <param name="index">The zero-based position.</param>
    public NativeElement this[int index] => _items[index];

    /// <summary>Adds <paramref name="child"/> as an island of the host and returns its wrapper.</summary>
    /// <param name="child">The native control to host.</param>
    /// <param name="ownsNative">Whether disposing the wrapper disposes <paramref name="child"/>.</param>
    /// <returns>The wrapper, already parented to the host.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The control is already hosted by this collection.</exception>
    /// <remarks>
    /// The control is taken from its previous WinForms parent, matching <see cref="Control.ControlCollection.Add(Control)"/>.
    /// The new island is appended last, so it paints above earlier ones.
    /// </remarks>
    public NativeElement Add(Control child, bool ownsNative = false)
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        if (IndexOf(child) >= 0)
        {
            throw new InvalidOperationException("This control is already hosted by the element.");
        }

        child.Parent?.Controls.Remove(child);
        var host = new NativeElement(child, ownsNative) { UseOwnerContentBox = true };
        _owner.Add(host);
        _items.Add(host);
        ApplyOrder();
        return host;
    }

    /// <summary>The wrapper that currently hosts <paramref name="child"/>, or <see langword="null"/>.</summary>
    /// <param name="child">The native control to look up.</param>
    public NativeElement? Find(Control child)
    {
        var index = IndexOf(child);
        return index < 0 ? null : _items[index];
    }

    /// <summary>Removes and disposes the island that hosts <paramref name="child"/>.</summary>
    /// <param name="child">The native control to stop hosting.</param>
    /// <returns><see langword="true"/> when the control was hosted by this collection.</returns>
    public bool Remove(Control child)
    {
        if (child is null) return false;
        var index = IndexOf(child);
        return index >= 0 && RemoveAt(index);
    }

    /// <summary>Removes and disposes <paramref name="host"/>.</summary>
    /// <param name="host">The wrapper returned by <see cref="Add"/>.</param>
    /// <returns><see langword="true"/> when the wrapper was hosted by this collection.</returns>
    public bool Remove(NativeElement host)
    {
        if (host is null) return false;
        var index = _items.IndexOf(host);
        return index >= 0 && RemoveAt(index);
    }

    /// <summary>Moves an island to another position in the host's z-order.</summary>
    /// <param name="child">The native control to move.</param>
    /// <param name="index">The zero-based destination; it is clamped to the collection bounds.</param>
    /// <returns><see langword="true"/> when the control was hosted by this collection.</returns>
    public bool Move(Control child, int index)
    {
        var host = child is null ? null : Find(child);
        return host is not null && Move(host, index);
    }

    /// <summary>Moves an island to another position in the host's z-order.</summary>
    /// <param name="host">The wrapper returned by <see cref="Add"/>.</param>
    /// <param name="index">The zero-based destination; it is clamped to the collection bounds.</param>
    /// <returns><see langword="true"/> when the wrapper was hosted by this collection.</returns>
    public bool Move(NativeElement host, int index)
    {
        var current = _items.IndexOf(host);
        if (current < 0) return false;

        index = index < 0 ? 0 : index >= _items.Count ? _items.Count - 1 : index;
        if (current == index) return true;

        _items.RemoveAt(current);
        _items.Insert(index, host);
        ApplyOrder();
        return true;
    }

    /// <summary>Removes and disposes every island; a control added with <c>ownsNative</c> is disposed with it.</summary>
    public void Clear()
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            RemoveAt(index);
        }
    }

    /// <inheritdoc />
    public IEnumerator<NativeElement> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int IndexOf(Control child)
    {
        for (var index = 0; index < _items.Count; index++)
        {
            if (ReferenceEquals(_items[index].Native, child))
            {
                return index;
            }
        }

        return -1;
    }

    private bool RemoveAt(int index)
    {
        var host = _items[index];
        _items.RemoveAt(index);
        // 先从宿主摘岛：RemoveChild 会清掉 Surface，之后 Dispose 就找不到宿主，HWND 会留在表面上。
        _owner.Surface?.RemoveNative(host);
        _owner.RemoveChild(host);
        host.Dispose();
        ApplyOrder();
        return true;
    }

    // 岛之间的 z 序由 Panel.ZIndex 决定；集合顺序写回 ZIndex，再让宿主按新序重排子 HWND。
    private void ApplyOrder()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            Easel.Visuals.Controls.Panel.SetZIndex(_items[index], index);
        }

        _owner.Surface?.ReorderNativeChildren();
    }
}

/// <summary>Native child hosting on a visual element.</summary>
public abstract partial class VisualElement
{
    private NativeChildrenCollection? _nativeChildren;

    /// <summary>The native WinForms controls hosted by this element.</summary>
    /// <remarks>
    /// Use <see cref="AddNative"/> for the common case. The collection is created on first access.
    /// </remarks>
    public NativeChildrenCollection NativeChildren => _nativeChildren ??= new NativeChildrenCollection(this);

    /// <summary>Adds <paramref name="child"/> as a native island of this element and returns its wrapper.</summary>
    /// <param name="child">The native control to host.</param>
    /// <param name="ownsNative">Whether disposing the wrapper disposes <paramref name="child"/>.</param>
    /// <returns>The wrapper, already parented to this element.</returns>
    /// <remarks>
    /// See <see cref="NativeChildrenCollection.Add"/> for placement and z-order.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The control is already hosted by this element.</exception>
    public NativeElement AddNative(Control child, bool ownsNative = false)
        => NativeChildren.Add(child, ownsNative);

    /// <summary>Removes and disposes the island that hosts <paramref name="child"/>.</summary>
    /// <param name="child">The native control to stop hosting.</param>
    /// <returns><see langword="true"/> when the control was hosted by this element.</returns>
    public bool RemoveNative(Control child) => _nativeChildren?.Remove(child) ?? false;

    /// <summary>Removes and disposes <paramref name="host"/>.</summary>
    /// <param name="host">The wrapper returned by <see cref="AddNative"/>.</param>
    /// <returns><see langword="true"/> when the wrapper was hosted by this element.</returns>
    public bool RemoveNative(NativeElement host) => _nativeChildren?.Remove(host) ?? false;
}

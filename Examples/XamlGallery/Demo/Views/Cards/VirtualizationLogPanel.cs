using System.Drawing;
using System.Windows.Forms;
using Demo.ViewModels;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>A custom <see cref="VirtualizingPanel"/> that windows a million-row log.</summary>
/// <remarks>
/// <para>
/// The card uses this subclass instead of <see cref="VirtualizingListBox"/> to exercise the generic base contract: the
/// template decides how a row looks, while <see cref="RealizeItem"/>, <see cref="RecycleItem"/> and
/// <see cref="BindRecycledItem"/> own creation, release and rebinding. The counters below are the evidence the page
/// shows — after the first window is built, scrolling only rebinds pooled rows.
/// </para>
/// <para>
/// Selection lives here because row state belongs to the item, not to a container: the row model raises
/// <see cref="VirtualLogRow.RowBrush"/> when <see cref="VirtualLogRow.IsSelected"/> changes, so a recycled row paints
/// the right state as soon as it is rebound.
/// </para>
/// </remarks>
public sealed class VirtualLogPanel : VirtualizingStackPanel
{
    private int _selectedIndex = -1;

    /// <summary>Creates the panel and makes it accept pointer selection.</summary>
    public VirtualLogPanel()
    {
        IsHitTestVisible = true;
        Focusable = true;
    }

    /// <summary>How many row visuals were created from scratch (not taken from the pool).</summary>
    public int CreatedCount { get; private set; }

    /// <summary>How many pooled row visuals were rebound to a new source index.</summary>
    public int ReusedCount { get; private set; }

    /// <summary>How many row visuals left the window and entered the pool.</summary>
    public int RecycledCount { get; private set; }

    /// <summary>How many times <see cref="RealizeItem"/> ran, fresh or reused.</summary>
    public int RealizedPasses { get; private set; }

    /// <summary>The selected source index, or -1 when nothing is selected.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>The selected row, or <see langword="null"/>.</summary>
    public VirtualLogRow? SelectedRow
        => _selectedIndex >= 0 && _selectedIndex < ItemCount ? GetItemAt(_selectedIndex) as VirtualLogRow : null;

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised after the realized window settled, so a page can read the new first/realized indices.</summary>
    /// <remarks><see cref="ScrollViewer.ScrollChanged"/> fires while the offset moves but before this panel has
    /// arranged the new window; a metrics readout driven by that event alone lags one layout behind.</remarks>
    public event EventHandler? WindowArranged;

    /// <summary>Selects the row at <paramref name="index"/>, clearing the previous row's state.</summary>
    /// <param name="index">The zero-based source index.</param>
    /// <returns><see langword="true"/> when the index is inside the source.</returns>
    public bool SelectIndex(int index)
    {
        if (index < 0 || index >= ItemCount)
        {
            return false;
        }

        if (index == _selectedIndex)
        {
            return true;
        }

        if (GetItemAt(_selectedIndex) is VirtualLogRow previous)
        {
            previous.IsSelected = false;
        }

        _selectedIndex = index;
        if (GetItemAt(index) is VirtualLogRow current)
        {
            current.IsSelected = true;
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    protected override VisualElement CreateItemVisual(object? item)
    {
        CreatedCount++;
        return base.CreateItemVisual(item);
    }

    /// <inheritdoc />
    protected override VisualElement RealizeItem(int index)
    {
        RealizedPasses++;
        return base.RealizeItem(index);
    }

    /// <inheritdoc />
    protected override void RecycleItem(int index, VisualElement element)
    {
        RecycledCount++;
        base.RecycleItem(index, element);

        // 池里的行不该继续引用它曾显示的那一条；重绑时基类会重新写 DataContext。
        element.ClearValue(VisualElement.DataContextProperty);
    }

    /// <inheritdoc />
    protected override void BindRecycledItem(VisualElement element, int index)
    {
        ReusedCount++;
        base.BindRecycledItem(element, index);
    }

    /// <inheritdoc />
    protected override void RealizeItems()
    {
        base.RealizeItems();
        if (_selectedIndex >= 0)
        {
            _selectedIndex = -1;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        WindowArranged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        var index = IndexAt(args.Position);
        if (index < 0)
        {
            return;
        }

        Focus();
        SelectIndex(index);
        args.Handled = true;
    }

    private int IndexAt(PointF surfacePoint)
    {
        if (FirstRealizedIndex < 0)
        {
            return -1;
        }

        for (var offset = 0; offset < Items.Count; offset++)
        {
            if (Items[offset].AbsoluteBounds.Contains(surfacePoint))
            {
                return FirstRealizedIndex + offset;
            }
        }

        return -1;
    }
}

using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Demo.Views;

/// <summary>
/// An order-entry workspace made of live WinForms controls: a <see cref="DataGridView"/> over an editable
/// <see cref="DataTable"/>, a <see cref="TreeView"/>, field editors, a <see cref="ToolStrip"/> and a
/// <see cref="StatusStrip"/>. The visual Grid measures and arranges every island; this file only supplies data
/// and events.
/// </summary>
public sealed partial class NativeWorkspaceCard : ThemedStackPanel
{
    private static readonly string[] Statuses = ["On track", "At risk", "Blocked", "Shipped"];

    private readonly DataTable _orders = new();
    private bool _detached;
    private bool _syncing;
    private int _nextOrder = 6;
    private ToolStripStatusLabel _countLabel = null!;
    private ToolStripStatusLabel _selectionLabel = null!;

    /// <summary>Creates the view, the sample table, the tool strip and the status strip.</summary>
    public NativeWorkspaceCard()
    {
        InitializeComponent();

        // 卡片离开预览树时先把事件挡掉：拆树会让 DataGridView 清空选择并再次触发 SelectionChanged。
        ParentChanged += (_, _) =>
        {
            if (ParentElement is null)
            {
                _detached = true;
            }
        };

        BuildOrders();
        BuildStatusStrip();
        BindGrid();
        BuildTree();
        BuildToolBar();
        statusField.Native.Items.AddRange(Statuses);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _detached = true;
            if (ordersGrid.Native is { IsDisposed: false } grid)
            {
                grid.SelectionChanged -= OnGridSelectionChanged;
                grid.CellValueChanged -= OnGridCellValueChanged;
            }
        }

        base.Dispose(disposing);
    }

    private void BuildOrders()
    {
        _orders.Columns.Add("Order", typeof(string));
        _orders.Columns.Add("Customer", typeof(string));
        _orders.Columns.Add("Qty", typeof(int));
        _orders.Columns.Add("Due", typeof(DateTime));
        _orders.Columns.Add("Status", typeof(string));

        _orders.Rows.Add("OPS-2401", "Checkout revamp", 24, DateTime.Today.AddDays(3), "On track");
        _orders.Rows.Add("OPS-2402", "Payout ledger", 8, DateTime.Today.AddDays(9), "At risk");
        _orders.Rows.Add("OPS-2403", "Search ranking", 42, DateTime.Today.AddDays(1), "Blocked");
        _orders.Rows.Add("OPS-2404", "Mobile shell", 16, DateTime.Today.AddDays(14), "Shipped");
        _orders.Rows.Add("OPS-2405", "Billing rules", 31, DateTime.Today.AddDays(6), "On track");
    }

    private void BindGrid()
    {
        // DataTable 的 DefaultView 支持表头排序与就地编辑；DataGridView 绑上去就是一座普通原生岛。
        ordersGrid.Native.DataSource = _orders;
        ordersGrid.Native.SelectionChanged += OnGridSelectionChanged;
        ordersGrid.Native.CellValueChanged += OnGridCellValueChanged;
    }

    private void BuildTree()
    {
        var workspace = navTree.Native.Nodes.Add("Workspace");
        workspace.Nodes.Add("All orders");
        workspace.Nodes.Add("Due this week");
        workspace.Nodes.Add("At risk");
        navTree.Native.ExpandAll();
        if (workspace.Nodes[0] is { } first)
        {
            navTree.Native.SelectedNode = first;
        }
    }

    private void BuildToolBar()
    {
        var add = new ToolStripButton("New order") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        add.Click += (_, _) => AddOrder();

        var remove = new ToolStripButton("Delete row") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        remove.Click += (_, _) => DeleteCurrentRow();

        var apply = new ToolStripButton("Apply fields") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        apply.Click += (_, _) => ApplyFields();

        toolStrip.Native.GripStyle = ToolStripGripStyle.Hidden;
        toolStrip.Native.Items.AddRange([add, remove, new ToolStripSeparator(), apply]);
    }

    private void BuildStatusStrip()
    {
        _countLabel = new ToolStripStatusLabel { BackColor = System.Drawing.Color.Transparent };
        _selectionLabel = new ToolStripStatusLabel
        {
            BackColor = System.Drawing.Color.Transparent,
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        statusStrip.Native.SizingGrip = false;
        statusStrip.Native.Items.AddRange([_countLabel, _selectionLabel]);
        UpdateStatus("Ready");
    }

    private void OnNavSelect(object? sender, TreeViewEventArgs e)
    {
        if (_detached || navTree.Native is not { IsDisposed: false } || ordersGrid.Native is not { IsDisposed: false })
        {
            return;
        }

        var filter = e.Node?.Text switch
        {
            "Due this week" => $"Due <= #{DateTime.Today.AddDays(7):MM/dd/yyyy}#",
            "At risk" => "Status = 'At risk'",
            _ => string.Empty,
        };

        // DataGridView 绑的是表的 DefaultView，改 RowFilter 后 Refresh 就换了一批可见行。
        _orders.DefaultView.RowFilter = filter;
        ordersGrid.Native.Refresh();
        if (ordersGrid.Native.Rows.Count > 0)
        {
            ordersGrid.Native.CurrentCell = ordersGrid.Native.Rows[0].Cells[0];
        }

        UpdateStatus($"{e.Node?.Text}: {_orders.DefaultView.Count} row(s)");
        SyncFieldsFromSelection();
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_detached)
        {
            return;
        }

        SyncFieldsFromSelection();
    }

    private void OnGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_detached)
        {
            return;
        }

        SyncFieldsFromSelection();
    }

    private void OnQtyChanged(object? sender, EventArgs e)
    {
        if (_detached || _syncing || qtyField.Native is not { IsDisposed: false } qty)
        {
            return;
        }

        qtyBar.Value = Math.Clamp((double)qty.Value, 0d, qtyBar.Maximum);
    }

    private void OnDueChanged(object? sender, EventArgs e)
    {
        if (_detached || _syncing || dueField.Native is not { IsDisposed: false } due)
        {
            return;
        }

        UpdateStatus($"Due set to {due.Value:yyyy-MM-dd}");
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        if (_detached || _syncing || statusField.Native is not { IsDisposed: false } status)
        {
            return;
        }

        UpdateStatus($"Status set to {status.SelectedItem}");
    }

    private void OnApplyClick(object? sender, EventArgs e)
    {
        if (!_detached)
        {
            ApplyFields();
        }
    }

    private void ApplyFields()
    {
        if (_detached || CurrentRow() is not { } row)
        {
            if (!_detached)
            {
                UpdateStatus("Select a row first");
            }

            return;
        }

        if (qtyField.Native is not { IsDisposed: false } qty
            || dueField.Native is not { IsDisposed: false } due
            || statusField.Native is not { IsDisposed: false } status)
        {
            return;
        }

        row["Qty"] = (int)qty.Value;
        row["Due"] = due.Value;
        row["Status"] = status.SelectedItem?.ToString() ?? Statuses[0];
        SyncFieldsFromSelection();
        UpdateStatus($"Applied fields to {row["Order"]}");
    }

    private void AddOrder()
    {
        var number = _nextOrder++;
        var row = _orders.NewRow();
        row["Order"] = $"OPS-24{number:00}";
        row["Customer"] = "New work item";
        row["Qty"] = 10;
        row["Due"] = DateTime.Today.AddDays(7);
        row["Status"] = Statuses[0];
        _orders.Rows.Add(row);
        UpdateStatus($"Added OPS-24{number:00}");
        SyncFieldsFromSelection();
    }

    private void DeleteCurrentRow()
    {
        if (CurrentRow() is not { } row)
        {
            UpdateStatus("Select a row first");
            return;
        }

        var order = row["Order"];
        row.Delete();
        UpdateStatus($"Deleted {order}");
        SyncFieldsFromSelection();
    }

    private DataRow? CurrentRow()
    {
        if (_detached || ordersGrid.Native is not { IsDisposed: false } grid)
        {
            return null;
        }

        // SelectionChanged 触发时 CurrentRow 还是旧行，SelectedRows 已经换成新行。
        if (grid.SelectedRows.Count > 0 && grid.SelectedRows[0].DataBoundItem is DataRowView selected)
        {
            return selected.Row;
        }

        return grid.CurrentRow?.DataBoundItem is DataRowView current ? current.Row : null;
    }

    private void SyncFieldsFromSelection()
    {
        if (_detached || CurrentRow() is not { } row)
        {
            if (!_detached)
            {
                detailTitle.Text = "No row selected";
                detailLine.Text = "Pick a row in the native grid.";
                qtyBar.Value = 0;
                UpdateStatus("No row selected");
            }

            return;
        }

        var qty = Convert.ToDecimal(row["Qty"]);
        var due = Convert.ToDateTime(row["Due"]);
        var status = row["Status"]?.ToString() ?? Statuses[0];
        var order = row["Order"]?.ToString() ?? string.Empty;
        var customer = row["Customer"]?.ToString() ?? string.Empty;

        // 拆卸过程中原生包装可能先清空 Native；字段缺失就只跳过回填，不把异常抛给布局/绘制。
        if (qtyField.Native is not { IsDisposed: false } qtyNative
            || dueField.Native is not { IsDisposed: false } dueNative
            || statusField.Native is not { IsDisposed: false } statusNative)
        {
            return;
        }

        _syncing = true;
        try
        {
            qtyNative.Value = Math.Clamp(qty, qtyNative.Minimum, qtyNative.Maximum);
            dueNative.Value = due;
            statusNative.SelectedItem = status;
        }
        finally
        {
            _syncing = false;
        }

        qtyBar.Value = Math.Clamp((double)qty, 0d, qtyBar.Maximum);
        detailTitle.Text = $"{order}  ·  {customer}";
        detailLine.Text = $"Due {due:yyyy-MM-dd}  ·  {status}";
        UpdateStatus($"Selected {order}");
    }

    private void UpdateStatus(string message)
    {
        if (_detached || _countLabel is null || _selectionLabel is null
            || _countLabel.IsDisposed || _selectionLabel.IsDisposed)
        {
            return;
        }

        _countLabel.Text = $"{_orders.DefaultView.Count} orders";
        _selectionLabel.Text = message;
    }
}

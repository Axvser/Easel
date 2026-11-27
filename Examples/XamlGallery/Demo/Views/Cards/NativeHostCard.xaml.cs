using System.Windows.Forms;

namespace Demo.Views;

/// <summary>
/// The code-behind native path: a configured WinForms <see cref="ListView"/> is assigned to
/// <see cref="Easel.Visuals.Controls.NativeControlHost.Child"/> and occupies one rectangle in the visual layout.
/// </summary>
public sealed partial class NativeHostCard : ThemedStackPanel
{
    private readonly ListView _builds = new()
    {
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HideSelection = false,
        BorderStyle = BorderStyle.None,
    };

    private int _nextBuild = 12;
    private bool _detached;

    /// <summary>Creates the view, configures the list and hands it to the host.</summary>
    public NativeHostCard()
    {
        InitializeComponent();

        _builds.Columns.Add("Build", 150);
        _builds.Columns.Add("Result", 100);
        _builds.Columns.Add("Duration", 110);
        _builds.Columns.Add("Started", 140);
        _builds.SelectedIndexChanged += OnBuildSelected;

        AddBuild("passed", 42);
        AddBuild("passed", 39);
        AddBuild("failed", 118);

        host.Child = _builds;
        UpdateStatus("The ListView is created and configured in code, then assigned to NativeControlHost.Child.");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _detached = true;
            _builds.SelectedIndexChanged -= OnBuildSelected;
        }

        base.Dispose(disposing);
    }

    private void AddBuild(string result, int seconds)
    {
        var item = new ListViewItem($"build-{_nextBuild++:0000}");
        item.SubItems.Add(result);
        item.SubItems.Add($"{seconds}s");
        item.SubItems.Add(System.DateTime.Now.ToString("HH:mm:ss"));
        _builds.Items.Add(item);
        item.Selected = true;
        item.EnsureVisible();
    }

    private void OnBuildSelected(object? sender, System.EventArgs e)
    {
        if (_detached)
        {
            return;
        }

        if (_builds.SelectedItems.Count > 0)
        {
            UpdateStatus($"Native selection: {_builds.SelectedItems[0].Text} finished with {_builds.SelectedItems[0].SubItems[1].Text}.");
        }
    }

    private void OnAddClick(object? sender, System.EventArgs e)
    {
        if (_detached)
        {
            return;
        }

        AddBuild("passed", 36 + (_nextBuild % 5) * 7);
        UpdateStatus($"Visual Button added {_builds.Items[^1].Text} to the native ListView.");
    }

    private void OnClearClick(object? sender, System.EventArgs e)
    {
        if (_detached)
        {
            return;
        }

        _builds.Items.Clear();
        UpdateStatus("Visual Button cleared the native ListView.");
    }

    private void UpdateStatus(string message)
    {
        if (!_detached)
        {
            hostStatus.Text = message;
        }
    }
}

using System.ComponentModel;
using System.Drawing;
using Easel;
using Easel.Controls;
using Easel.Visuals;
using Easel.Visuals.Controls;
using IndustrialWorkbench.ViewModels;
using WinForms = System.Windows.Forms;

namespace IndustrialWorkbench.Views;

/// <summary>The detail page: a large live instance, a live property readout and a copyable XAML snippet.</summary>
public sealed partial class ControlDetailView : UserControl
{
    private readonly List<TextBlock> _valueLabels = [];
    private ControlInfo? _info;
    private WorkbenchViewModel? _model;

    /// <summary>Creates the page and its generated initializer.</summary>
    public ControlDetailView() => InitializeComponent();

    /// <summary>Swaps the page to <paramref name="info"/> and binds it to <paramref name="model"/>.</summary>
    public void ShowControl(ControlInfo info, WorkbenchViewModel model)
    {
        AttachModel(model);
        _info = info;

        TitleText.Text = info.Title;
        GroupBadgeText.Text = info.Group;
        SummaryText.Text = info.Summary;
        UsageText.Text = info.Usage;
        CopyHint.Text = string.Empty;

        var previous = DetailHost.Content as VisualElement;
        DetailHost.DataContext = model;
        DetailHost.Content = info.CreateView();
        previous?.Dispose();

        BuildReadout();
    }

    // 换模型才重订阅；同一模型内切页不需要来回摘挂。
    private void AttachModel(WorkbenchViewModel model)
    {
        if (ReferenceEquals(_model, model))
        {
            return;
        }

        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        model.PropertyChanged += OnModelChanged;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => UpdateReadout();

    private void BuildReadout()
    {
        if (_info is null || _model is null)
        {
            return;
        }

        ReadoutHost.Children.Clear();
        _valueLabels.Clear();
        foreach (var row in _info.Readout(_model))
        {
            var grid = new Grid { Margin = new Thickness(0f, 0f, 0f, 6f) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = row.Name,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x74, 0x88, 0x9C)),
            };
            var value = new TextBlock
            {
                Text = row.Value,
                FontSize = 12,
                FontFamily = "Consolas",
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0)),
                HorizontalAlignment = Easel.Controls.HorizontalAlignment.Right,
            };
            Grid.SetColumn(value, 1);
            grid.Add(name);
            grid.Add(value);
            ReadoutHost.Add(grid);
            _valueLabels.Add(value);
        }
    }

    private void UpdateReadout()
    {
        if (_info is null || _model is null)
        {
            return;
        }

        var rows = _info.Readout(_model);
        if (rows.Count != _valueLabels.Count)
        {
            BuildReadout();
            return;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            _valueLabels[index].Text = rows[index].Value;
        }
    }

    private void OnCopyClick(object? sender, EventArgs e)
    {
        if (_info is null)
        {
            return;
        }

        try
        {
            WinForms.Clipboard.SetText(_info.Usage);
            CopyHint.Text = "已复制";
        }
        catch (Exception)
        {
            CopyHint.Text = "复制失败";
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        base.Dispose(disposing);
    }
}

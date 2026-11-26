using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Easel;
using Demo.Mvvm;

namespace Demo;

/// <summary>One row shown by the data-bound controls in the gallery.</summary>
public sealed class SampleRow
{
    /// <summary>A short work-item code such as <c>OPS-1042</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The display name of the row.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A numeric value the data controls render.</summary>
    public double Value { get; set; }

    /// <summary>Whether the row is enabled; bound by the check-box column.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The owning team.</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>The person accountable for the row.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>The workflow state shown in the badge column.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The completion percentage shown in the progress column.</summary>
    public double Progress { get; set; }

    /// <summary>The last update date.</summary>
    public DateTime Updated { get; set; }
}

/// <summary>One node of the small hierarchical sample shown by the tree view.</summary>
public sealed class GalleryNode
{
    /// <summary>The node label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The child nodes; the tree view discovers them by this name.</summary>
    public ObservableCollection<GalleryNode> Children { get; } = [];
}

/// <summary>
/// The data context behind every gallery section: collections, a command, scalar values and one source-level
/// validation error used by the binding section.
/// </summary>
public sealed class GalleryModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);
    private string? _optionalName = "Ada Lovelace";
    private double _amount = 42.5;
    private DateTime? _date = DateTime.Today;
    private int _saveCount;
    private string _collectionState = "Unsorted · all rows";
    private bool _sortedDescending;
    private bool _filterEnabled;

    private static readonly string[] Departments =
        ["Platform", "Design", "Revenue", "Research", "Support", "Finance"];

    private static readonly string[] Owners =
        ["Ada Lovelace", "Grace Hopper", "Alan Turing", "Katherine Johnson", "Edsger Dijkstra", "Margaret Hamilton", "Linus Torvalds", "Barbara Liskov"];

    private static readonly string[] ProjectNames =
        ["Checkout revamp", "Payout ledger", "Search ranking", "Mobile shell", "Billing rules", "Import pipeline", "Design tokens", "Audit trail", "Route planner", "Invoice sync", "Session replay", "Fraud signals"];

    private static readonly string[] Statuses = ["On track", "At risk", "Blocked", "Shipped"];

    /// <summary>Creates the sample collections and the save command.</summary>
    public GalleryModel()
    {
        Rows =
        [
            Row("OPS-1042", ProjectNames[0], Departments[0], Owners[0], "On track", 48250, 72, 1),
            Row("OPS-1043", ProjectNames[1], Departments[2], Owners[1], "At risk", 31900, 41, 2),
            Row("OPS-1044", ProjectNames[2], Departments[3], Owners[2], "Blocked", 72500, 18, 3),
            Row("OPS-1045", ProjectNames[3], Departments[1], Owners[3], "Shipped", 95800, 100, 5),
            Row("OPS-1046", ProjectNames[4], Departments[5], Owners[4], "On track", 12750, 64, 6),
            Row("OPS-1047", ProjectNames[5], Departments[0], Owners[5], "At risk", 54300, 37, 8),
            Row("OPS-1048", ProjectNames[6], Departments[1], Owners[6], "On track", 18800, 88, 9),
            Row("OPS-1049", ProjectNames[7], Departments[4], Owners[7], "Shipped", 60400, 100, 11),
            Row("OPS-1050", ProjectNames[8], Departments[3], Owners[0], "Blocked", 29100, 12, 13),
            Row("OPS-1051", ProjectNames[9], Departments[5], Owners[2], "On track", 87400, 57, 15),
            Row("OPS-1052", ProjectNames[10], Departments[0], Owners[4], "At risk", 41600, 29, 18),
            Row("OPS-1053", ProjectNames[11], Departments[2], Owners[6], "On track", 23300, 76, 21),
        ];

        TreeNodes =
        [
            new GalleryNode
            {
                Name = "Workspace",
                Children =
                {
                    new GalleryNode
                    {
                        Name = "Surface",
                        Children =
                        {
                            new GalleryNode { Name = "Canvas" },
                            new GalleryNode { Name = "Minimap" },
                            new GalleryNode { Name = "Viewport" },
                        },
                    },
                    new GalleryNode
                    {
                        Name = "Controls",
                        Children =
                        {
                            new GalleryNode { Name = "Buttons" },
                            new GalleryNode { Name = "Selectors" },
                            new GalleryNode
                            {
                                Name = "Data",
                                Children =
                                {
                                    new GalleryNode { Name = "DataGrid" },
                                    new GalleryNode { Name = "TreeView" },
                                },
                            },
                        },
                    },
                    new GalleryNode
                    {
                        Name = "Diagnostics",
                        Children =
                        {
                            new GalleryNode { Name = "Paint log" },
                            new GalleryNode { Name = "Focus chain" },
                        },
                    },
                },
            },
        ];

        SaveCommand = new RelayCommand(_ => SaveCount++);
        RowsView = new CollectionView(Rows);

        var random = new Random(20261124);
        for (var index = 0; index < 2048; index++)
        {
            var status =
                index % 13 == 0 ? "Blocked"
                : index % 7 == 0 ? "At risk"
                : index % 5 == 0 ? "Shipped"
                : "On track";
            var progress = status == "Shipped" ? 100d : random.Next(8, 96);
            TableRows.Add(new SampleRow
            {
                Code = $"OPS-{1000 + index}",
                Name = ProjectNames[index % ProjectNames.Length],
                Department = Departments[index % Departments.Length],
                Owner = Owners[(index / 3) % Owners.Length],
                Status = status,
                Value = Math.Round(random.Next(4, 240) * 1000d),
                Progress = progress,
                Enabled = index % 11 != 0,
                Updated = DateTime.Today.AddDays(-random.Next(0, 90)),
            });
        }

        InboxRows =
        [
            Row("OPS-2101", "Triage new incidents", "Support", Owners[5], "At risk", 14200, 34, 0),
            Row("OPS-2102", "Refine import rules", "Platform", Owners[0], "On track", 26800, 61, 0),
            Row("OPS-2103", "Verify payout batch", "Finance", Owners[3], "Blocked", 38700, 9, 1),
            Row("OPS-2104", "Ship audit export", "Revenue", Owners[2], "Shipped", 52100, 100, 2),
            Row("OPS-2105", "Review search weights", "Research", Owners[4], "On track", 19400, 48, 3),
        ];

        DoneRows =
        [
            Row("OPS-2201", "Localize billing copy", "Design", Owners[1], "Shipped", 33100, 100, 4),
            Row("OPS-2202", "Cache the lookup table", "Platform", Owners[6], "Shipped", 44700, 100, 5),
            Row("OPS-2203", "Retire legacy importer", "Support", Owners[7], "On track", 22800, 82, 6),
        ];

        for (var index = 0; index < 5000; index++)
        {
            var status = Statuses[index % Statuses.Length];
            VirtualRows.Add(new SampleRow
            {
                Code = $"V-{index + 1:0000}",
                Name = $"Virtual row {index + 1:0000}",
                Department = Departments[index % Departments.Length],
                Owner = Owners[index % Owners.Length],
                Status = status,
                Value = (index + 1) * 12.5d,
                Progress = status == "Shipped" ? 100d : index % 100,
                Enabled = index % 7 != 0,
                Updated = DateTime.Today.AddDays(-(index % 60)),
            });
        }
    }

    private static SampleRow Row(
        string code,
        string name,
        string department,
        string owner,
        string status,
        double value,
        double progress,
        int daysAgo)
        => new()
        {
            Code = code,
            Name = name,
            Department = department,
            Owner = owner,
            Status = status,
            Value = value,
            Progress = progress,
            Enabled = status != "Blocked",
            Updated = DateTime.Today.AddDays(-daysAgo),
        };

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>The rows bound by the list, grid and combo-box examples.</summary>
    public ObservableCollection<SampleRow> Rows { get; }

    /// <summary>The same rows under the name the indexer binding examples use.</summary>
    public ObservableCollection<SampleRow> Items => Rows;

    /// <summary>A sorted and filtered view over <see cref="Rows"/>; the data section binds it directly.</summary>
    public CollectionView RowsView { get; }

    /// <summary>Two thousand rows rendered by the data table; only the visible ones are painted.</summary>
    public ObservableCollection<SampleRow> TableRows { get; } = [];

    /// <summary>The left list of the drag board.</summary>
    public ObservableCollection<SampleRow> InboxRows { get; } = [];

    /// <summary>The right list of the drag board.</summary>
    public ObservableCollection<SampleRow> DoneRows { get; } = [];

    /// <summary>Five thousand rows used by the virtualized list.</summary>
    public ObservableCollection<SampleRow> VirtualRows { get; } = [];

    /// <summary>The current sort and filter state of <see cref="RowsView"/>.</summary>
    public string CollectionState
    {
        get => _collectionState;
        private set => SetField(ref _collectionState, value);
    }

    /// <summary>Sorts the view by name, toggling direction on each call.</summary>
    public void SortRows()
    {
        _sortedDescending = !_sortedDescending;
        RowsView.SortDescriptions.Clear();
        RowsView.SortDescriptions.Add(new Easel.SortDescription(
            nameof(SampleRow.Name),
            _sortedDescending ? ListSortDirection.Descending : ListSortDirection.Ascending));
        UpdateCollectionState();
    }

    /// <summary>Filters the view to enabled rows, or clears the filter.</summary>
    public void FilterRows()
    {
        _filterEnabled = !_filterEnabled;
        RowsView.Filter = _filterEnabled
            ? static item => item is SampleRow { Enabled: true }
            : null;
        UpdateCollectionState();
    }

    private void UpdateCollectionState()
        => CollectionState =
        $"{(_sortedDescending ? "Name ↓" : "Name ↑")} · {(_filterEnabled ? "enabled only" : "all rows")} · {RowsView.Count} shown";

    /// <summary>The hierarchical sample bound by the tree view.</summary>
    public ObservableCollection<GalleryNode> TreeNodes { get; }

    /// <summary>The command invoked by the button and input-binding examples.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>How many times <see cref="SaveCommand"/> has run.</summary>
    public int SaveCount
    {
        get => _saveCount;
        private set => SetField(ref _saveCount, value);
    }

    /// <summary>A nullable date bound by the calendar, date picker and string-format examples.</summary>
    public DateTime? Date
    {
        get => _date;
        set => SetField(ref _date, value);
    }

    /// <summary>A numeric value bound with string formatting.</summary>
    public double Amount
    {
        get => _amount;
        set => SetField(ref _amount, value);
    }

    /// <summary>A name that must not be blank; the binding section shows the source-level error.</summary>
    public string? OptionalName
    {
        get => _optionalName;
        set
        {
            if (!SetField(ref _optionalName, value))
            {
                return;
            }

            ValidateOptionalName();
        }
    }

    /// <inheritdoc />
    public bool HasErrors => _errors.Count > 0;

    /// <inheritdoc />
    public IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName is null || !_errors.TryGetValue(propertyName, out var errors))
        {
            return Array.Empty<string>();
        }

        return errors;
    }

    private void ValidateOptionalName()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(OptionalName))
        {
            errors.Add("A display name is required.");
        }

        if (errors.Count == 0)
        {
            _errors.Remove(nameof(OptionalName));
        }
        else
        {
            _errors[nameof(OptionalName)] = errors;
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(OptionalName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

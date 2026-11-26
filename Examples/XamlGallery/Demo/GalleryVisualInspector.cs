using XamlBrush = Easel.Brush;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Demo;

/// <summary>Builds an editor row for each editable dependency property of the selected visual element.</summary>
internal sealed class GalleryVisualInspector
{
    private static readonly HashSet<string> Hidden = new(StringComparer.Ordinal)
    {
        "DataContext", "Style", "Template", "ItemsSource", "ItemFactory", "Content", "Child", "Font", "Name",
        "ChromeBackground", "ChromeBorderBrush", "ChromeBorderThickness", "ChromeBorderWidth",
    };

    private static readonly Dictionary<Type, DependencyProperty[]> EditableProperties = [];

    private readonly VC.StackPanel _stack = new() { Spacing = 6 };
    private readonly ResourceDictionary _resources;
    private readonly VC.TextBlock _caption;
    private readonly List<RowSlot> _slots = [];

    internal GalleryVisualInspector(ResourceDictionary resources)
    {
        _resources = resources;
        _caption = Caption(string.Empty);
        _stack.Add(_caption);
    }

    private XamlBrush Brush(string key)
        => _resources.TryGetValue(key, out var value) && value is XamlBrush brush
            ? brush
            : new SolidColorBrush(Theme.Current.MutedForeground);

    /// <summary>The visual root of the inspector.</summary>
    public VisualElement Root => _stack;

    /// <summary>Rebuilds the rows for <paramref name="target"/>.</summary>
    /// <param name="target">The element to inspect, or <see langword="null"/> to show the empty state.</param>
    public void SetTarget(VisualElement? target)
    {
        if (target is null)
        {
            _caption.Text = "Select a card to edit its dependency properties.";
            HideSlots(0);
            return;
        }

        _caption.Text = target.GetType().Name;
        var index = 0;
        foreach (var property in GetEditableProperties(target.GetType()))
        {
            var slot = index < _slots.Count ? _slots[index] : CreateSlot();
            slot.Bind(target, property, ((IWinformsObject)target).GetValue(property));
            index++;
        }

        HideSlots(index);
    }

    private RowSlot CreateSlot()
    {
        var slot = new RowSlot(Brush("Muted"));
        _slots.Add(slot);
        _stack.Add(slot.Root);
        return slot;
    }

    private void HideSlots(int from)
    {
        for (var index = from; index < _slots.Count; index++)
        {
            _slots[index].Hide();
        }
    }

    private static DependencyProperty[] GetEditableProperties(Type targetType)
    {
        if (EditableProperties.TryGetValue(targetType, out var cached))
        {
            return cached;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var editable = new List<DependencyProperty>();
        for (var type = targetType; type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType != typeof(DependencyProperty) || field.GetValue(null) is not DependencyProperty property)
                {
                    continue;
                }

                if (seen.Add(property.Name) && !Hidden.Contains(property.Name) && IsEditable(property))
                {
                    editable.Add(property);
                }
            }
        }

        var result = editable.ToArray();
        EditableProperties[targetType] = result;
        return result;
    }

    private static bool IsEditable(DependencyProperty property)
    {
        var type = property.PropertyType;
        return type == typeof(bool)
               || type == typeof(string)
               || type.IsEnum
               || type == typeof(int)
               || type == typeof(double)
               || type == typeof(float)
               || type == typeof(Color);
    }

    private static string FormatValue(Type type, object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (type == typeof(double) && value is double number && double.IsNaN(number))
        {
            return "Auto";
        }

        return value is IFormattable formattable
            ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            : value.ToString() ?? string.Empty;
    }

    private static object? ParseValue(Type type, string text)
    {
        if (type == typeof(string))
        {
            return text;
        }

        if (type == typeof(Color))
        {
            return XamlRuntime.ParseColor(text);
        }

        var trimmed = text.Trim();
        if (type == typeof(double))
        {
            return trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? double.NaN : double.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (type == typeof(float))
        {
            return float.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (type == typeof(int))
        {
            return int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    private VC.TextBlock Caption(string text)
        => new()
        {
            Text = text,
            Foreground = Brush("Muted"),
            Wrap = true,
        };

    private enum EditorKind
    {
        Text,
        Boolean,
        Enum,
    }

    // 行按序号复用：换页只改标签与编辑器状态；同一种编辑器在行内缓存，类型顺序变化时不重建。
    private sealed class RowSlot
    {
        private readonly Dictionary<EditorKind, VisualElement> _editors = [];
        private readonly VC.TextBlock _label;
        private readonly VC.StackPanel _row;
        private VisualElement? _activeEditor;
        private Type? _enumType;
        private string[] _enumNames = [];
        private VisualElement? _target;
        private DependencyProperty? _property;
        private bool _syncing;

        public RowSlot(XamlBrush labelBrush)
        {
            _label = new VC.TextBlock
            {
                Text = string.Empty,
                Foreground = labelBrush,
                Width = 150,
                VerticalAlignment = Easel.Controls.VerticalAlignment.Center,
            };
            _row = new VC.StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            _row.Add(_label);
        }

        public VisualElement Root => _row;

        public void Bind(VisualElement target, DependencyProperty property, object? value)
        {
            var kind = KindOf(property);
            var editor = SelectEditor(kind);

            _target = target;
            _property = property;
            _label.Text = property.Name;
            _row.Visibility = Easel.Controls.Visibility.Visible;

            _syncing = true;
            try
            {
                switch (kind)
                {
                    case EditorKind.Boolean:
                        ((VC.CheckBox)editor).IsChecked = value is true;
                        break;
                    case EditorKind.Enum:
                        BindEnum(editor, property, value);
                        break;
                    default:
                        ((VC.TextBox)editor).Text = FormatValue(property.PropertyType, value);
                        break;
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        public void Hide()
        {
            _row.Visibility = Easel.Controls.Visibility.Collapsed;
            _target = null;
            _property = null;
        }

        private VisualElement SelectEditor(EditorKind kind)
        {
            if (_activeEditor is not null)
            {
                _activeEditor.Visibility = Easel.Controls.Visibility.Collapsed;
            }

            if (!_editors.TryGetValue(kind, out var editor))
            {
                editor = CreateEditor(kind);
                _editors[kind] = editor;
                _row.Add(editor);
            }

            editor.Visibility = Easel.Controls.Visibility.Visible;
            _activeEditor = editor;
            return editor;
        }

        private void BindEnum(VisualElement editor, DependencyProperty property, object? value)
        {
            var combo = (VC.ComboBox)editor;
            if (_enumType != property.PropertyType)
            {
                _enumType = property.PropertyType;
                _enumNames = Enum.GetNames(property.PropertyType);
                combo.Items.Clear();
                foreach (var name in _enumNames)
                {
                    combo.Items.Add(name);
                }
            }

            combo.SelectedIndex = Array.IndexOf(_enumNames, value?.ToString());
        }

        private VisualElement CreateEditor(EditorKind kind)
        {
            switch (kind)
            {
                case EditorKind.Boolean:
                {
                    var check = new VC.CheckBox();
                    check.PropertyChanged += (_, e) =>
                    {
                        if (_syncing || _target is not { } target || _property is not { } property
                            || !ReferenceEquals(e.Property, VC.CheckBox.IsCheckedProperty))
                        {
                            return;
                        }

                        target.SetValue(property, check.IsChecked == true);
                    };
                    return check;
                }
                case EditorKind.Enum:
                {
                    var combo = new VC.ComboBox { Width = 140 };
                    _enumType = null;
                    combo.SelectionChanged += (_, _) =>
                    {
                        if (_syncing || _target is not { } target || _property is not { } property)
                        {
                            return;
                        }

                        if (combo.SelectedItem is string text
                            && Enum.TryParse(property.PropertyType, text, out var parsed))
                        {
                            target.SetValue(property, parsed);
                        }
                    };
                    return combo;
                }
                default:
                {
                    var text = new VC.TextBox { Width = 140 };
                    text.PropertyChanged += (_, e) =>
                    {
                        if (_syncing || _target is not { } target || _property is not { } property
                            || !ReferenceEquals(e.Property, VC.TextBox.TextProperty))
                        {
                            return;
                        }

                        try
                        {
                            var parsed = ParseValue(property.PropertyType, text.Text);
                            if (parsed is not null)
                            {
                                target.SetValue(property, parsed);
                            }
                        }
                        catch (FormatException)
                        {
                            // 输入中途的非法文本不写回；等用户改到可解析的值再提交。
                        }
                    };
                    return text;
                }
            }
        }

        private static EditorKind KindOf(DependencyProperty property)
            => property.PropertyType == typeof(bool)
                ? EditorKind.Boolean
                : property.PropertyType.IsEnum
                    ? EditorKind.Enum
                    : EditorKind.Text;
    }
}

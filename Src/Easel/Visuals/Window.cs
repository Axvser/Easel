namespace Easel.Visuals;

/// <summary>A XAML-declarable window that hosts one visual tree and runs with <c>Application.Run</c>.</summary>
/// <remarks>
/// <para>
/// Derive a partial class from this type, put <c>x:Class</c> on a <c>Window</c> root and call
/// <c>InitializeComponent</c> in the constructor. The generated initializer assigns the root element to
/// <see cref="Content"/>; the window derives from <see cref="VisualForm"/>, so the usual
/// <c>Application.Run(new MyWindow())</c> bootstrap applies.
/// </para>
/// <para>
/// <see cref="Title"/> maps to the native <c>Form.Text</c> caption. <see cref="Resources"/> is the hosting
/// surface's dictionary, so resources declared in <c>Window.Resources</c> are visible to the whole tree.
/// </para>
/// </remarks>
public class Window : VisualForm
{
    /// <summary>The visual root declared under the window, or <see langword="null"/>.</summary>
    public VisualElement? Content
    {
        get => Root;
        set => Root = value;
    }

    /// <summary>The window caption.</summary>
    public string Title
    {
        get => Text;
        set => Text = value;
    }

    /// <summary>The resource dictionary offered to every element in this window's tree.</summary>
    public ResourceDictionary Resources => Surface.Resources;
}

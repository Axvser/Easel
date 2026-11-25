using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Text editing in the HWND-free visual backend: multi-line layout, word navigation, the undo journal, horizontal
/// scrolling and the native editor island that supplies IME and accessibility.
/// </summary>
[STATestClass]
public sealed class VisualTextEditingTests
{
    [TestMethod]
    public void TextBox_MeasuresMultipleLinesWhenAcceptsReturn()
    {
        var single = new VC.TextBox { Text = "one" };
        var multi = new VC.TextBox { AcceptsReturn = true, Text = "one\ntwo\nthree" };

        var singleSize = single.Measure(new Size(300, 2000));
        var multiSize = multi.Measure(new Size(300, 2000));

        Assert.IsTrue(multiSize.Height > singleSize.Height, "three lines must be taller than one");
        Assert.IsTrue(multiSize.Height >= singleSize.Height + 20, "the extra lines add real height");
    }

    [TestMethod]
    public void TextBox_WrapsLongLinesInsideTheAvailableWidth()
    {
        var wrapped = new VC.TextBox { Wrap = true, Text = new string('W', 120) };
        var unwrapped = new VC.TextBox { Text = new string('W', 120) };

        var wrappedSize = wrapped.Measure(new Size(80, 4000));
        var unwrappedSize = unwrapped.Measure(new Size(80, 4000));

        Assert.IsTrue(wrappedSize.Height > unwrappedSize.Height, "wrapping adds lines");
    }

    [TestMethod]
    public void TextBox_NavigatesByWordWithControl()
    {
        using var surface = CreateSurface(320, 80);
        var text = new VC.TextBox { Width = 280, Height = 34, Text = "hello world" };
        surface.Root = text;
        Layout(surface, 320, 80);
        surface.FocusElement(text);

        surface.ProcessKeyDown(Keys.End);
        Assert.AreEqual(11, text.CaretIndex);

        surface.ProcessKeyDown(Keys.Left, Keys.Control);
        Assert.AreEqual(6, text.CaretIndex, "Ctrl+Left stops at the start of the previous word");

        surface.ProcessKeyDown(Keys.Left, Keys.Control);
        Assert.AreEqual(0, text.CaretIndex);

        surface.ProcessKeyDown(Keys.Right, Keys.Control);
        Assert.AreEqual(6, text.CaretIndex, "Ctrl+Right stops at the start of the next word");

        surface.ProcessKeyDown(Keys.Right, Keys.Control);
        Assert.AreEqual(11, text.CaretIndex);

        surface.ProcessKeyDown(Keys.End, Keys.Control);
        Assert.AreEqual(11, text.CaretIndex);

        surface.ProcessKeyDown(Keys.Home, Keys.Control);
        Assert.AreEqual(0, text.CaretIndex);
    }

    [TestMethod]
    public void TextBox_SelectsAWordOnDoubleClick()
    {
        using var surface = CreateSurface(320, 80);
        var text = new VC.TextBox { Width = 280, Height = 34, Text = "hello world" };
        surface.Root = text;
        Layout(surface, 320, 80);

        var point = text.PointToSurface(new PointF(12f, 16f));
        surface.ProcessPointerDown(point, MouseButtons.Left, clickCount: 2);

        Assert.AreEqual(0, text.SelectionStart);
        Assert.AreEqual(5, text.SelectionLength);
    }

    [TestMethod]
    public void TextBox_UndoesAndRedoesEdits()
    {
        using var surface = CreateSurface(320, 80);
        var text = new VC.TextBox { Width = 280, Height = 34 };
        surface.Root = text;
        Layout(surface, 320, 80);
        surface.FocusElement(text);

        surface.ProcessTextInput("abc");
        Assert.AreEqual("abc", text.Text);

        surface.ProcessKeyDown(Keys.Back);
        Assert.AreEqual("ab", text.Text);

        surface.ProcessKeyDown(Keys.Z, Keys.Control);
        Assert.AreEqual("abc", text.Text, "undo restores the deleted character");

        surface.ProcessKeyDown(Keys.Y, Keys.Control);
        Assert.AreEqual("ab", text.Text, "redo reapplies the edit");

        surface.ProcessKeyDown(Keys.Z, Keys.Control);
        Assert.AreEqual("abc", text.Text);
    }

    [TestMethod]
    public void TextBox_ScrollsHorizontallyToKeepTheCaretVisible()
    {
        using var surface = CreateSurface(200, 80);
        var text = new VC.TextBox { Width = 120, Height = 34, Text = new string('W', 120) };
        surface.Root = text;
        Layout(surface, 200, 80);
        surface.FocusElement(text);

        surface.ProcessKeyDown(Keys.End);

        Assert.IsTrue(text.MaxHorizontalOffset > 0f, "the text is wider than the content rectangle");
        Assert.IsTrue(text.HorizontalOffset > 0f, "the caret at the end must pull the view right");

        surface.ProcessKeyDown(Keys.Home, Keys.Control);
        Assert.AreEqual(0f, text.HorizontalOffset, 0.5f, "going home scrolls back to the left edge");
    }

    [TestMethod]
    public void TextBox_HostsTheNativeEditorOnlyWhileFocused()
    {
        using var surface = CreateSurface(320, 160);
        _ = surface.Handle;
        Application.DoEvents();

        var root = new VC.StackPanel();
        var text = new VC.TextBox { Width = 260, Height = 34, UseNativeEditor = true };
        var button = new VC.Button { Width = 260, Height = 34, Text = "Other" };
        root.Add(text);
        root.Add(button);
        surface.Root = root;
        Layout(surface, 320, 160);

        surface.FocusElement(text);
        Application.DoEvents();

        Assert.IsTrue(text.IsUsingNativeEditor, "a focused field shows the native island");
        Assert.IsTrue(surface.Controls.Contains(text.NativeEditor), "the island is parented to the surface");

        surface.FocusElement(button);
        Application.DoEvents();

        Assert.IsFalse(text.IsUsingNativeEditor, "leaving the field deactivates the island");
        Assert.IsFalse(surface.Controls.Contains(text.NativeEditor), "the island is removed from the surface");
    }

    [TestMethod]
    public void TextBox_NativeEditorSurvivesTheNextLayoutPass()
    {
        using var surface = CreateSurface(320, 160);
        _ = surface.Handle;
        Application.DoEvents();

        var text = new VC.TextBox { Width = 260, Height = 34, UseNativeEditor = true };
        surface.Root = text;
        Layout(surface, 320, 160);

        surface.FocusElement(text);
        Application.DoEvents();
        Assert.IsTrue(surface.Controls.Contains(text.NativeEditor), "the focused field parents the island");

        // 岛的 ArrangeOverride 只在尺寸变化时重跑；下一次整树布局如果把它当成“这趟没安排到”摘掉，
        // 它就会脱离宿主且再也拿不到焦点。回归：聚焦后的任何一趟布局都必须保留这个岛。
        surface.InvalidateSurface();
        Layout(surface, 320, 160);
        Application.DoEvents();

        Assert.IsTrue(text.IsUsingNativeEditor, "the island stays open across a layout pass");
        Assert.IsTrue(surface.Controls.Contains(text.NativeEditor), "the island stays parented to the surface");
    }

    [TestMethod]
    public void TextBox_UseNativeEditorFalseKeepsTheWholeFieldVisual()
    {
        using var surface = CreateSurface(320, 160);
        _ = surface.Handle;
        Application.DoEvents();

        var text = new VC.TextBox { Width = 260, Height = 34, UseNativeEditor = false };
        surface.Root = text;
        Layout(surface, 320, 160);
        surface.FocusElement(text);
        Application.DoEvents();

        Assert.AreSame(text, surface.FocusedElement);
        Assert.IsFalse(text.IsUsingNativeEditor, "the opt-out never opens the native island");
        Assert.IsFalse(surface.Controls.Contains(text.NativeEditor), "no child HWND is parented");

        surface.ProcessTextInput("abc");
        Assert.AreEqual("abc", text.Text, "typing still goes through the visual text path");
        Assert.AreEqual(3, text.CaretIndex);

        text.UseNativeEditor = true;
        Application.DoEvents();

        Assert.IsTrue(text.IsUsingNativeEditor, "turning it back on shows the island while focused");
        Assert.IsTrue(surface.Controls.Contains(text.NativeEditor));
    }

    [TestMethod]
    public void TextBox_NativeEditorIsClippedToTheScrollViewport()
    {
        using var surface = CreateSurface(200, 160);
        _ = surface.Handle;
        Application.DoEvents();

        var scroll = new VC.ScrollViewer { Width = 200, Height = 100 };
        var content = new VC.StackPanel { Width = 150 };
        content.Add(new VC.Border { Height = 190 });
        var text = new VC.TextBox { Width = 150, Height = 34, UseNativeEditor = true };
        content.Add(text);
        scroll.Content = content;
        surface.Root = scroll;
        Layout(surface, 200, 160);

        scroll.VerticalOffset = 100;
        Layout(surface, 200, 160);
        surface.FocusElement(text);
        Application.DoEvents();

        Assert.IsTrue(text.IsUsingNativeEditor, "the field is focused");
        var viewport = scroll.ContentPresenter!.AbsoluteBounds;
        var scale = surface.DeviceDpi / 96f;
        var editor = text.NativeEditor;
        var viewportTop = (int)(viewport.Top * scale);
        var viewportBottom = (int)(viewport.Bottom * scale);

        // 原生岛是独立 HWND；视口外的部分靠窗口区域裁掉，而不是把窗口缩小——
        // 缩小会让编辑器的排版尺寸跟着变，滚动时文字会重折行。
        Assert.IsTrue(editor.Bounds.Top < viewportBottom && editor.Bounds.Bottom > viewportBottom,
            $"the island spans the viewport edge (editor={editor.Bounds}, viewport={viewportTop}..{viewportBottom})");

        var middle = editor.Bounds.Width / 2;
        var visibleY = Math.Min(viewportBottom - 1, editor.Bounds.Bottom - 1) - editor.Bounds.Y;
        var hiddenY = viewportBottom + 2 - editor.Bounds.Y;
        Assert.IsTrue(NativeWindowRegion.Contains(editor, new Point(middle, visibleY)),
            "the part inside the viewport stays visible");
        Assert.IsFalse(NativeWindowRegion.Contains(editor, new Point(middle, hiddenY)),
            "the part below the viewport is clipped");
    }

    [TestMethod]
    public void TextBox_TransparentBackgroundFallsBackToTheNearestOpaqueAncestor()
    {
        using var surface = CreateSurface(220, 120);
        _ = surface.Handle;
        Application.DoEvents();

        var sheet = new VC.Border { Background = new SolidColorBrush(Color.FromArgb(255, 13, 17, 23)) };
        var text = new VC.TextBox { Width = 160, Height = 32, Background = Brush.Empty, UseNativeEditor = true };
        sheet.Add(text);
        surface.Root = sheet;
        Layout(surface, 220, 120);

        surface.FocusElement(text);
        Application.DoEvents();

        Assert.IsTrue(text.IsUsingNativeEditor, "the native island still opens for a transparent visual field");
        Assert.AreEqual(Color.FromArgb(255, 13, 17, 23), text.NativeEditor.BackColor,
            "the native TextBox gets the nearest opaque ancestor colour, never transparent");
    }

    [TestMethod]
    public void TypingSurvivesAnOutOfRangeSelection()
    {
        using var surface = new VisualHost { Size = new Size(220, 60) };
        using var box = new VC.TextBox { Text = "abc", Width = 180, Height = 30, UseNativeEditor = false };
        surface.Root = box;
        Layout(surface, 220, 60);
        Assert.IsTrue(box.Focus());

        var caret = typeof(VC.TextBox).GetField("_caretIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var anchor = typeof(VC.TextBox).GetField("_selectionAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!;

        caret.SetValue(box, -1);
        anchor.SetValue(box, -1);
        surface.ProcessTextInput("x");
        Assert.AreEqual("xabc", box.Text, "a negative selection is clamped to the start");

        caret.SetValue(box, 99);
        anchor.SetValue(box, 99);
        surface.ProcessTextInput("y");
        Assert.AreEqual("xabcy", box.Text, "a selection past the end is clamped to the text length");
    }

    [TestMethod]
    public void FirstClickPositionsTheNativeEditorCaret()
    {
        using var surface = new VisualHost { Size = new Size(240, 80) };
        _ = surface.Handle;
        Application.DoEvents();

        using var text = new VC.TextBox { Width = 200, Height = 32, Text = "abcdefghij", UseNativeEditor = true };
        surface.Root = text;
        Layout(surface, 240, 80);
        Application.DoEvents();

        var point = new PointF(text.AbsoluteBounds.X + 150f, text.AbsoluteBounds.Y + 16f);
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
        Application.DoEvents();

        Assert.IsTrue(text.IsUsingNativeEditor, "the click opens the native editor");
        Assert.IsGreaterThan(0, text.CaretIndex, "the click sets the visual caret");
        Assert.IsGreaterThan(0, text.NativeEditor.SelectionStart, "the first click positions the native caret");
    }

    [TestMethod]
    public void FirstClickFocusesTheNativeEditor()
    {
        using var form = new Form
        {
            Size = new Size(320, 200),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(surface);
        using var text = new VC.TextBox { Width = 200, Height = 32, Text = "hello", UseNativeEditor = true };
        var root = new VC.Canvas { Width = 320, Height = 200 };
        VC.Canvas.SetLeft(text, 40);
        VC.Canvas.SetTop(text, 40);
        root.Add(text);
        surface.Root = root;
        Layout(surface, 320, 200);
        form.TopMost = true;
        form.Show();
        form.Activate();
        for (var attempt = 0; attempt < 20 && !ReferenceEquals(Form.ActiveForm, form); attempt++)
        {
            SetForegroundWindow(form.Handle);
            Application.DoEvents();
            Thread.Sleep(25);
        }

        if (!ReferenceEquals(Form.ActiveForm, form))
        {
            Assert.Inconclusive("the test host did not become the active form");
        }

        var point = new PointF(text.AbsoluteBounds.X + 100f, text.AbsoluteBounds.Y + 16f);
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);

        for (var attempt = 0; attempt < 20 && !text.NativeEditor.Focused; attempt++)
        {
            Application.DoEvents();
            Thread.Sleep(25);
        }

        Assert.IsTrue(text.IsUsingNativeEditor, "the click opens the native editor");
        Assert.IsTrue(text.NativeEditor.Focused, "the first click leaves the caret in the native editor");
        form.Close();
    }

    [TestMethod]
    public void TextBox_UsesTheVisualPathByDefault()
    {
        using var surface = CreateSurface(320, 160);
        _ = surface.Handle;
        Application.DoEvents();

        var text = new VC.TextBox { Width = 260, Height = 34 };
        surface.Root = text;
        Layout(surface, 320, 160);
        surface.FocusElement(text);
        Application.DoEvents();

        Assert.IsFalse(text.UseNativeEditor, "the native island is an opt-in fallback");
        Assert.IsFalse(text.IsUsingNativeEditor, "the visual path is the default");
        Assert.IsFalse(surface.Controls.Contains(text.NativeEditor), "no child HWND is parented by default");

        surface.ProcessTextInput("abc");
        Assert.AreEqual("abc", text.Text, "typing stays on the visual path");
    }

    [TestMethod]
    public void TextBox_HidesThePlaceholderWhileFocused()
    {
        using var surface = new VisualHost { Size = new Size(300, 70) };
        var text = new VC.TextBox
        {
            Width = 260,
            Height = 34,
            Placeholder = "Filter demos...",
            UseNativeEditor = false,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            CaretBlinkInterval = 0,
        };
        surface.Root = text;
        Layout(surface, 300, 70);

        using (var idle = Render(surface, 300, 70))
        {
            Assert.IsTrue(ContainsNearColor(idle, Theme.Current.MutedForeground, 4),
                "an idle field shows its placeholder");
        }

        surface.FocusElement(text);

        using var focused = Render(surface, 300, 70);
        var near = FindNearColor(focused, Theme.Current.MutedForeground, 4);
        Assert.IsNull(near,
            $"a focused field hides the placeholder behind the caret (found {near} at {Theme.Current.MutedForeground})");
    }

    [TestMethod]
    public void TextBox_PaintsCustomSelectionAndCaret()
    {
        using var surface = new VisualHost { Size = new Size(260, 80) };
        var text = new VC.TextBox
        {
            Width = 220,
            Height = 36,
            Text = "WWWW",
            UseNativeEditor = false,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            Foreground = Color.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
            SelectionBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0x44, 0xAA)),
            SelectionForeground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)),
            CaretBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            CaretThickness = 3d,
            CaretBlinkInterval = 0,
        };
        surface.Root = text;
        Layout(surface, 260, 80);
        surface.FocusElement(text);
        surface.ProcessKeyDown(Keys.A, Keys.Control);
        Assert.AreEqual(4, text.SelectionLength);

        using var bitmap = Render(surface, 260, 80);

        Assert.IsTrue(ContainsColor(bitmap, Color.FromArgb(0xFF, 0x22, 0x44, 0xAA)),
            "the custom selection brush is painted");
        Assert.IsTrue(ContainsColor(bitmap, Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)),
            "selected text uses the custom selection foreground");
        Assert.IsTrue(ContainsColor(bitmap, Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            "the custom caret brush is painted");
    }

    [TestMethod]
    public void RichTextBox_PaintsCustomSelectionAndCaret()
    {
        using var surface = new VisualHost { Size = new Size(260, 80) };
        var text = new VC.RichTextBox
        {
            Width = 220,
            Height = 36,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            Foreground = Color.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
            SelectionBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0x44, 0xAA)),
            SelectionForeground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)),
            CaretBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            CaretThickness = 3d,
            CaretBlinkInterval = 0,
        };
        text.Text = "WWWW";
        surface.Root = text;
        Layout(surface, 260, 80);
        surface.FocusElement(text);
        surface.ProcessKeyDown(Keys.A, Keys.Control);
        Assert.AreEqual(4, text.SelectionLength);

        using var bitmap = Render(surface, 260, 80);

        Assert.IsTrue(ContainsColor(bitmap, Color.FromArgb(0xFF, 0x22, 0x44, 0xAA)),
            "the custom selection brush is painted");
        Assert.IsTrue(ContainsColor(bitmap, Color.FromArgb(0xFF, 0x00, 0xFF, 0x00)),
            "selected text uses the custom selection foreground");
        Assert.IsTrue(ContainsColor(bitmap, Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            "the custom caret brush is painted");
    }

    private static Bitmap Render(VisualHost surface, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
        return bitmap;
    }

    private static Point? FindNearColor(Bitmap bitmap, Color color, int tolerance)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (Math.Abs(pixel.R - color.R) <= tolerance
                    && Math.Abs(pixel.G - color.G) <= tolerance
                    && Math.Abs(pixel.B - color.B) <= tolerance)
                {
                    return new Point(x, y);
                }
            }
        }

        return null;
    }

    private static bool ContainsNearColor(Bitmap bitmap, Color color, int tolerance)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (Math.Abs(pixel.R - color.R) <= tolerance
                    && Math.Abs(pixel.G - color.G) <= tolerance
                    && Math.Abs(pixel.B - color.B) <= tolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsColor(Bitmap bitmap, Color color)
    {
        var target = color.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == target)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [TestMethod]
    public void TextBox_InsertKeyTogglesOverwriteMode()
    {
        using var surface = CreateSurface(320, 80);
        var text = new VC.TextBox { Width = 280, Height = 34, Text = "abc", UseNativeEditor = false };
        surface.Root = text;
        Layout(surface, 320, 80);
        surface.FocusElement(text);

        Assert.IsFalse(text.IsOverwriteMode);
        surface.ProcessKeyDown(Keys.Insert);
        Assert.IsTrue(text.IsOverwriteMode, "Insert turns overwrite on");
        surface.ProcessKeyDown(Keys.Insert);
        Assert.IsFalse(text.IsOverwriteMode, "Insert turns it back off");
    }

    [TestMethod]
    public void TextBox_OverwriteReplacesTheNextCharacter()
    {
        using var surface = CreateSurface(320, 80);
        var text = new VC.TextBox { Width = 280, Height = 34, Text = "abc", UseNativeEditor = false };
        surface.Root = text;
        Layout(surface, 320, 80);
        surface.FocusElement(text);
        surface.ProcessKeyDown(Keys.Home);
        surface.ProcessKeyDown(Keys.Right);
        surface.ProcessKeyDown(Keys.Insert);

        surface.ProcessTextInput("X");
        Assert.AreEqual("aXc", text.Text, "the next character is replaced");
        Assert.AreEqual(2, text.CaretIndex);

        surface.ProcessTextInput("Y");
        Assert.AreEqual("aXY", text.Text, "the next character is replaced again");

        surface.ProcessTextInput("Z");
        Assert.AreEqual("aXYZ", text.Text, "at the end the field inserts");
    }

    [TestMethod]
    public void TextBox_OverwriteModeDrawsABlockCaret()
    {
        using var surface = new VisualHost { Size = new Size(260, 80) };
        var text = new VC.TextBox
        {
            Width = 220,
            Height = 36,
            Text = "WWWW",
            UseNativeEditor = false,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            Foreground = Color.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
            CaretBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            CaretThickness = 1d,
            CaretBlinkInterval = 0,
            CaretBlockOpacity = 1d,
        };
        surface.Root = text;
        Layout(surface, 260, 80);
        surface.FocusElement(text);
        surface.ProcessKeyDown(Keys.Home);

        using var insert = Render(surface, 260, 80);
        var insertPixels = CountRedish(insert);
        Assert.IsGreaterThan(0, insertPixels, "the insert caret is painted");

        surface.ProcessKeyDown(Keys.Insert);
        using var overwrite = Render(surface, 260, 80);
        var overwritePixels = CountRedish(overwrite);

        Assert.IsGreaterThan(insertPixels, overwritePixels, "the overwrite caret is a wider block");
    }

    [TestMethod]
    public void TextBox_CaretSitsAtTheEndOfTheLastCharacter()
    {
        using var surface = new VisualHost { Size = new Size(260, 80) };
        var text = new VC.TextBox
        {
            Width = 220,
            Height = 36,
            Text = "WWWW",
            UseNativeEditor = false,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            Foreground = Color.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
            CaretBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            CaretThickness = 1d,
            CaretBlinkInterval = 0,
        };
        surface.Root = text;
        Layout(surface, 260, 80);
        surface.FocusElement(text);
        surface.ProcessKeyDown(Keys.End);

        using var bitmap = Render(surface, 260, 80);
        var caretLeft = int.MaxValue;
        var lastGlyph = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (IsRedish(pixel))
                {
                    caretLeft = Math.Min(caretLeft, x);
                }
                else if (pixel.R > 160 && pixel.G > 160 && pixel.B > 160)
                {
                    lastGlyph = Math.Max(lastGlyph, x);
                }
            }
        }

        Assert.IsTrue(caretLeft < int.MaxValue, "the caret is painted");
        Assert.IsTrue(lastGlyph > 0, "the text is painted");
        Assert.IsLessThan(4, caretLeft - lastGlyph,
            $"the caret sits at the text advance (glyph={lastGlyph}, caret={caretLeft})");
    }

    private static int CountRedish(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (IsRedish(bitmap.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool IsRedish(Color pixel) => pixel.R > 120 && pixel.G < 80 && pixel.B < 80;

    [TestMethod]
    public void TextBox_CentersSingleLineTextVertically()
    {
        using var surface = new VisualHost { Size = new Size(260, 100) };
        var text = new VC.TextBox
        {
            Width = 220,
            Height = 64,
            Text = "WW",
            UseNativeEditor = false,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            Foreground = Color.FromArgb(0xFF, 0xE7, 0xE7, 0xE7),
            CaretBlinkInterval = 0,
        };
        surface.Root = text;
        Layout(surface, 260, 100);
        surface.FocusElement(text);

        using var bitmap = Render(surface, 260, 100);
        var top = int.MaxValue;
        var bottom = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 160 && pixel.G > 160 && pixel.B > 160)
                {
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        Assert.IsTrue(bottom > top, "the text is painted");
        var textCenter = (top + bottom) / 2f;
        var fieldCenter = text.AbsoluteBounds.Top + (text.AbsoluteBounds.Height / 2f);
        Assert.IsTrue(Math.Abs(textCenter - fieldCenter) <= 4f,
            $"the single line sits in the middle (text={textCenter}, field={fieldCenter})");
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    private static VisualHost CreateSurface(int width, int height)
        => new() { Size = new Size(width, height) };

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}

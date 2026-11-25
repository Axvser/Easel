using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Dialogs;
using VC = Easel.Visuals.Controls;
using DialogResult = Easel.Visuals.Dialogs.DialogResult;

namespace Easel.Test;

/// <summary>
/// The in-surface dialog layer: scrim and card rendering, message/confirm/prompt services, input blocking, focus
/// restoration, Tab trapping, stacking, native-island hiding and control-template parts.
/// </summary>
[STATestClass]
public sealed class VisualDialogTests
{
    [TestMethod]
    public void DialogPaintsScrimCardChromeTitleBodyAndPrimaryButton()
    {
        using var surface = CreateSurface(480, 320);
        var rootColor = Color.FromArgb(0xFF, 0x10, 0x20, 0x30);
        using var root = new VC.Border { Background = rootColor };
        surface.Root = root;

        using var dialog = new VisualDialog(new DialogOptions
        {
            Title = "Delete file?",
            Message = "This cannot be undone.",
            PrimaryText = "Delete",
            SecondaryText = "Keep",
            Kind = DialogKind.Warning,
            Animate = false,
        });
        var task = dialog.ShowAsync(surface);
        using var bitmap = Render(surface, 480, 320);

        var theme = Theme.Current;
        var card = dialog.Card.AbsoluteBounds;
        Assert.IsTrue(card.Width <= 432f, "the card is capped at MaxWidth and the outer margin");
        Assert.AreEqual((480f - card.Width) / 2f, card.X, 1f, "the card is centred horizontally");
        Assert.AreEqual((320f - card.Height) / 2f, card.Y, 1f, "the card is centred vertically");
        Assert.IsTrue(card.Bottom < 320f);

        // 遮罩是 40% 黑，叠在根底色上。
        var scrim = bitmap.GetPixel(5, 5);
        AssertColorNear(scrim, Color.FromArgb(0xFF, (int)(rootColor.R * 0.6), (int)(rootColor.G * 0.6), (int)(rootColor.B * 0.6)), 6, "the scrim darkens the root tree");

        var cardFill = bitmap.GetPixel((int)card.X + 30, (int)card.Y + 8);
        AssertColorNear(cardFill, theme.Surface, 6, "the card paints the theme surface");

        Assert.IsTrue(ContainsColor(bitmap, Inflate(card, 2f), theme.ControlBorder, 14),
            "the card paints a border in the theme border colour");

        var header = new RectangleF(card.X + 24f, card.Y + 18f, card.Width - 48f, 42f);
        Assert.IsTrue(ContainsColor(bitmap, header, theme.Foreground, 90),
            "the title text paints inside the card header");

        var body = new RectangleF(card.X + 24f, card.Y + 58f, card.Width - 48f, 50f);
        Assert.IsTrue(ContainsColor(bitmap, body, theme.MutedForeground, 90),
            "the body message paints inside the card");

        var footer = new RectangleF(card.X + 24f, card.Bottom - 60f, card.Width - 48f, 50f);
        Assert.IsTrue(ContainsColor(bitmap, footer, theme.Accent, 12),
            "the primary button is filled with the accent colour");
        Assert.IsTrue(ContainsColor(bitmap, footer, theme.ControlBorder, 14),
            "the secondary button keeps an outlined border");

        Assert.IsFalse(task.IsCompleted, $"a dialog stays open until input closes it (open={dialog.IsOpen}, result={dialog.Result}, status={task.Status})");
        Assert.IsTrue(dialog.IsOpen);
    }

    [TestMethod]
    public void MessageAsyncCompletesOnPrimaryClick()
    {
        using var surface = CreateSurface(480, 320);
        var task = DialogService.MessageAsync(
            surface,
            "Saved",
            "Your changes were stored.",
            DialogKind.Success);
        Render(surface, 480, 320);
        var dialog = surface.ActiveDialog!;
        Assert.IsNotNull(dialog.PrimaryButton);

        Click(surface, dialog.PrimaryButton!);

        Assert.IsTrue(task.IsCompleted);
        Assert.AreEqual(DialogResult.Primary, task.GetAwaiter().GetResult());
        Assert.IsFalse(dialog.IsOpen, "the dialog leaves the overlay when it closes");
    }

    [TestMethod]
    public void ConfirmAsyncReturnsSecondaryAndEscapeReturnsCancel()
    {
        using var surface = CreateSurface(480, 320);

        var secondaryTask = DialogService.ConfirmAsync(
            surface,
            "Overwrite?",
            "Replace the existing file.",
            "Replace",
            "Keep both");
        Render(surface, 480, 320);
        var secondaryDialog = surface.ActiveDialog!;
        Click(surface, secondaryDialog.SecondaryButton!);
        Assert.AreEqual(DialogResult.Secondary, secondaryTask.GetAwaiter().GetResult());

        var cancelTask = DialogService.ConfirmAsync(
            surface,
            "Overwrite?",
            "Replace the existing file.",
            "Replace",
            "Keep both");
        Render(surface, 480, 320);
        Assert.IsTrue(surface.ProcessKeyDown(Keys.Escape), "Escape is handled by the dialog");
        Assert.AreEqual(DialogResult.Cancel, cancelTask.GetAwaiter().GetResult());
        Assert.IsNull(surface.ActiveDialog);
    }

    [TestMethod]
    public void ScrimBlocksPointerInputToTheRootTree()
    {
        using var surface = CreateSurface(480, 320);
        var root = new VC.StackPanel();
        var covered = new VC.Button { Text = "Covered", Width = 480, Height = 320 };
        var clicks = 0;
        covered.Click += (_, _) => clicks++;
        root.Add(covered);
        surface.Root = root;
        Render(surface, 480, 320);

        using var dialog = new VisualDialog(new DialogOptions
        {
            Title = "Modal",
            Message = "The covered tree must not receive this press.",
            Animate = false,
        });
        var task = dialog.ShowAsync(surface);
        Render(surface, 480, 320);

        var point = new PointF(5f, 5f);
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);

        Assert.AreEqual(0, clicks, "the scrim owns the press instead of the covered button");
        Assert.IsTrue(dialog.IsOpen, "the default dialog ignores outside presses");

        dialog.Close(DialogResult.Cancel);
        Assert.AreEqual(DialogResult.Cancel, task.GetAwaiter().GetResult());
    }

    [TestMethod]
    public void OutsideClickClosesOnlyWhenConfigured()
    {
        using var surface = CreateSurface(480, 320);

        using var stubborn = new VisualDialog(new DialogOptions
        {
            Title = "Stay",
            Message = "This dialog ignores outside presses.",
            DismissOnOutsideClick = false,
            Animate = false,
        });
        var stubbornTask = stubborn.ShowAsync(surface);
        Render(surface, 480, 320);
        PressOutside(surface);
        Assert.IsTrue(stubborn.IsOpen, "DismissOnOutsideClick=false keeps the dialog open");
        Assert.IsFalse(stubbornTask.IsCompleted);
        stubborn.Close(DialogResult.Cancel);

        using var dismissible = new VisualDialog(new DialogOptions
        {
            Title = "Dismiss",
            Message = "This dialog closes on an outside press.",
            DismissOnOutsideClick = true,
            Animate = false,
        });
        var dismissibleTask = dismissible.ShowAsync(surface);
        Render(surface, 480, 320);

        var insideCard = new PointF(
            dismissible.Card.AbsoluteBounds.X + 4f,
            dismissible.Card.AbsoluteBounds.Y + 4f);
        surface.ProcessPointerDown(insideCard, MouseButtons.Left);
        surface.ProcessPointerUp(insideCard, MouseButtons.Left);
        Assert.IsTrue(dismissible.IsOpen, "a press inside the card does not dismiss it");

        PressOutside(surface);
        Assert.IsFalse(dismissible.IsOpen);
        Assert.AreEqual(DialogResult.Cancel, dismissibleTask.GetAwaiter().GetResult());
    }

    [TestMethod]
    public void FocusStartsOnPrimaryIsTrappedInTheDialogAndIsRestored()
    {
        using var surface = CreateSurface(480, 320);
        var root = new VC.StackPanel();
        var outsideButton = new VC.Button { Text = "Outside", Width = 120, Height = 30 };
        root.Add(outsideButton);
        surface.Root = root;
        Render(surface, 480, 320);
        Assert.IsTrue(surface.FocusElement(outsideButton));

        using var dialog = new VisualDialog(new DialogOptions
        {
            Title = "Confirm",
            Message = "Pick one.",
            PrimaryText = "Yes",
            SecondaryText = "No",
            Animate = false,
        });
        dialog.ShowAsync(surface);
        Render(surface, 480, 320);

        Assert.AreSame(dialog.PrimaryButton, surface.FocusedElement, "focus starts on the primary button");

        surface.ProcessKeyDown(Keys.Tab);
        Assert.IsTrue(
            dialog.ContainsVisual(surface.FocusedElement!),
            "Tab moves focus to the secondary button inside the dialog");
        surface.ProcessKeyDown(Keys.Tab, Keys.Shift);
        Assert.AreSame(dialog.PrimaryButton, surface.FocusedElement, "Shift+Tab cycles back inside the dialog");
        Assert.AreNotSame(outsideButton, surface.FocusedElement, "focus never escapes to the covered tree");

        surface.ProcessKeyDown(Keys.Escape);
        Assert.AreSame(outsideButton, surface.FocusedElement, "closing restores the element focused before the dialog");
    }

    [TestMethod]
    public void TwoStackedDialogsResolveInOrderAndRestoreFocusToTheNext()
    {
        using var surface = CreateSurface(480, 320);

        using var first = new VisualDialog(new DialogOptions
        {
            Title = "First",
            Message = "The lower dialog.",
            PrimaryText = "First OK",
            Animate = false,
        });
        var firstTask = first.ShowAsync(surface);
        Render(surface, 480, 320);
        Assert.AreSame(first.PrimaryButton, surface.FocusedElement);

        using var second = new VisualDialog(new DialogOptions
        {
            Title = "Second",
            Message = "The newest dialog sits on top.",
            PrimaryText = "Second OK",
            Animate = false,
        });
        var secondTask = second.ShowAsync(surface);
        Render(surface, 480, 320);
        Assert.AreSame(second.PrimaryButton, surface.FocusedElement, "the newest dialog takes focus");

        Click(surface, second.PrimaryButton!);

        Assert.AreEqual(DialogResult.Primary, secondTask.GetAwaiter().GetResult());
        Assert.IsFalse(second.IsOpen);
        Assert.IsTrue(first.IsOpen, "the lower dialog stays open");
        Assert.AreSame(first.PrimaryButton, surface.FocusedElement, "closing the top dialog restores focus to the next");

        Click(surface, first.PrimaryButton!);
        Assert.AreEqual(DialogResult.Primary, firstTask.GetAwaiter().GetResult());
        Assert.IsFalse(first.IsOpen);
    }

    [TestMethod]
    public async Task PromptReturnsTheInitialTextOnEnterAndNullOnCancel()
    {
        using var surface = CreateSurface(480, 320);

        var accepted = DialogService.PromptAsync(surface, "Rename", "New name", "document");
        Render(surface, 480, 320);
        Assert.IsTrue(surface.ProcessKeyDown(Keys.Enter), "Enter accepts the prompt");
        Assert.AreEqual("document", await accepted);

        var cancelled = DialogService.PromptAsync(surface, "Rename", "New name", "document");
        Render(surface, 480, 320);
        surface.ProcessKeyDown(Keys.Escape);
        Assert.IsNull(await cancelled);
    }

    [TestMethod]
    public void TemplatePartsArePopulatedAndClickThrough()
    {
        using var surface = CreateSurface(480, 320);

        VC.TextBlock? title = null;
        VC.TextBlock? body = null;
        VC.Button? primary = null;
        var template = new VisualControlTemplate(scope =>
        {
            var root = new VC.StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
            title = new VC.TextBlock();
            body = new VC.TextBlock { Wrap = true };
            primary = new VC.Button { Text = "", Height = 30 };
            root.Add(title);
            root.Add(body);
            root.Add(primary);
            scope.Register("PART_Title", title);
            scope.Register("PART_Content", body);
            scope.Register("PART_Primary", primary);
            return root;
        })
        {
            TargetType = typeof(VisualDialog),
        };
        template.Triggers.Add(new VisualTrigger(VisualElement.IsEnabledProperty, false)
        {
            TargetName = "PART_Primary",
            Setters = { new VisualSetter(VisualElement.WidthProperty, 140d) },
        });

        using var dialog = new VisualDialog(new DialogOptions
        {
            Title = "Templated",
            Message = "Template body",
            PrimaryText = "Continue",
            Animate = false,
        })
        {
            Template = template,
        };

        var task = dialog.ShowAsync(surface);
        Render(surface, 480, 320);

        Assert.AreEqual("Templated", title!.Text, "PART_Title receives the title");
        Assert.AreEqual("Template body", body!.Text, "PART_Content receives the message");
        Assert.AreEqual("Continue", primary!.Text, "a part with empty text receives the primary label");
        Assert.AreSame(primary, dialog.PrimaryButton);

        Click(surface, primary);
        Assert.AreEqual(DialogResult.Primary, task.GetAwaiter().GetResult());

        dialog.IsEnabled = false;
        Assert.AreEqual(140d, primary.Width, "a template trigger still targets the named part after it moved into the card");
    }

    [TestMethod]
    public void NativeIslandHidesWhileTheDialogIsOpenAndIsPlacedAgainOnClose()
    {
        using var form = new Form
        {
            Size = new Size(360, 260),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(surface);

        var root = new VC.Canvas { Width = 360, Height = 260 };
        using var nativeButton = new Button { Text = "native" };
        var island = new VC.NativeElement(nativeButton) { Width = 120, Height = 32 };
        VC.Canvas.SetLeft(island, 24);
        VC.Canvas.SetTop(island, 24);
        root.Add(island);
        surface.Root = root;

        form.Show();
        form.Activate();
        Application.DoEvents();
        Thread.Sleep(80);
        Application.DoEvents();
        Assert.IsTrue(nativeButton.Visible, "the live island starts visible");

        using var dialog = new VisualDialog(new DialogOptions
        {
            Title = "Modal",
            Message = "The island must not float over the scrim.",
            Animate = false,
        });
        var task = dialog.ShowAsync(surface);
        Assert.IsFalse(nativeButton.Visible, "the island is hidden for the dialog's lifetime");

        surface.ProcessKeyDown(Keys.Escape);
        Assert.AreEqual(DialogResult.Cancel, task.GetAwaiter().GetResult());
        Application.DoEvents();

        Assert.IsTrue(nativeButton.Visible, "the island is shown again on close");
        Assert.IsTrue(surface.Controls.Contains(nativeButton), "the island keeps its handle and parent");
        Assert.IsTrue(nativeButton.Width > 0 && nativeButton.Height > 0, "the island is placed again");
        Assert.AreEqual(surface.DpiScale * 24, nativeButton.Left, 2f, "the island keeps its arranged position");

        form.Close();
    }

    private static VisualHost CreateSurface(int width, int height)
        => new() { Size = new Size(width, height) };

    private static Bitmap Render(VisualHost surface, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
        return bitmap;
    }

    private static void Click(VisualHost surface, VisualElement element)
    {
        var point = new PointF(
            element.AbsoluteBounds.X + (element.AbsoluteBounds.Width / 2f),
            element.AbsoluteBounds.Y + (element.AbsoluteBounds.Height / 2f));
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
    }

    private static void PressOutside(VisualHost surface)
    {
        var point = new PointF(5f, 5f);
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
    }

    private static RectangleF Inflate(RectangleF rectangle, float amount)
        => new(
            rectangle.X - amount,
            rectangle.Y - amount,
            rectangle.Width + (amount * 2f),
            rectangle.Height + (amount * 2f));

    private static bool ContainsColor(Bitmap bitmap, RectangleF area, Color color, int tolerance)
    {
        var left = Math.Max(0, (int)Math.Floor(area.Left));
        var top = Math.Max(0, (int)Math.Floor(area.Top));
        var right = Math.Min(bitmap.Width, (int)Math.Ceiling(area.Right));
        var bottom = Math.Min(bitmap.Height, (int)Math.Ceiling(area.Bottom));
        var limit = tolerance * tolerance * 3;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var dr = pixel.R - color.R;
                var dg = pixel.G - color.G;
                var db = pixel.B - color.B;
                if ((dr * dr) + (dg * dg) + (db * db) <= limit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AssertColorNear(Color actual, Color expected, int tolerance, string message)
    {
        var dr = actual.R - expected.R;
        var dg = actual.G - expected.G;
        var db = actual.B - expected.B;
        Assert.IsTrue(
            (dr * dr) + (dg * dg) + (db * db) <= tolerance * tolerance * 3,
            $"{message} (actual {actual}, expected {expected})");
    }

}

using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The calendar family's built-in templates, their part contracts and the state they preserve.</summary>
[STATestClass]
public sealed class CalendarDefaultsTests
{
    [TestMethod]
    public void CalendarDefaultTemplate_HostsTheMonthGridAndSelectsADay()
    {
        using var surface = new VisualHost { Size = new Size(320, 300), DpiScaleOverride = 1f };
        using var calendar = new VC.Calendar
        {
            Width = 250d,
            Height = 226d,
            DisplayMonth = new DateTime(2024, 5, 1),
        };
        surface.Root = calendar;
        Layout(surface, 320, 300);

        Assert.IsNotNull(calendar.TemplateRoot, "the Calendar implicit style supplies the default template");
        var item = calendar.MonthGrid;
        Assert.IsNotNull(item, "PART_CalendarItem resolves through the default template");
        Assert.IsNotNull(item!.TemplateRoot, "the CalendarItem implicit style supplies its month-grid template");

        var header = item.GetTemplateChild<VC.TextBlock>("PART_Header");
        var weekDays = item.GetTemplateChild<VC.UniformGrid>("PART_WeekDays");
        var days = item.GetTemplateChild<VC.UniformGrid>("PART_Days");
        Assert.IsNotNull(header, "PART_Header resolves through the item template");
        Assert.IsNotNull(weekDays, "PART_WeekDays resolves through the item template");
        Assert.IsNotNull(days, "PART_Days resolves through the item template");
        Assert.AreEqual(
            new DateTime(2024, 5, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            header!.Text,
            "the header shows the display month");

        Assert.HasCount(7, weekDays!.Children, "the weekday row has one label per column");
        Assert.HasCount(42, days!.Children, "the day grid keeps six weeks of slots");

        var index = DayIndex(new DateTime(2024, 5, 15), new DateTime(2024, 5, 1), DayOfWeek.Monday);
        var cell = (VC.CalendarDayCell)days.Children[index];
        Click(surface, Center(cell.AbsoluteBounds));

        Assert.AreEqual(new DateTime(2024, 5, 15), calendar.SelectedDate!.Value.Date,
            "clicking a day cell selects that date");
        Assert.AreEqual(5, calendar.DisplayMonth.Month, "the display month follows the selection");
    }

    [TestMethod]
    public void CalendarItemDefaultTemplate_NavigatesAndTracksHoverThroughParts()
    {
        using var surface = new VisualHost { Size = new Size(320, 300), DpiScaleOverride = 1f };
        using var calendar = new VC.Calendar
        {
            Width = 250d,
            Height = 226d,
            DisplayMonth = new DateTime(2024, 5, 1),
            SelectedDate = new DateTime(2024, 5, 6),
        };
        surface.Root = calendar;
        Layout(surface, 320, 300);

        var item = calendar.MonthGrid!;
        var previous = item.GetTemplateChild<VC.Border>("PART_Previous");
        var next = item.GetTemplateChild<VC.Border>("PART_Next");
        Assert.IsNotNull(previous, "PART_Previous resolves through the item template");
        Assert.IsNotNull(next, "PART_Next resolves through the item template");
        Assert.IsFalse(previous!.IsHitTestVisible, "the arrow chrome stays decorative");
        Assert.IsFalse(next!.IsHitTestVisible);

        Click(surface, Center(previous.AbsoluteBounds));
        Assert.AreEqual(new DateTime(2024, 4, 1), calendar.DisplayMonth, "the previous part moves one month back");

        Click(surface, Center(next.AbsoluteBounds));
        Click(surface, Center(next.AbsoluteBounds));
        Assert.AreEqual(new DateTime(2024, 6, 1), calendar.DisplayMonth, "the next part moves one month forward");

        calendar.DisplayMonth = new DateTime(2024, 5, 1);
        var days = item.GetTemplateChild<VC.UniformGrid>("PART_Days")!;
        var index = DayIndex(new DateTime(2024, 5, 9), new DateTime(2024, 5, 1), DayOfWeek.Monday);
        var cell = (VC.CalendarDayCell)days.Children[index];
        surface.ProcessPointerMove(Center(cell.AbsoluteBounds));
        Assert.AreEqual(new DateTime(2024, 5, 9), item.HoverDate, "moving over a day reports it as hovered");

        surface.ProcessPointerMove(new PointF(315f, 295f));
        Assert.IsNull(item.HoverDate, "leaving the grid clears the hover date");
    }

    [TestMethod]
    public void CalendarFamilyDefaultTemplates_MatchExactTypesOnly()
    {
        using var surface = new VisualHost { Size = new Size(320, 300), DpiScaleOverride = 1f };
        using var calendar = new VC.Calendar { Width = 250d, Height = 226d };
        surface.Root = calendar;
        Layout(surface, 320, 300);
        Assert.IsNotNull(calendar.TemplateRoot, "the exact Calendar type gets the implicit default template");
        Assert.IsNotNull(calendar.MonthGrid!.TemplateRoot, "the exact CalendarItem type gets its own template");

        using var explicitCalendar = new VC.Calendar
        {
            Width = 250d,
            Height = 226d,
            // An explicit style wins over the exact-type default and keeps the self-drawn fallback.
            Style = new VisualStyle { TargetType = typeof(VC.Calendar) },
        };
        surface.Root = explicitCalendar;
        Layout(surface, 320, 300);
        Assert.IsNull(explicitCalendar.TemplateRoot, "an explicit empty style opts out of the default template");
        Assert.IsNull(explicitCalendar.MonthGrid, "the self-drawn calendar has no month-grid part");
    }

    [TestMethod]
    public void DatePickerDefaultTemplate_ShowsTheFormatAndDrivesThePopup()
    {
        using var surface = new VisualHost { Size = new Size(320, 300), DpiScaleOverride = 1f };
        using var picker = new VC.DatePicker
        {
            Width = 180d,
            Height = 34d,
            Format = "dd/MM/yyyy",
            SelectedDate = new DateTime(2024, 5, 6),
        };
        surface.Root = picker;
        Layout(surface, 320, 300);

        Assert.IsNotNull(picker.TemplateRoot, "the DatePicker implicit style supplies the default template");
        var textBox = picker.GetTemplateChild<VC.TextBox>("PART_TextBox");
        var button = picker.GetTemplateChild<VC.Button>("PART_Button");
        var popup = picker.GetTemplateChild<VC.VisualPopup>("PART_Popup");
        Assert.IsNotNull(textBox, "PART_TextBox resolves through the default template");
        Assert.IsNotNull(button, "PART_Button resolves through the default template");
        Assert.IsNotNull(popup, "PART_Popup resolves through the default template");
        Assert.IsNotNull(textBox!.TemplateRoot, "the TextBox part gets the default text-input template");
        Assert.IsNotNull(button!.TemplateRoot, "the Button part gets the default button template");
        Assert.AreEqual("06/05/2024", textBox!.Text, "the field shows the selected date in Format");
        var field = textBox.AbsoluteBounds;
        Assert.IsGreaterThan(
            0,
            CountBrightPixels(surface, new RectangleF(field.X + 10f, field.Y + 6f, field.Width - 40f, field.Height - 12f)),
            "the field text actually paints");

        picker.SelectedDate = new DateTime(2024, 6, 7);
        Assert.AreEqual("07/06/2024", textBox.Text, "changing the selection refreshes the field text");

        textBox.Text = "2024-12-24";
        Assert.AreEqual(new DateTime(2024, 12, 24), picker.SelectedDate!.Value.Date,
            "typing a parseable date commits it");

        Click(surface, Center(button!.AbsoluteBounds));
        Assert.IsTrue(picker.IsDropDownOpen, "the button opens the drop-down");
        Assert.AreSame(picker.DropDownCalendar, popup!.Child, "PART_Popup hosts the picker's calendar");
        Assert.IsInstanceOfType<VC.Calendar>(surface.FocusedElement);

        surface.ProcessKeyDown(Keys.Right);
        Assert.IsFalse(picker.IsDropDownOpen, "choosing a day closes the drop-down");
        Assert.AreEqual(new DateTime(2024, 12, 25), picker.SelectedDate!.Value.Date,
            "the calendar moves the selection by one day");
        Assert.AreEqual("25/12/2024", textBox.Text, "the field text follows the calendar selection");
    }

    [TestMethod]
    public void DatePickerDefaultTemplate_ClickingTheFieldOpensThePopupAndShowsThePlaceholder()
    {
        using var surface = new VisualHost { Size = new Size(320, 300), DpiScaleOverride = 1f };
        using var picker = new VC.DatePicker
        {
            Width = 180d,
            Height = 34d,
            Placeholder = "Pick a day",
        };
        surface.Root = picker;
        Layout(surface, 320, 300);

        var textBox = picker.GetTemplateChild<VC.TextBox>("PART_TextBox")!;
        Assert.AreEqual("Pick a day", textBox.Text, "the placeholder fills an empty field");
        Assert.AreEqual(Theme.Current.MutedForeground.ToArgb(), textBox.Foreground.ToArgb(),
            "the placeholder uses the muted palette colour");

        Click(surface, Center(picker.AbsoluteBounds));
        Assert.IsTrue(picker.IsDropDownOpen, "a press on the field opens the drop-down");
        Assert.IsInstanceOfType<VC.Calendar>(surface.FocusedElement);

        // The calendar owns the keyboard while the popup is open; choosing a day closes the drop-down.
        surface.ProcessKeyDown(Keys.Right);
        Assert.IsFalse(picker.IsDropDownOpen, "choosing a day closes the drop-down");
        Assert.IsNotNull(picker.SelectedDate, "the keyboard selection lands in the picker");
    }

    private static int DayIndex(DateTime date, DateTime firstOfMonth, DayOfWeek firstDayOfWeek)
    {
        var offset = ((int)firstOfMonth.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return offset + date.Day - 1;
    }

    private static int CountBrightPixels(VisualHost surface, RectangleF region)
    {
        var size = surface.Size;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, size);
        var count = 0;
        for (var y = (int)region.Top; y < (int)region.Bottom; y++)
        {
            for (var x = (int)region.Left; x < (int)region.Right; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 120 && pixel.G > 120 && pixel.B > 120)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));

    private static void Click(VisualHost surface, PointF point)
    {
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}

using System;
using System.Drawing;
using Easel;
using Easel.Visuals.Controls;

namespace LegacyFixture
{
    internal static class Program
    {
        private static int Main()
        {
            var failures = 0;

            var view = new LegacyView();
            failures += Check(view.Frame != null, "x:Name field 'frame' was not initialized");
            failures += Check(view.TitleControl != null, "x:Name field 'title' was not initialized");

            var brush = view.Frame.Background as SolidColorBrush;
            failures += Check(brush != null, "Background did not produce a SolidColorBrush");
            if (brush != null)
            {
                failures += Check(
                    brush.Color == Color.FromArgb(255, 16, 32, 48),
                    "Background color was not parsed");
            }

            failures += Check(Math.Abs(view.Frame.Width - 80) < 0.01, "Width literal was not applied");

            view.Frame.Width = 42;
            failures += Check(Math.Abs(view.Frame.Width - 42) < 0.01, "dependency property write/read failed");

            var card = new LegacyCard();
            card.SetValue(LegacyCard.GapProperty, 7);
            failures += Check((int)card.GetValue(LegacyCard.GapProperty) == 7, "generated attached property failed");

            var pair = (Name: "easel", Count: 2);
            failures += Check(pair.Count == 2 && pair.Name == "easel", "ValueTuple is not usable on net45");

            Console.WriteLine(failures == 0 ? "legacy fixture passed" : "legacy fixture failed: " + failures);
            return failures == 0 ? 0 : 1;
        }

        private static int Check(bool condition, string message)
        {
            if (condition)
            {
                return 0;
            }

            Console.WriteLine("FAIL: " + message);
            return 1;
        }
    }
}

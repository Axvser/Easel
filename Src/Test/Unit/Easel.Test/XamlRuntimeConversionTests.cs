using System.Drawing;
using Easel;

namespace Easel.Test;

/// <summary>Literal conversions the XAML compiler hands to <see cref="XamlRuntime.ConvertValue"/>.</summary>
[TestClass]
public sealed class XamlRuntimeConversionTests
{
    /// <summary>A rectangle literal keeps its four numbers in x,y,width,height order.</summary>
    [TestMethod]
    public void RectangleF_LiteralParsesFourNumbers()
    {
        var rectangle = (RectangleF)XamlRuntime.ConvertValue("1.5,2,30,40", typeof(RectangleF))!;

        Assert.AreEqual(1.5f, rectangle.X);
        Assert.AreEqual(2f, rectangle.Y);
        Assert.AreEqual(30f, rectangle.Width);
        Assert.AreEqual(40f, rectangle.Height);
    }

    /// <summary>An integer rectangle uses the same order and rejects a wrong count.</summary>
    [TestMethod]
    public void Rectangle_LiteralParsesFourIntegers()
    {
        var rectangle = (Rectangle)XamlRuntime.ConvertValue("3, 4, 50, 60", typeof(Rectangle))!;

        Assert.AreEqual(new Rectangle(3, 4, 50, 60), rectangle);
        Assert.ThrowsExactly<FormatException>(() => XamlRuntime.ConvertValue("1,2,3", typeof(Rectangle)));
    }
}

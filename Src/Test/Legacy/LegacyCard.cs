using Easel;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace LegacyFixture
{
    public partial class LegacyCard : StackPanel
    {
        [XamlAttachedProperty(DefaultValue = "0")]
        static partial void SetGap(VisualElement element, int value);
    }
}

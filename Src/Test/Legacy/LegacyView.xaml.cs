using Easel.Visuals.Controls;

namespace LegacyFixture
{
    public partial class LegacyView : StackPanel
    {
        public LegacyView()
        {
            InitializeComponent();
        }

        public Border Frame
        {
            get { return frame; }
        }

        public TextBlock TitleControl
        {
            get { return title; }
        }
    }
}

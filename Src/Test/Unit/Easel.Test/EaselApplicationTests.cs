using System.Windows.Forms;

namespace Easel.Test;

[STATestClass]
public sealed class EaselApplicationTests
{
    [TestMethod]
    public void TheDpiProbeMatchesTheProcessMode()
    {
        var mode = Application.HighDpiMode;
        var perMonitorV2 = EaselApplication.IsPerMonitorV2Active();

        if (mode == HighDpiMode.PerMonitorV2)
        {
            Assert.IsTrue(perMonitorV2);
        }
        else if (mode != HighDpiMode.PerMonitor)
        {
            Assert.IsFalse(perMonitorV2, "a process that is not per-monitor aware cannot report PMv2");
        }
    }
}

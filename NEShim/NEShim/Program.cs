using BizHawk.Common;

namespace NEShim;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Set Windows multimedia timer resolution to 1ms so that Thread.Sleep(1)
        // actually sleeps ~1ms. Without this, Windows 11's Dynamic Timer Resolution
        // can make Sleep(1) sleep 15-25ms, breaking the 60Hz emulation loop entirely.
        Win32Imports.timeBeginPeriod(1);

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        finally
        {
            Win32Imports.timeEndPeriod(1);
        }
    }
}

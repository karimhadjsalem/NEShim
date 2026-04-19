using BizHawk.Common;
using Steamworks;

namespace NEShim;

static class Program
{
    [STAThread]
    static void Main()
    {
        // If the app was not launched through Steam, RestartAppIfNecessary()
        // relaunches it via Steam so the overlay DLL is injected correctly.
        // Must be called before SteamAPI.Init().
        var appIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");
        if (File.Exists(appIdPath) &&
            uint.TryParse(File.ReadAllText(appIdPath).Trim(), out uint appId) &&
            appId != 0)
        {
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                return; // Steam is relaunching us — exit this instance
        }

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

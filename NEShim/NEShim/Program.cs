using System.Reflection;
using BizHawk.Common;
using Steamworks;

namespace NEShim;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Catch unhandled exceptions on both the UI thread and background threads,
        // write a local crash.log, and show a dialog pointing to it.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => HandleCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            HandleCrash(e.ExceptionObject as Exception);

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

    private static void HandleCrash(Exception? ex)
    {
        try
        {
            string path    = Path.Combine(AppContext.BaseDirectory, "crash.log");
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            File.WriteAllText(path,
                $"NEShim crash log\n" +
                $"Time:    {DateTime.UtcNow:O}\n" +
                $"Version: {version}\n\n" +
                $"{ex}\n");
            MessageBox.Show(
                $"NEShim encountered an unexpected error and must close.\n\n" +
                $"A crash log has been written to:\n{path}\n\n" +
                "If you report this issue, please attach the log.",
                "NEShim — Unexpected Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch { /* swallow — already crashing */ }
        finally { Environment.Exit(1); }
    }
}

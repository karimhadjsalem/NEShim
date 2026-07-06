using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BizHawk.Common;
using NEShim.Platform;
using SDL3;
using Steamworks;

namespace NEShim;

[ExcludeFromCodeCoverage]
static class Program
{
    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            HandleCrash(e.ExceptionObject as Exception);

        var appIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");
        if (File.Exists(appIdPath) &&
            uint.TryParse(File.ReadAllText(appIdPath).Trim(), out uint appId) &&
            appId != 0)
        {
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                return;
        }

        Win32Imports.timeBeginPeriod(1);
        try
        {
            using var sdlHost = new SDL3WindowHost("NEShim", 1024, 672);
            new NEShimApp(sdlHost).Run();
        }
        catch (Exception ex)
        {
            HandleCrash(ex);
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
            SDL.ShowSimpleMessageBox(SDL.MessageBoxFlags.Error,
                "NEShim — Unexpected Error",
                $"NEShim encountered an unexpected error and must close.\n\n" +
                $"A crash log has been written to:\n{path}\n\n" +
                "If you report this issue, please attach the log.",
                IntPtr.Zero);
        }
        catch { }
        finally { Environment.Exit(1); }
    }
}

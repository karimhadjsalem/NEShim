using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
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

        // AppDomain.ProcessExit (Logger's own flush hook) only reliably fires on a normal Main
        // return or Environment.Exit() — an unhandled Ctrl+C (SIGINT) or `kill` (SIGTERM) can
        // terminate the process before the logger's background writer thread ever drains its
        // queue, silently truncating neshim.log's last few lines. Flush explicitly on both.
        TryRegisterFlushOnSignal(PosixSignal.SIGINT);
        TryRegisterFlushOnSignal(PosixSignal.SIGTERM);

        var appIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");
        if (File.Exists(appIdPath) &&
            uint.TryParse(File.ReadAllText(appIdPath).Trim(), out uint appId) &&
            appId != 0)
        {
            try
            {
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                    return;
            }
            catch (DllNotFoundException)
            {
                // Steam native library not present on this platform — skip restart check.
            }
        }

        PlatformDetector.BeginHighResolutionTiming();
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
            PlatformDetector.EndHighResolutionTiming();
        }
    }

    // Held for the process lifetime — PosixSignalRegistration.Create's return value must stay
    // rooted or the GC can collect it and silently unregister the handler.
    private static PosixSignalRegistration? _sigintRegistration;
    private static PosixSignalRegistration? _sigtermRegistration;

    private static void TryRegisterFlushOnSignal(PosixSignal signal)
    {
        try
        {
            var registration = PosixSignalRegistration.Create(signal, _ => Logger.Instance.Dispose());
            if (signal == PosixSignal.SIGINT) _sigintRegistration  = registration;
            else                              _sigtermRegistration = registration;
        }
        catch (PlatformNotSupportedException) { }
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

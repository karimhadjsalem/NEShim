namespace NEShim;

/// <summary>
/// Appends timestamped lines to <c>neshim.log</c> in the application directory.
/// Logging is disabled by default; call <see cref="Enable"/> once after config is loaded
/// to activate it. Thread-safe. Failures are silently swallowed so logging never crashes the game.
/// </summary>
internal static class Logger
{
    private static string        _path    =
        Path.Combine(AppContext.BaseDirectory, "neshim.log");

    private static readonly object _lock = new();
    private static volatile bool   _enabled;

    /// <summary>
    /// Resets logger state and redirects output to <paramref name="pathOverride"/>.
    /// For use in tests only — not called in production code.
    /// </summary>
    internal static void Reset(string pathOverride)
    {
        lock (_lock)
        {
            _enabled = false;
            _path    = pathOverride;
        }
    }

    /// <summary>
    /// Activates logging and writes a session-start header to the log file.
    /// Call once, immediately after the config is loaded, when <c>EnableLogging</c> is true.
    /// </summary>
    public static void Enable()
    {
        _enabled = true;
        Log($"=== NEShim session started {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
    }

    /// <summary>
    /// Writes a single line to the log file, prefixed with a UTC timestamp.
    /// No-op when logging has not been enabled via <see cref="Enable"/>.
    /// </summary>
    public static void Log(string message)
    {
        if (!_enabled) return;

        string line = $"{DateTime.UtcNow:HH:mm:ss.fff} {message}";
        try
        {
            lock (_lock)
                File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch { }
    }
}

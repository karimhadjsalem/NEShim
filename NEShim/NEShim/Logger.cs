using System.Collections.Concurrent;

namespace NEShim;

/// <summary>
/// Appends timestamped lines to <c>neshim.log</c> in the application directory.
/// Logging is disabled by default; call <see cref="Enable"/> once after config is loaded.
/// <see cref="Log"/> never blocks the caller — lines are enqueued and drained by a dedicated
/// background thread. Dispose <see cref="Instance"/> at shutdown to flush all pending lines.
/// </summary>
internal sealed class Logger : IDisposable
{
    public static readonly Logger Instance = new();

    private string _path = Path.Combine(AppContext.BaseDirectory, "neshim.log");
    private volatile bool              _enabled;
    private BlockingCollection<string>? _queue;
    private Thread?                    _writerThread;

    private Logger()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    // ---- Static convenience API — all existing call sites are unchanged ----

    public static bool IsEnabled           => Instance._enabled;
    public static void Enable()            => Instance.EnableCore();
    public static void Log(string message) => Instance.LogCore(message);

    /// <summary>
    /// Writes a line to neshim.log regardless of <see cref="Enable"/> having been called —
    /// unlike <see cref="Log"/>, which is a no-op unless the player's EnableLogging config is
    /// set. Reserved for diagnostics that must be recoverable even from a default-config run
    /// (e.g. a structurally broken multi-game DLC entry). Routes through the same background
    /// queue as <see cref="Log"/> when logging is already enabled, falling back to a direct
    /// synchronous write only when it isn't — two independent writers appending to the same file
    /// at once can otherwise race (one write silently lost to a sharing violation).
    /// </summary>
    public static void LogAlways(string message) => Instance.LogAlwaysCore(message);

    /// <summary>For use in tests only — not called in production code.</summary>
    internal static void Reset(string pathOverride) => Instance.ResetCore(pathOverride);

    // ---- IDisposable ----

    /// <summary>
    /// Drains all queued lines to disk and stops the background writer thread.
    /// After Dispose, <see cref="Enable"/> may be called again to start a new session.
    /// No-op if logging was never enabled.
    /// </summary>
    public void Dispose() => FlushCore();

    // ---- Private instance implementation ----

    private void EnableCore()
    {
        if (_enabled) FlushCore(); // re-enable: drain the running session first

        _queue        = new BlockingCollection<string>();
        _writerThread = new Thread(WriteLoop) { IsBackground = true, Name = "LogWriter" };
        _writerThread.Start();
        _enabled = true;
        LogCore($"=== NEShim session started {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
    }

    private void LogCore(string message)
    {
        if (!_enabled) return;
        string line = $"{DateTime.UtcNow:HH:mm:ss.fff} {message}";
        try { _queue?.TryAdd(line); }
        catch { }
    }

    private void LogAlwaysCore(string message)
    {
        string line = $"{DateTime.UtcNow:HH:mm:ss.fff} {message}";
        if (_enabled)
        {
            try { _queue?.TryAdd(line); }
            catch { }
            return;
        }
        try { File.AppendAllText(_path, line + Environment.NewLine); }
        catch { }
    }

    private void FlushCore()
    {
        if (!_enabled) return;
        _enabled = false;
        try { _queue?.CompleteAdding(); } catch { }
        _writerThread?.Join(2000);
    }

    private void ResetCore(string pathOverride)
    {
        FlushCore();
        _queue        = null;
        _writerThread = null;
        _path         = pathOverride;
    }

    private void WriteLoop()
    {
        if (_queue is null) return;
        foreach (string line in _queue.GetConsumingEnumerable())
        {
            try { File.AppendAllText(_path, line + Environment.NewLine); }
            catch { }
        }
    }
}

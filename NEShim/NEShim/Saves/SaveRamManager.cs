using System.IO;
using NEShim.Emulation;

namespace NEShim.Saves;

/// <summary>
/// Persists battery-backed save RAM (e.g., Zelda, Metroid) between sessions.
/// </summary>
internal sealed class SaveRamManager
{
    private readonly IEmulationCore _core;
    private readonly string _path;

    public SaveRamManager(IEmulationCore core, string sramPath)
    {
        _core = core;
        _path = sramPath;
    }

    /// <summary>Loads save RAM from disk into the emulator on startup.</summary>
    public void LoadFromDisk()
    {
        if (!File.Exists(_path))
        {
            Logger.Log($"[SaveRAM] No file at {_path} — starting fresh.");
            return;
        }

        try
        {
            byte[] data = File.ReadAllBytes(_path);
            _core.SetSaveRam(data);
            Logger.Log($"[SaveRAM] Loaded {data.Length:N0} bytes ← {_path}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[SaveRAM] Load failed (starting fresh): {ex.Message}");
        }
    }

    /// <summary>Writes save RAM to disk on shutdown (only if modified).</summary>
    public void SaveToDisk()
    {
        if (!_core.SaveRamModified)
        {
            Logger.Log("[SaveRAM] Not modified — skipping write.");
            return;
        }

        byte[]? data = _core.GetSaveRam();
        if (data is null || data.Length == 0)
        {
            Logger.Log("[SaveRAM] GetSaveRam returned empty — skipping write.");
            return;
        }

        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(_path, data);
            Logger.Log($"[SaveRAM] Saved {data.Length:N0} bytes → {_path}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[SaveRAM] Save failed: {ex.Message}");
        }
    }
}

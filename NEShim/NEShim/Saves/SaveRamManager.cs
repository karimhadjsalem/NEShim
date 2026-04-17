using System.IO;
using BizHawk.Emulation.Common;

namespace NEShim.Saves;

/// <summary>
/// Persists battery-backed save RAM (e.g., Zelda, Metroid) between sessions.
/// </summary>
internal sealed class SaveRamManager
{
    private readonly ISaveRam _saveRam;
    private readonly string _path;

    public SaveRamManager(ISaveRam saveRam, string sramPath)
    {
        _saveRam = saveRam;
        _path    = sramPath;
    }

    /// <summary>Loads save RAM from disk into the emulator on startup.</summary>
    public void LoadFromDisk()
    {
        if (!File.Exists(_path)) return;

        try
        {
            byte[] data = File.ReadAllBytes(_path);
            _saveRam.StoreSaveRam(data);
        }
        catch { /* if file is corrupt, start fresh */ }
    }

    /// <summary>Writes save RAM to disk on shutdown (only if modified).</summary>
    public void SaveToDisk()
    {
        if (!_saveRam.SaveRamModified) return;

        byte[]? data = _saveRam.CloneSaveRam();
        if (data is null || data.Length == 0) return;

        try
        {
            string dir = Path.GetDirectoryName(_path)!;
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(_path, data);
        }
        catch { /* best-effort */ }
    }
}

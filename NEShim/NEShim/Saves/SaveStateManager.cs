using System.IO;
using System.Text.Json;
using BizHawk.Emulation.Common;

namespace NEShim.Saves;

/// <summary>
/// Manages 8 save state slots plus an auto-save slot.
/// </summary>
internal sealed class SaveStateManager
{
    public const int SlotCount = 8;

    private readonly IStatable _statable;
    private readonly string _directory;

    public int ActiveSlot { get; set; } = 0;

    public SaveStateManager(IStatable statable, string saveDirectory)
    {
        _statable  = statable;
        _directory = saveDirectory;
        Directory.CreateDirectory(_directory);
    }

    // ---- Slot paths ----

    private string StatePath(int slot)    => Path.Combine(_directory, $"slot{slot}.state");
    private string MetaPath(int slot)     => Path.Combine(_directory, $"slot{slot}.meta");
    private string AutoStatePath()        => Path.Combine(_directory, "autosave.state");

    // ---- Save ----

    public void SaveSlot(int slot)
    {
        Directory.CreateDirectory(_directory);
        using var fs = File.OpenWrite(StatePath(slot));
        using var bw = new BinaryWriter(fs);
        _statable.SaveStateBinary(bw);

        var meta = new SlotMeta { Timestamp = DateTime.UtcNow };
        File.WriteAllText(MetaPath(slot), JsonSerializer.Serialize(meta));
        Logger.Log($"[SaveState] Saved slot {slot + 1} → {StatePath(slot)}");
    }

    public void SaveToActiveSlot() => SaveSlot(ActiveSlot);

    public void AutoSave()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            using var fs = File.OpenWrite(AutoStatePath());
            using var bw = new BinaryWriter(fs);
            _statable.SaveStateBinary(bw);
            Logger.Log($"[SaveState] Auto-save written → {AutoStatePath()}");
        }
        catch (Exception ex) { Logger.Log($"[SaveState] Auto-save failed: {ex.Message}"); }
    }

    // ---- Load ----

    public bool LoadSlot(int slot)
    {
        string path = StatePath(slot);
        if (!File.Exists(path))
        {
            Logger.Log($"[SaveState] Load slot {slot + 1} — file not found.");
            return false;
        }

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            _statable.LoadStateBinary(br);
            Logger.Log($"[SaveState] Loaded slot {slot + 1} ← {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"[SaveState] Load slot {slot + 1} failed: {ex.Message}");
            return false;
        }
    }

    public bool LoadFromActiveSlot() => LoadSlot(ActiveSlot);

    public bool AutoLoad()
    {
        string path = AutoStatePath();
        if (!File.Exists(path))
        {
            Logger.Log("[SaveState] Auto-load — no file found.");
            return false;
        }

        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            _statable.LoadStateBinary(br);
            Logger.Log($"[SaveState] Auto-load restored ← {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"[SaveState] Auto-load failed: {ex.Message}");
            return false;
        }
    }

    // ---- Info ----

    public bool HasAutoSave  => File.Exists(AutoStatePath());
    public bool SlotExists(int slot) => File.Exists(StatePath(slot));

    public SlotMeta? GetSlotMeta(int slot)
    {
        string path = MetaPath(slot);
        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<SlotMeta>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public string SlotLabel(int slot)
    {
        var meta = GetSlotMeta(slot);
        if (meta is null) return $"Slot {slot + 1}  [Empty]";
        return $"Slot {slot + 1}  {meta.Timestamp.ToLocalTime():MM/dd HH:mm}";
    }
}

public sealed class SlotMeta
{
    public DateTime Timestamp { get; set; }
}

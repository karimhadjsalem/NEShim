using NEShim.Emulation;

namespace NEShim.Saves;

internal sealed class SaveManager : ISaveManager
{
    private readonly SaveStateManager _states;
    private readonly SaveRamManager   _ram;

    public SaveManager(IEmulationCore core, string saveStateDirectory,
                       string saveRamPath, int initialActiveSlot = 0)
    {
        _states = new SaveStateManager(core, saveStateDirectory);
        _ram    = new SaveRamManager(core, saveRamPath);
        _states.ActiveSlot = initialActiveSlot;
    }

    public int SlotCount => SaveStateManager.SlotCount;

    public int ActiveSlot
    {
        get => _states.ActiveSlot;
        set => _states.ActiveSlot = value;
    }

    public bool      HasAutoSave           => _states.HasAutoSave;
    public bool      SlotExists(int slot)  => _states.SlotExists(slot);
    public SlotMeta? GetSlotMeta(int slot) => _states.GetSlotMeta(slot);

    public void SaveSlot(int slot)        => _states.SaveSlot(slot);
    public void SaveToActiveSlot()        => _states.SaveToActiveSlot();
    public void AutoSave()                => _states.AutoSave();

    public bool LoadSlot(int slot)        => _states.LoadSlot(slot);
    public bool LoadFromActiveSlot()      => _states.LoadFromActiveSlot();
    public bool AutoLoad()                => _states.AutoLoad();

    public void Startup()                 => _ram.LoadFromDisk();
    public void Shutdown()                => _ram.SaveToDisk();
}

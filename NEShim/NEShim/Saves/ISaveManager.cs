namespace NEShim.Saves;

internal interface ISaveManager
{
    int       SlotCount  { get; }
    int       ActiveSlot { get; set; }
    bool      HasAutoSave { get; }
    bool      SlotExists(int slot);
    SlotMeta? GetSlotMeta(int slot);

    void SaveSlot(int slot);
    void SaveToActiveSlot();
    void AutoSave();

    bool LoadSlot(int slot);
    bool LoadFromActiveSlot();
    bool AutoLoad();

    void Startup();
    void Shutdown();
}

using NEShim.Input;

namespace NEShim.Emulation;

internal interface IEmulationCore : IDisposable
{
    void      LoadRom(byte[] rom, string gameName = "game");
    FrameData RunFrame(InputSnapshot input);

    void SaveState(Stream destination);
    void LoadState(Stream source);

    byte[]? GetSaveRam();
    void    SetSaveRam(byte[] data);
    bool    SaveRamModified { get; }

    IReadOnlyDictionary<string, IMemoryDomain>? MemoryDomains { get; }

    string RomHash          { get; }
    int    VsyncNumerator   { get; }
    int    VsyncDenominator { get; }

    void Reset();
}

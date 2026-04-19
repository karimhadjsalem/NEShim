using System.IO;
using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Nintendo.NES;
using NEShim.Config;

namespace NEShim.Emulation;

/// <summary>
/// Owns the NES emulator instance and exposes its services.
/// </summary>
internal sealed class EmulatorHost : IDisposable
{
    private readonly NES _nes;

    public IVideoProvider  Video        { get; }
    public ISoundProvider  Sound        { get; }
    public IStatable       States       { get; }
    public ISaveRam        SaveRam      { get; }
    public NesController   Controller   { get; }
    public IMemoryDomains? MemoryDomains { get; }

    /// <summary>SHA1 hex digest of the raw ROM bytes, used to key per-game configs.</summary>
    public string RomHash { get; }

    // VSync timing from the core
    public int VsyncNumerator   => Video.VsyncNumerator;
    public int VsyncDenominator => Video.VsyncDenominator;

    private EmulatorHost(NES nes, string romHash)
    {
        _nes = nes;

        Video  = nes.ServiceProvider.GetService<IVideoProvider>()
                 ?? throw new InvalidOperationException("IVideoProvider not registered by NES core.");
        Sound  = nes.ServiceProvider.GetService<ISoundProvider>()
                 ?? throw new InvalidOperationException("ISoundProvider not registered by NES core.");
        States = nes.ServiceProvider.GetService<IStatable>()
                 ?? throw new InvalidOperationException("IStatable not registered by NES core.");
        SaveRam       = (ISaveRam)nes;
        MemoryDomains = nes.ServiceProvider.GetService<IMemoryDomains>();
        Controller    = new NesController(nes.ControllerDefinition);
        RomHash       = romHash;

        Logger.Log($"[Emulator] MemoryDomains: {(MemoryDomains is null ? "unavailable" : "available")}");
        Logger.Log("[Emulator] Core ready.");
    }

    public static EmulatorHost Load(string romPath, AppConfig config)
    {
        byte[] rom = File.ReadAllBytes(romPath);

        var fileProvider = new NeshimFileProvider();
        var glProvider   = new NullOpenGLProvider();
        var coreComm     = new CoreComm(
            showMessage:      msg => Logger.Log($"[NES] {msg}"),
            notifyMessage:    (msg, _) => Logger.Log($"[NES notify] {msg}"),
            question:         _ => null,
            coreFileProvider: fileProvider,
            prefs:            CoreComm.CorePreferencesFlags.None,
            oglProvider:      glProvider);

        var gameInfo = new GameInfo
        {
            Name          = Path.GetFileNameWithoutExtension(romPath),
            System        = "NES-NTSC",
            NotInDatabase = true,
        };

        var settings     = new NES.NESSettings();
        var syncSettings = new NES.NESSyncSettings
        {
            Controls = new NESControlSettings
            {
                NesLeftPort  = "ControllerNES",
                NesRightPort = "UnpluggedNES",
            }
        };

        Logger.Log($"[Emulator] Loading ROM: {romPath} ({rom.Length:N0} bytes)");

        var nes     = new NES(coreComm, gameInfo, rom, settings, syncSettings);
        string hash = SHA1Checksum.ComputeDigestHex(rom);

        Logger.Log($"[Emulator] ROM hash (SHA1): {hash}");
        Logger.Log($"[Emulator] VSync: {nes.VsyncNumerator}/{nes.VsyncDenominator}");

        return new EmulatorHost(nes, hash);
    }

    /// <summary>Advances emulation by one frame.</summary>
    public bool RunFrame()
    {
        return _nes.FrameAdvance(Controller, render: true, rendersound: true);
    }

    /// <summary>Hard-resets the NES (power cycle).</summary>
    public void Reset()
    {
        Logger.Log("[Emulator] Hard reset.");
        _nes.HardReset();
    }

    public void Dispose() => _nes.Dispose();
}

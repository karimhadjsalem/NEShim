using System.IO;
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

    public IVideoProvider Video { get; }
    public ISoundProvider Sound { get; }
    public IStatable States { get; }
    public ISaveRam SaveRam { get; }
    public NesController Controller { get; }

    // VSync timing from the core
    public int VsyncNumerator => Video.VsyncNumerator;
    public int VsyncDenominator => Video.VsyncDenominator;

    private EmulatorHost(NES nes)
    {
        _nes = nes;

        Video  = nes.ServiceProvider.GetService<IVideoProvider>()
                 ?? throw new InvalidOperationException("IVideoProvider not registered by NES core.");
        Sound  = nes.ServiceProvider.GetService<ISoundProvider>()
                 ?? throw new InvalidOperationException("ISoundProvider not registered by NES core.");
        States = nes.ServiceProvider.GetService<IStatable>()
                 ?? throw new InvalidOperationException("IStatable not registered by NES core.");
        SaveRam = (ISaveRam)nes;

        Controller = new NesController(nes.ControllerDefinition);
    }

    public static EmulatorHost Load(string romPath, AppConfig config)
    {
        byte[] rom = File.ReadAllBytes(romPath);

        var fileProvider = new NeshimFileProvider();
        var glProvider   = new NullOpenGLProvider();
        var coreComm     = new CoreComm(
            showMessage:      msg => System.Diagnostics.Debug.WriteLine($"[NES] {msg}"),
            notifyMessage:    (msg, _) => System.Diagnostics.Debug.WriteLine($"[NES notify] {msg}"),
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

        var nes = new NES(coreComm, gameInfo, rom, settings, syncSettings);
        return new EmulatorHost(nes);
    }

    /// <summary>Advances emulation by one frame.</summary>
    public bool RunFrame()
    {
        return _nes.FrameAdvance(Controller, render: true, rendersound: true);
    }

    public void Dispose() => _nes.Dispose();
}

using System.Diagnostics.CodeAnalysis;
using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Nintendo.NES;
using NEShim.Config;
using NEShim.Input;

namespace NEShim.Emulation;

/// <summary>
/// BizHawk-backed implementation of <see cref="IEmulationCore"/>.
/// Create the instance, then call <see cref="LoadRom"/> before any other method.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class BizHawkEmulationCore : IEmulationCore, IDisposable
{
    private readonly AppConfig _config;

    private NES          _nes         = null!;
    private IVideoProvider _video     = null!;
    private ISoundProvider _sound     = null!;
    private NesController  _controller = null!;

    public IStatable                               States        { get; private set; } = null!;
    public IReadOnlyDictionary<string, IMemoryDomain>? MemoryDomains { get; private set; }

    public string RomHash          { get; private set; } = string.Empty;
    public int    VsyncNumerator   => _video.VsyncNumerator;
    public int    VsyncDenominator => _video.VsyncDenominator;

    public bool SaveRamModified => ((ISaveRam)_nes).SaveRamModified;

    public BizHawkEmulationCore(AppConfig config)
    {
        _config = config;
    }

    public void LoadRom(byte[] rom, string gameName = "game")
    {
        _nes?.Dispose();

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
            Name          = gameName,
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
            },
            RegionOverride = _config.Region.ToUpperInvariant() switch
            {
                "PAL"   => NES.NESSyncSettings.Region.PAL,
                "NTSC"  => NES.NESSyncSettings.Region.NTSC,
                "DENDY" => NES.NESSyncSettings.Region.Dendy,
                _       => NES.NESSyncSettings.Region.Default,
            },
        };

        Logger.Log($"[Emulator] Loading ROM: {gameName} ({rom.Length:N0} bytes)");
        Logger.Log($"[Emulator] Region config: '{_config.Region}' → override={syncSettings.RegionOverride}");

        var nes = new NES(coreComm, gameInfo, rom, settings, syncSettings);

        string hash = SHA1Checksum.ComputeDigestHex(rom);
        Logger.Log($"[Emulator] ROM hash (SHA1): {hash}");
        Logger.Log($"[Emulator] Active region: {nes.Region} — VSync: {(decimal)nes.VsyncNumerator() / nes.VsyncDenominator()}");

        _nes        = nes;
        _video      = nes.ServiceProvider.GetService<IVideoProvider>()
                      ?? throw new InvalidOperationException("IVideoProvider not registered by NES core.");
        _sound      = nes.ServiceProvider.GetService<ISoundProvider>()
                      ?? throw new InvalidOperationException("ISoundProvider not registered by NES core.");
        States      = nes.ServiceProvider.GetService<IStatable>()
                      ?? throw new InvalidOperationException("IStatable not registered by NES core.");
        _controller = new NesController(nes.ControllerDefinition);
        RomHash     = hash;

        var bizHawkDomains = nes.ServiceProvider.GetService<IMemoryDomains>();
        if (bizHawkDomains is not null)
        {
            var dict = new Dictionary<string, IMemoryDomain>();
            foreach (var domain in bizHawkDomains)
                dict[domain.Name] = new BizHawkMemoryDomainAdapter(domain);
            MemoryDomains = dict;
        }
        else
        {
            MemoryDomains = null;
        }

        Logger.Log($"[Emulator] MemoryDomains: {(MemoryDomains is null ? "unavailable" : "available")}");
        Logger.Log("[Emulator] Core ready.");
    }

    public FrameData RunFrame(InputSnapshot input)
    {
        _controller.Update(input);
        _nes.FrameAdvance(_controller, render: true, rendersound: true);

        int[] videoBuffer = _video.GetVideoBuffer();
        _sound.GetSamplesSync(out short[] audioSamples, out int sampleCount);

        return new FrameData(videoBuffer, _video.BufferWidth, _video.BufferHeight,
                             audioSamples, sampleCount);
    }

    public byte[]? GetSaveRam() => ((ISaveRam)_nes).CloneSaveRam();

    public void SetSaveRam(byte[] data) => ((ISaveRam)_nes).StoreSaveRam(data);

    public void Reset()
    {
        Logger.Log("[Emulator] Hard reset.");
        _nes.HardReset();
    }

    public void Dispose() => _nes?.Dispose();

    private sealed class BizHawkMemoryDomainAdapter : IMemoryDomain
    {
        private readonly BizHawk.Emulation.Common.MemoryDomain _domain;
        internal BizHawkMemoryDomainAdapter(BizHawk.Emulation.Common.MemoryDomain domain) => _domain = domain;
        public byte PeekByte(long address) => _domain.PeekByte(address);
    }
}

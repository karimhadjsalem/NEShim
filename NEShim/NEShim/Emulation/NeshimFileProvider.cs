using BizHawk.Emulation.Common;

namespace NEShim.Emulation;

internal sealed class NeshimFileProvider : ICoreFileProvider
{
    // NEShim does not support FDS (requires BIOS) or libretro cores.
    // All firmware requests return null; path methods return empty strings.

    public byte[]? GetFirmware(FirmwareID id, string? msg = null) => null;

    public byte[] GetFirmwareOrThrow(FirmwareID id, string? msg = null)
        => throw new MissingFirmwareException(msg ?? id.ToString());

    public (byte[] FW, GameInfo Game) GetFirmwareWithGameInfoOrThrow(FirmwareID id, string? msg = null)
        => throw new MissingFirmwareException(msg ?? id.ToString());

    public string GetRetroSaveRAMDirectory(IGameInfo game) => string.Empty;

    public string GetRetroSystemPath(IGameInfo game) => string.Empty;

    public string GetUserPath(string sysID, bool temp) => string.Empty;
}

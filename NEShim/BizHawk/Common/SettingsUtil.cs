namespace BizHawk.Common;

public class SettingsUtil
{
    public static void SetDefaultValues(BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES.QuickNESSettings settings)
    {
        settings.NumSprites = 8;
        settings.ClipLeftAndRight = false;
        settings.ClipTopAndBottom = true;
    }
    
    public static void SetDefaultValues(BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES.QuickNESSyncSettings settings)
    {
        settings.Port1 = BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES.Port1PeripheralOption.Gamepad;
        settings.Port2 = BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES.Port2PeripheralOption.Unplugged;
    }
}
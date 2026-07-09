namespace NEShim.Input;

/// <summary>
/// Cardinal-mode analog stick helpers shared by SDL3GamepadSource (gameplay) and
/// SDL3GamepadSource.GetMenuNav (menu navigation).
/// Each method returns true only when its axis is the dominant one, preventing
/// accidental diagonal NES inputs when the stick is pushed diagonally.
/// </summary>
internal static class AnalogStickHelper
{
    public static bool StickUp(int lx, int ly, int dz)    => ly >  dz && Math.Abs(ly) >= Math.Abs(lx);
    public static bool StickDown(int lx, int ly, int dz)  => ly < -dz && Math.Abs(ly) >= Math.Abs(lx);
    public static bool StickLeft(int lx, int ly, int dz)  => lx < -dz && Math.Abs(lx) >  Math.Abs(ly);
    public static bool StickRight(int lx, int ly, int dz) => lx >  dz && Math.Abs(lx) >  Math.Abs(ly);
}

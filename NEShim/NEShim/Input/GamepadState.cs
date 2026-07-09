namespace NEShim.Input;

internal struct GamepadState
{
    public bool DPadUp, DPadDown, DPadLeft, DPadRight;
    public bool Start, Back;
    public bool LeftShoulder, RightShoulder;
    public bool LeftThumb, RightThumb;
    public bool A, B, X, Y;
    public short ThumbLX, ThumbLY;
    public bool Connected;

    public bool GetButton(string? buttonName) => buttonName switch
    {
        "DPadUp"        => DPadUp,
        "DPadDown"      => DPadDown,
        "DPadLeft"      => DPadLeft,
        "DPadRight"     => DPadRight,
        "Start"         => Start,
        "Back"          => Back,
        "LeftShoulder"  => LeftShoulder,
        "RightShoulder" => RightShoulder,
        "LeftThumb"     => LeftThumb,
        "RightThumb"    => RightThumb,
        "A"             => A,
        "B"             => B,
        "X"             => X,
        "Y"             => Y,
        _               => false,
    };
}

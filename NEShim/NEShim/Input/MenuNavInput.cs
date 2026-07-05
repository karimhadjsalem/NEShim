namespace NEShim.Input;

/// <summary>
/// Edge-triggered menu navigation intent for one poll interval.
/// All fields are true only on the frame the action first becomes active.
/// </summary>
internal readonly struct MenuNavInput
{
    public bool Up      { get; init; }
    public bool Down    { get; init; }
    public bool Left    { get; init; }
    public bool Right   { get; init; }
    public bool Confirm { get; init; }
    public bool Back    { get; init; }

    public bool Any => Up || Down || Left || Right || Confirm || Back;

    public static MenuNavInput Union(MenuNavInput a, MenuNavInput b) => new()
    {
        Up      = a.Up      || b.Up,
        Down    = a.Down    || b.Down,
        Left    = a.Left    || b.Left,
        Right   = a.Right   || b.Right,
        Confirm = a.Confirm || b.Confirm,
        Back    = a.Back    || b.Back,
    };
}

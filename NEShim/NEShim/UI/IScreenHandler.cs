namespace NEShim.UI;

/// <summary>
/// Common contract implemented by both the menu-specific nested <c>ScreenHandler</c> base classes
/// (<see cref="InGameMenu"/>'s and <see cref="MainMenuScreen"/>'s own, for handlers that need full
/// access to their owning menu) and <see cref="SharedScreenHandler"/> (for handlers whose logic is
/// identical between the two menus, typed against <see cref="IMenuHost"/> instead). Lets both
/// menus keep a single <c>Dictionary&lt;Screen, IScreenHandler&gt;</c> regardless of which handler
/// shape backs a given screen.
/// </summary>
internal interface IScreenHandler
{
    string   Title     { get; }
    int      ItemCount { get; }
    string[] GetItems();
    void     Activate(int index);
    bool            IsItemEnabled(int index);
    IntPtr          GetItemIcon(int index);
    IntPtr          GetItemValueIcon(int index);
    SliderItemData? GetSliderData(int index);
    bool            IsConfirmStyle { get; }
    bool            ShowsControllerDiagram { get; }

    /// <summary>
    /// The NES button config key the controller diagram should highlight for the given selected
    /// row, or null if that row isn't a real NES button (e.g. Back/OpenMenu, or a non-binding
    /// screen). Overridden only by the gamepad/keyboard binding handlers — every other screen
    /// keeps the default. Lets <see cref="InGameMenu.ActiveNesButton"/>/<see cref="MainMenuScreen"/>'s
    /// equivalent dispatch polymorphically through whichever handler is active instead of
    /// switching on <see cref="Screen"/> values, so adding a new binding screen never requires
    /// touching either menu class.
    /// </summary>
    string? GetActiveNesButton(int selectedItem);

    /// <summary>
    /// Row index a visual divider should be drawn before, or -1 for no divider. Generalizes what
    /// was previously a single hardcoded case (the OpenMenu row's own "System" divider on the P1
    /// Gamepad Bindings screen, still driven separately by <c>OpenMenuBindingIndex</c> — see
    /// <see cref="MenuRenderer"/>/<see cref="MainMenuRenderer"/>) so other handlers (e.g.
    /// <see cref="PlayerSelectHandler"/>'s gamepad/keyboard grouping) can request the same visual
    /// treatment without the renderer knowing which screen wants it.
    /// </summary>
    int SeparatorIndex { get; }

    /// <summary>Optional caption drawn under the <see cref="SeparatorIndex"/> divider, or null for
    /// a bare line with no label.</summary>
    string? SeparatorLabel { get; }
}

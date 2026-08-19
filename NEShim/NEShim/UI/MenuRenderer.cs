using NEShim.Platform;
using NEShim.Rendering;
using NEShim.UI.Controls;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the in-game pause menu overlay.
/// Draws on top of the frozen game frame using SDL3PaintContext. Per-row/per-element drawing
/// (item rows, sliders, the controller diagram, panel chrome) is delegated to the shared,
/// independently-testable components in NEShim.UI.Controls — this class owns only the
/// in-game-specific panel layout/orchestration around them.
/// </summary>
internal static class MenuRenderer
{
    private static readonly SDL.Color OverlayColor  = new() { R =  0, G =  0, B =  0, A = 180 };
    private static readonly SDL.Color PanelColor    = new() { R = 18, G = 18, B = 32, A = 230 };
    private static readonly SDL.Color SelectedBg    = new() { R = 55, G = 110, B = 195, A = 210 };
    private static readonly SDL.Color TitleColor    = new() { R = 175, G = 215, B = 255, A = 255 };
    private static readonly SDL.Color SubtitleColor = new() { R = 255, G = 200, B = 100, A = 255 };
    private static readonly SDL.Color WarningColor  = new() { R = 255, G = 120, B =  60, A = 255 };
    private static readonly SDL.Color ItemColor     = new() { R = 255, G = 255, B = 255, A = 255 };
    private static readonly SDL.Color DimColor      = new() { R = 190, G = 190, B = 190, A = 170 };
    private static readonly SDL.Color BorderColor   = new() { R =  75, G = 135, B = 215, A = 200 };
    private static readonly SDL.Color WarningBorder = new() { R = 200, G =  90, B =  40, A = 200 };
    private static readonly SDL.Color AmberColor    = new() { R = 255, G = 220, B = 140, A = 255 };

    // In-game menu items are 4px shorter than main-menu items (38 vs 42) — the in-game
    // overlay is intentionally more compact so the frozen game frame stays visible.
    // On Steam Deck all layout constants scale up so the menus remain readable at handheld distance.
    // Properties, not static readonly fields: they depend on MenuScale.Scale, which changes
    // live on window resize/fullscreen toggle (see MenuScale's doc comment). A static readonly
    // field would bake in whatever scale was active the first time this class was touched and
    // never update again, desyncing spacing from the font sizes computed inline in Draw() below
    // (which do re-read MenuScale.Scale every frame) — reproduced on both Windows and Linux as
    // items overflowing off-screen in windowed mode and overlapping after returning to fullscreen.
    internal static int ItemH        => S(38);
    private  static int PanelPad     => S(16);
    private  static int SeparatorH   => S(18);
    private  static int PanelHeaderH => S(64);
    private  static int ItemsStartY  => S(56);

    private const int ControllerAreaW = MenuRenderConstants.ControllerAreaW;
    private const int FullPanelW      = MenuRenderConstants.FullPanelW;
    private const int SlimPanelW      = MenuRenderConstants.SlimPanelW;
    private const int MinWidthForCtrl = MenuRenderConstants.MinWidthForCtrl;

    private const int DisconnectPanelW = 400;
    private const int DisconnectPanelH = 110;
    private const int DisconnectTitleY = 12;
    private const int DisconnectTitleH = 40;
    private const int DisconnectHintY  = 62;
    private const int DisconnectHintH  = 36;

    // ---- Drawing ----

    public static void Draw(SDL3PaintContext ctx, SDL.Rect bounds, InGameMenu menu)
    {
        ctx.FillRect(ToFRect(bounds), OverlayColor);

        if (menu.Current == Screen.ControllerDisconnected)
        {
            DrawDisconnectScreen(ctx, bounds, menu);
            return;
        }

        var    items        = menu.GetCurrentItems();
        string title        = menu.GetTitle();
        bool   isConfirm    = menu.IsConfirmStyle;
        int    warningRowH  = isConfirm ? ItemH : 0;
        int    openMenuIdx  = menu.Current == Screen.GamepadBindings
                              ? menu.OpenMenuBindingIndex : -1;
        // Generic handler-driven divider (e.g. PlayerSelectHandler's gamepad/keyboard grouping) —
        // mutually exclusive with the OpenMenu row's own divider, which stays driven separately
        // by OpenMenuBindingIndex/menu.Localization.SystemSectionLabel below.
        int    sepIdx       = openMenuIdx >= 0 ? openMenuIdx : menu.GetCurrentSeparatorIndex();
        string? sepLabel    = openMenuIdx >= 0 ? menu.Localization.SystemSectionLabel : menu.GetCurrentSeparatorLabel();
        bool   hasSeparator = sepIdx >= 0;
        bool   showCtrl     = ShouldShowController(bounds, menu);

        var (panelX, panelY, panelW, panelH, listW) = PanelMetrics(bounds, items.Length, warningRowH, hasSeparator, showCtrl);
        var panelFRect = new SDL.FRect { X = panelX, Y = panelY, W = panelW, H = panelH };

        PanelFrameControl.Draw(ctx, panelFRect, PanelColor, isConfirm ? WarningBorder : BorderColor);

        // Title
        var   titleColor = isConfirm                                          ? WarningColor
                         : (menu.RebindingAction != null || menu.IsGamepadRebinding) ? SubtitleColor
                         : TitleColor;
        var   titleRect  = new SDL.FRect { X = panelX + PanelPad, Y = panelY + S(10), W = panelW - PanelPad * 2, H = S(36) };
        ctx.DrawText(title, titleRect, titleColor, menu.Localization.FontFamily, 15f * MenuScale.Scale, bold: true);

        // Divider
        ctx.DrawLine(panelX + PanelPad, panelY + S(50), panelX + panelW - PanelPad, panelY + S(50),
            new SDL.Color { R = 255, G = 255, B = 255, A = 70 });

        // Warning label on confirm screens
        if (isConfirm)
        {
            var warnRect = new SDL.FRect { X = panelX + PanelPad, Y = panelY + S(52), W = panelW - PanelPad * 2, H = S(28) };
            ctx.DrawText(menu.Localization.InGameConfirmWarning, warnRect,
                new SDL.Color { R = 255, G = 180, B = 100, A = 200 },
                menu.Localization.FontFamily, 11f * MenuScale.Scale, bold: false, italic: true);
        }

        // Controller diagram on the right side of binding screens
        if (showCtrl)
        {
            // Bounded to the content region (same as where the item list itself starts/ends,
            // ItemsStartY/PanelPad) rather than the raw panel edges — using unscaled literal
            // offsets here previously let this line start above the title's own scaled Y
            // (S(10)), so it ran through the header band and visibly cut through the title text.
            int contentTop    = panelY + ItemsStartY;
            int contentBottom = panelY + panelH - PanelPad;
            ctx.DrawLine(panelX + listW, contentTop, panelX + listW, contentBottom,
                new SDL.Color { R = 255, G = 255, B = 255, A = 50 });

            var ctrlArea = new SDL.FRect { X = panelX + listW + 6, Y = contentTop, W = panelW - listW - 10, H = contentBottom - contentTop };
            string ctrlLabel = MenuBindingHelpers.ControllerDiagramLabel(
                menu.Localization, MenuBindingHelpers.PlayerForBindingScreen(menu.Current));
            ControllerDiagramControl.Draw(ctx, ctrlArea, menu.ActiveNesButton, ctrlLabel, menu.Localization.FontFamily, MenuScale.Scale);
        }

        // Rebind prompt (left portion)
        if (menu.RebindingAction != null || menu.IsGamepadRebinding)
        {
            string hint = menu.IsGamepadRebinding
                ? (menu.OverrideStartBindingProtection
                    ? menu.Localization.InGameRebindPressButtonNoCancel
                    : menu.Localization.InGameRebindPressButton)
                : menu.Localization.InGameRebindPressKey;
            var hintRect = new SDL.FRect { X = panelX + PanelPad, Y = panelY + ItemsStartY, W = listW - PanelPad * 2, H = panelH - ItemsStartY - PanelPad };
            ctx.DrawText(hint, hintRect,
                new SDL.Color { R = 255, G = 255, B = 180, A = 220 },
                menu.Localization.FontFamily, 13f * MenuScale.Scale, bold: false, italic: true);
            return;
        }

        // Item list (left portion)
        var sliderItems = new SliderItemData?[items.Length];
        for (int i = 0; i < items.Length; i++)
            sliderItems[i] = menu.GetCurrentSliderData(i);
        float sliderLabelColumnW = SliderControl.ComputeLabelColumnW(ctx, sliderItems, menu.Localization.FontFamily, MenuScale.Scale);
        for (int i = 0; i < items.Length; i++)
        {
            if (hasSeparator && i == sepIdx)
            {
                int sepLineY = panelY + ItemsStartY + warningRowH + i * ItemH + 2;
                ctx.DrawLine(panelX + PanelPad, sepLineY, panelX + listW - PanelPad, sepLineY,
                    new SDL.Color { R = 255, G = 255, B = 255, A = 70 });
                if (sepLabel != null)
                {
                    var sepRect = new SDL.FRect { X = panelX + PanelPad, Y = sepLineY + 3, W = listW - PanelPad * 2, H = S(12) };
                    ctx.DrawText(sepLabel, sepRect,
                        new SDL.Color { R = 180, G = 180, B = 180, A = 140 },
                        menu.Localization.FontFamily, 8f * MenuScale.Scale, bold: false,
                        TextHAlign.Near, TextVAlign.Top);
                }
            }

            int extraY = hasSeparator && i >= sepIdx ? SeparatorH : 0;
            var itemRect = new SDL.Rect
            {
                X = panelX + 6,
                Y = panelY + ItemsStartY + warningRowH + i * ItemH + extraY,
                W = listW - 12,
                H = ItemH - 2,
            };

            bool            enabled    = menu.IsItemEnabled(i);
            bool            selected   = i == menu.SelectedItem && enabled;
            bool            isOpenMenu = openMenuIdx >= 0 && i == openMenuIdx;
            var             textColor  = isOpenMenu ? AmberColor : (enabled ? ItemColor : DimColor);
            SliderItemData? sliderData = sliderItems[i];
            IntPtr          icon       = menu.GetCurrentItemIcon(i);
            IntPtr          valueIcon  = menu.GetCurrentItemValueIcon(i);

            if (sliderData.HasValue)
            {
                if (selected) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                SliderControl.Draw(ctx, sliderData.Value, itemRect, textColor, menu.Localization.FontFamily, selected, sliderLabelColumnW, MenuScale.Scale);
            }
            else if (icon != IntPtr.Zero)
            {
                if (selected) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                IconRowControl.Draw(ctx, icon, items[i], selected, itemRect, textColor, menu.Localization.FontFamily, MenuScale.Scale);
            }
            else if (selected)
            {
                ctx.FillRect(ToFRect(itemRect), SelectedBg);
                ItemRowControl.Draw(ctx, items[i], itemRect, textColor, menu.Localization.FontFamily, bold: true, selected: true, valueIcon, MenuScale.Scale);
            }
            else if (enabled)
            {
                ItemRowControl.Draw(ctx, items[i], itemRect, textColor, menu.Localization.FontFamily, bold: false, selected: false, valueIcon, MenuScale.Scale);
            }
            else
            {
                ItemRowControl.Draw(ctx, items[i], itemRect, DimColor, menu.Localization.FontFamily, bold: false, selected: false, valueIcon, MenuScale.Scale);
            }
        }
    }

    // ---- Private helpers ----

    private static void DrawDisconnectScreen(SDL3PaintContext ctx, SDL.Rect bounds, InGameMenu menu)
    {
        int panelW = MenuRenderConstants.ScaledPanelW(DisconnectPanelW, bounds.W);
        int panelH = S(DisconnectPanelH);
        int panelX = Math.Max(8, (bounds.W - panelW) / 2);
        int panelY = Math.Max(8, (bounds.H - panelH) / 2);
        var panelFRect = new SDL.FRect { X = panelX, Y = panelY, W = panelW, H = panelH };

        PanelFrameControl.Draw(ctx, panelFRect, PanelColor, WarningBorder);

        var titleRect = new SDL.FRect { X = panelX, Y = panelY + S(DisconnectTitleY), W = panelW, H = S(DisconnectTitleH) };
        ctx.DrawText(menu.Localization.ControllerDisconnectedTitle, titleRect, WarningColor,
            menu.Localization.FontFamily, 15f * MenuScale.Scale, bold: true);

        var hintRect = new SDL.FRect { X = panelX, Y = panelY + S(DisconnectHintY), W = panelW, H = S(DisconnectHintH) };
        ctx.DrawText(menu.Localization.ControllerDisconnectedHint, hintRect, DimColor,
            menu.Localization.FontFamily, 11f * MenuScale.Scale, bold: false, italic: true);
    }

    private static bool ShouldShowController(SDL.Rect bounds, InGameMenu menu) =>
        bounds.W >= MinWidthForCtrl && menu.ShowsControllerDiagram;

    private static (int panelX, int panelY, int panelW, int panelH, int listW) PanelMetrics(
        SDL.Rect bounds, int itemCount, int warningRowH, bool hasSeparator, bool showCtrl)
    {
        int ctrlAreaW = MenuRenderConstants.ScaledPanelW(ControllerAreaW, bounds.W);
        int panelW = showCtrl
            ? MenuRenderConstants.ScaledPanelW(FullPanelW, bounds.W)
            : MenuRenderConstants.ScaledPanelW(SlimPanelW, bounds.W);
        int listW  = showCtrl ? panelW - ctrlAreaW : panelW;
        int panelH = PanelHeaderH + warningRowH + itemCount * ItemH + PanelPad + (hasSeparator ? SeparatorH : 0);
        int panelX = Math.Max(8, (bounds.W - panelW) / 2);
        int panelY = Math.Max(8, (bounds.H - panelH) / 2);
        return (panelX, panelY, panelW, panelH, listW);
    }

    private static SDL.FRect ToFRect(SDL.Rect r) => new() { X = r.X, Y = r.Y, W = r.W, H = r.H };

    private static int S(int value) => (int)Math.Round(value * MenuScale.Scale);
}

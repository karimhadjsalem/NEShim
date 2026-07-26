using NEShim.Platform;
using NEShim.Rendering;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the in-game pause menu overlay.
/// Draws on top of the frozen game frame using SDL3PaintContext.
/// </summary>
internal static class MenuRenderer
{
    private static readonly SDL.Color OverlayColor  = new() { R =  0, G =  0, B =  0, A = 180 };
    private static readonly SDL.Color PanelColor    = new() { R = 18, G = 18, B = 32, A = 230 };
    private static readonly SDL.Color SelectedBg    = new() { R = 55, G = 110, B = 195, A = 210 };
    private static readonly SDL.Color AccentBar     = new() { R = 160, G = 200, B = 255, A = 255 };
    private static readonly SDL.Color BarFill       = new() { R =  80, G = 140, B = 240, A = 255 };
    private static readonly SDL.Color BarEmpty      = new() { R =  35, G =  35, B =  55, A = 220 };
    private static readonly SDL.Color TitleColor    = new() { R = 175, G = 215, B = 255, A = 255 };
    private static readonly SDL.Color SubtitleColor = new() { R = 255, G = 200, B = 100, A = 255 };
    private static readonly SDL.Color WarningColor  = new() { R = 255, G = 120, B =  60, A = 255 };
    private static readonly SDL.Color ItemColor     = new() { R = 255, G = 255, B = 255, A = 255 };
    private static readonly SDL.Color DimColor      = new() { R = 190, G = 190, B = 190, A = 170 };
    private static readonly SDL.Color BorderColor   = new() { R =  75, G = 135, B = 215, A = 200 };
    private static readonly SDL.Color WarningBorder = new() { R = 200, G =  90, B =  40, A = 200 };
    private static readonly SDL.Color AmberColor    = new() { R = 255, G = 220, B = 140, A = 255 };

    // Left indent that every item text observes, selected or not (keeps text column stable).
    private const float ItemTextIndent = 13f;

    // In-game menu items are 4px shorter than main-menu items (38 vs 42) — the in-game
    // overlay is intentionally more compact so the frozen game frame stays visible.
    // On Steam Deck all layout constants scale up so the menus remain readable at handheld distance.
    internal static readonly int ItemH        = S(38);
    private  static readonly int PanelPad     = S(16);
    private  static readonly int SeparatorH   = S(18);
    private  static readonly int PanelHeaderH = S(64);
    private  static readonly int ItemsStartY  = S(56);

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

        if (menu.Current == InGameMenu.Screen.ControllerDisconnected)
        {
            DrawDisconnectScreen(ctx, bounds, menu);
            return;
        }

        var    items        = menu.GetCurrentItems();
        string title        = menu.GetTitle();
        bool   isConfirm    = menu.Current == InGameMenu.Screen.ConfirmMainMenu
                           || menu.Current == InGameMenu.Screen.ConfirmExit;
        int    warningRowH  = isConfirm ? ItemH : 0;
        int    openMenuIdx  = menu.Current == InGameMenu.Screen.GamepadBindings
                              ? menu.OpenMenuBindingIndex : -1;
        bool   hasSeparator = openMenuIdx >= 0;
        bool   showCtrl     = ShouldShowController(bounds, menu.Current);

        var (panelX, panelY, panelW, panelH, listW) = PanelMetrics(bounds, items.Length, warningRowH, hasSeparator, showCtrl);
        var panelFRect = new SDL.FRect { X = panelX, Y = panelY, W = panelW, H = panelH };

        ctx.FillRect(panelFRect, PanelColor);
        ctx.DrawRect(panelFRect, isConfirm ? WarningBorder : BorderColor, 2f);

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
            ctx.DrawLine(panelX + listW, panelY + 8, panelX + listW, panelY + panelH - 8,
                new SDL.Color { R = 255, G = 255, B = 255, A = 50 });

            var ctrlArea = new SDL.FRect { X = panelX + listW + 6, Y = panelY + 14, W = panelW - listW - 10, H = panelH - 28 };
            DrawControllerSprite(ctx, ctrlArea, menu.ActiveNesButton, menu.Localization.NesControllerLabel, menu.Localization.FontFamily);
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
        float sliderLabelColumnW = ComputeSliderLabelColumnW(ctx, menu, items.Length, MenuScale.Scale);
        for (int i = 0; i < items.Length; i++)
        {
            if (hasSeparator && i == openMenuIdx)
            {
                int sepLineY = panelY + ItemsStartY + warningRowH + i * ItemH + 2;
                ctx.DrawLine(panelX + PanelPad, sepLineY, panelX + listW - PanelPad, sepLineY,
                    new SDL.Color { R = 255, G = 255, B = 255, A = 70 });
                var sepRect = new SDL.FRect { X = panelX + PanelPad, Y = sepLineY + 3, W = listW - PanelPad * 2, H = S(12) };
                ctx.DrawText(menu.Localization.SystemSectionLabel, sepRect,
                    new SDL.Color { R = 180, G = 180, B = 180, A = 140 },
                    menu.Localization.FontFamily, 8f * MenuScale.Scale, bold: false,
                    TextHAlign.Near, TextVAlign.Top);
            }

            int extraY = hasSeparator && i >= openMenuIdx ? SeparatorH : 0;
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
            SliderItemData? sliderData = menu.GetCurrentSliderData(i);
            IntPtr          icon       = menu.GetCurrentItemIcon(i);

            if (sliderData.HasValue)
            {
                if (selected) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                DrawSliderItem(ctx, sliderData.Value, itemRect, textColor, menu.Localization.FontFamily, selected, sliderLabelColumnW);
            }
            else if (icon != IntPtr.Zero)
            {
                if (selected) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                DrawItemWithIcon(ctx, icon, items[i], selected, enabled, itemRect, textColor, menu.Localization.FontFamily);
            }
            else if (selected)
            {
                ctx.FillRect(ToFRect(itemRect), SelectedBg);
                DrawItemRow(ctx, items[i], itemRect, textColor, menu.Localization.FontFamily, bold: true, selected: true);
            }
            else if (enabled)
            {
                DrawItemRow(ctx, items[i], itemRect, textColor, menu.Localization.FontFamily, bold: false, selected: false);
            }
            else
            {
                DrawItemRow(ctx, items[i], itemRect, DimColor, menu.Localization.FontFamily, bold: false, selected: false);
            }
        }
    }

    // ---- Private helpers ----

    private static void DrawDisconnectScreen(SDL3PaintContext ctx, SDL.Rect bounds, InGameMenu menu)
    {
        int panelW = MenuRenderConstants.PanelW(DisconnectPanelW, bounds.W);
        int panelH = S(DisconnectPanelH);
        int panelX = Math.Max(8, (bounds.W - panelW) / 2);
        int panelY = Math.Max(8, (bounds.H - panelH) / 2);
        var panelFRect = new SDL.FRect { X = panelX, Y = panelY, W = panelW, H = panelH };

        ctx.FillRect(panelFRect, PanelColor);
        ctx.DrawRect(panelFRect, WarningBorder, 2f);

        var titleRect = new SDL.FRect { X = panelX, Y = panelY + S(DisconnectTitleY), W = panelW, H = S(DisconnectTitleH) };
        ctx.DrawText(menu.Localization.ControllerDisconnectedTitle, titleRect, WarningColor,
            menu.Localization.FontFamily, 15f * MenuScale.Scale, bold: true);

        var hintRect = new SDL.FRect { X = panelX, Y = panelY + S(DisconnectHintY), W = panelW, H = S(DisconnectHintH) };
        ctx.DrawText(menu.Localization.ControllerDisconnectedHint, hintRect, DimColor,
            menu.Localization.FontFamily, 11f * MenuScale.Scale, bold: false, italic: true);
    }

    private static bool ShouldShowController(SDL.Rect bounds, InGameMenu.Screen screen) =>
        bounds.W >= MinWidthForCtrl
        && (screen == InGameMenu.Screen.KeyboardBindings
            || screen == InGameMenu.Screen.GamepadBindings);

    private static (int panelX, int panelY, int panelW, int panelH, int listW) PanelMetrics(
        SDL.Rect bounds, int itemCount, int warningRowH, bool hasSeparator, bool showCtrl)
    {
        int ctrlAreaW = MenuRenderConstants.ScaledPanelW(ControllerAreaW, bounds.W);
        int panelW = showCtrl
            ? MenuRenderConstants.ScaledPanelW(FullPanelW, bounds.W)
            : MenuRenderConstants.PanelW(SlimPanelW, bounds.W);
        int listW  = showCtrl ? panelW - ctrlAreaW : panelW;
        int panelH = PanelHeaderH + warningRowH + itemCount * ItemH + PanelPad + (hasSeparator ? SeparatorH : 0);
        int panelX = Math.Max(8, (bounds.W - panelW) / 2);
        int panelY = Math.Max(8, (bounds.H - panelH) / 2);
        return (panelX, panelY, panelW, panelH, listW);
    }

    private const float ControllerAspect = 2.43f;

    private static void DrawControllerSprite(SDL3PaintContext ctx, SDL.FRect area, string? activeButton, string label, string fontFamily)
    {
        float ctrlW = area.W;
        float ctrlH = ctrlW / ControllerAspect;
        if (ctrlH > area.H) { ctrlH = area.H; ctrlW = ctrlH * ControllerAspect; }
        float ox = area.X + (area.W - ctrlW) * 0.5f;
        float oy = area.Y + (area.H - ctrlH) * 0.5f;
        var ctrlRect = new SDL.Rect { X = (int)ox, Y = (int)oy, W = (int)ctrlW, H = (int)ctrlH };
        ctx.BlitSurface(ControllerSprites.Base, null, ctrlRect);
        ControllerSprites.DrawHighlight(ctx, ctrlRect, activeButton);

        float labelGap = oy - area.Y;
        if (labelGap >= 12f)
        {
            float fontSize = Math.Min(14f, labelGap * 0.75f) * MenuScale.Scale;
            var labelRect  = new SDL.FRect { X = area.X, Y = area.Y, W = area.W, H = labelGap };
            ctx.DrawText(label, labelRect,
                new SDL.Color { R = 200, G = 210, B = 230, A = 180 },
                fontFamily, fontSize, bold: true);
        }
    }

    private const int IconW   = 20;
    private const int IconH   = 14;
    private const int IconGap = 4;

    private static void DrawItemWithIcon(
        SDL3PaintContext ctx, IntPtr icon, string text, bool selected, bool enabled,
        SDL.Rect itemRect, SDL.Color textColor, string fontFamily)
    {
        int iconW = S(IconW);
        int iconH = S(IconH);
        int iconX = itemRect.X + S(2);
        int iconY = itemRect.Y + (itemRect.H - iconH) / 2;
        ctx.BlitSurface(icon, null, new SDL.Rect { X = iconX, Y = iconY, W = iconW, H = iconH });

        int textOffsetX = S(IconW + IconGap);
        var textRect    = new SDL.FRect { X = itemRect.X + textOffsetX, Y = itemRect.Y, W = itemRect.W - textOffsetX, H = itemRect.H };
        ctx.DrawText(text, textRect, enabled ? textColor : DimColor,
            fontFamily, 12f * MenuScale.Scale, selected, TextHAlign.Near, TextVAlign.Center);
    }

    private static void DrawItemRow(SDL3PaintContext ctx, string text, SDL.Rect itemRect,
        SDL.Color color, string fontFamily, bool bold, bool selected)
    {
        if (selected)
        {
            var bar = new SDL.FRect { X = itemRect.X + 4f, Y = itemRect.Y + 5f, W = 3f, H = itemRect.H - 10f };
            ctx.FillRect(bar, AccentBar);
        }
        var textFRect = new SDL.FRect
        {
            X = itemRect.X + ItemTextIndent,
            Y = itemRect.Y,
            W = itemRect.W - ItemTextIndent,
            H = itemRect.H,
        };
        ctx.DrawText(text, textFRect, color, fontFamily, 12f * MenuScale.Scale, bold,
            TextHAlign.Near, TextVAlign.Center, tabStop: S(120));
    }

    private static void DrawSliderItem(SDL3PaintContext ctx, SliderItemData data, SDL.Rect itemRect,
        SDL.Color textColor, string fontFamily, bool selected, float labelColumnW)
    {
        if (selected)
        {
            var accentLine = new SDL.FRect { X = itemRect.X + 4f, Y = itemRect.Y + 5f, W = 3f, H = itemRect.H - 10f };
            ctx.FillRect(accentLine, AccentBar);
        }

        float scale    = MenuScale.Scale;
        float contentX = itemRect.X + ItemTextIndent;
        float contentW = itemRect.W - ItemTextIndent;
        float labelW   = labelColumnW;
        float valueW   =  48f * scale;
        float barGap   =   6f * scale;
        float barH     =   8f * scale;
        float barX     = contentX + labelW + barGap;
        float barW     = contentW - labelW - barGap * 2f - valueW;
        float barY     = itemRect.Y + (itemRect.H - barH) * 0.5f;

        ctx.DrawText(data.Label, new SDL.FRect { X = contentX, Y = itemRect.Y, W = labelW, H = itemRect.H },
            textColor, fontFamily, 12f * scale, bold: false, TextHAlign.Near, TextVAlign.Center);

        if (barW > 0)
        {
            ctx.FillRect(new SDL.FRect { X = barX, Y = barY, W = barW, H = barH }, BarEmpty);
            float fillW = Math.Clamp(data.Fill01, 0f, 1f) * barW;
            if (fillW > 0)
                ctx.FillRect(new SDL.FRect { X = barX, Y = barY, W = fillW, H = barH }, BarFill);
        }

        float valueX = barX + Math.Max(0f, barW) + barGap;
        ctx.DrawText(data.ValueText, new SDL.FRect { X = valueX, Y = itemRect.Y, W = valueW, H = itemRect.H },
            textColor, fontFamily, 11f * scale, bold: false, TextHAlign.Far, TextVAlign.Center);
    }

    // Measures all slider labels on the current screen and returns the column width wide enough
    // to fit the longest label (with a small padding margin). All bars on the screen start at
    // the same X position, keeping the layout stable as the user navigates.
    private static float ComputeSliderLabelColumnW(SDL3PaintContext ctx, InGameMenu menu, int itemCount, float scale)
    {
        float maxLabelW = 0f;
        for (int i = 0; i < itemCount; i++)
        {
            var slider = menu.GetCurrentSliderData(i);
            if (!slider.HasValue) continue;
            var (w, _) = ctx.MeasureText(slider.Value.Label, menu.Localization.FontFamily, 12f * scale, bold: false);
            maxLabelW = Math.Max(maxLabelW, w);
        }
        const float MinColumnW   = 80f;
        const float LabelPadding = 10f;
        return Math.Max(MinColumnW * scale, maxLabelW + LabelPadding * scale);
    }

    private static SDL.FRect ToFRect(SDL.Rect r) => new() { X = r.X, Y = r.Y, W = r.W, H = r.H };

    private static int S(int value) => (int)Math.Round(value * MenuScale.Scale);
}

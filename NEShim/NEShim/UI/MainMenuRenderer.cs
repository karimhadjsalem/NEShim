using NEShim.Platform;
using NEShim.Rendering;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the pre-game main menu and all its sub-screens.
/// Main screen: background image + panel anchored per <c>MainMenuScreen.MenuPosition</c>.
/// Sub-screens (Settings, Video, Sound, etc.): centred panel over dimmed background.
/// </summary>
internal static class MainMenuRenderer
{
    private static readonly SDL.Color BgFallback  = new() { R =  12, G =  12, B =  24, A = 255 };
    private static readonly SDL.Color OverlayDim  = new() { R =   0, G =   0, B =   0, A = 130 };
    private static readonly SDL.Color SubDim      = new() { R =   0, G =   0, B =   0, A = 175 };
    private static readonly SDL.Color PanelColor  = new() { R =  16, G =  16, B =  30, A = 225 };
    private static readonly SDL.Color BorderColor = new() { R =  70, G = 130, B = 210, A = 200 };
    private static readonly SDL.Color TitleColor  = new() { R = 195, G = 225, B = 255, A = 255 };
    private static readonly SDL.Color RebindColor = new() { R = 255, G = 200, B = 100, A = 255 };
    private static readonly SDL.Color ItemOn      = new() { R = 255, G = 255, B = 255, A = 255 };
    private static readonly SDL.Color ItemDim     = new() { R = 160, G = 160, B = 160, A = 110 };
    private static readonly SDL.Color SelectedBg  = new() { R =  50, G = 105, B = 190, A = 210 };
    private static readonly SDL.Color AccentBar   = new() { R = 160, G = 200, B = 255, A = 255 };
    private static readonly SDL.Color BarFill     = new() { R =  80, G = 140, B = 240, A = 255 };
    private static readonly SDL.Color BarEmpty    = new() { R =  35, G =  35, B =  55, A = 220 };
    private static readonly SDL.Color AmberColor  = new() { R = 255, G = 220, B = 140, A = 255 };

    // Left indent that every item text observes, selected or not (keeps text column stable).
    private const float ItemTextIndent = 13f;

    // Main-menu items are 4px taller than in-game menu items (42 vs 38) — the main menu
    // is a full-screen overlay with more visual breathing room.
    // On Steam Deck all layout constants scale up so the menus remain readable at handheld distance.
    private static readonly int ItemH           = S(42);
    private static readonly int Pad             = S(14);
    private static readonly int SeparatorH      = S(18);
    private static readonly int PanelHeaderH    = S(52);
    private static readonly int ItemListStartY  = S(50);
    private static readonly int TitleRectH      = S(36);
    private static readonly int DividerY        = S(46);
    private static readonly int SeparatorLabelH = S(12);

    private const int Margin          = 40;
    private const int TitleYOffset    = 8;
    private const int MainPanelMaxW   = 360;
    private const int RebindPanelMaxW = 400;
    private const int RebindPanelH    = 120;

    private const int ControllerAreaW = MenuRenderConstants.ControllerAreaW;
    private const int FullPanelW      = MenuRenderConstants.FullPanelW;
    private const int SlimPanelW      = MenuRenderConstants.SlimPanelW;
    private const int MinWidthForCtrl = MenuRenderConstants.MinWidthForCtrl;

    // ---- Panel positioning ----

    /// <summary>
    /// Computes the main-screen panel <see cref="SDL.Rect"/> from <paramref name="position"/>.
    /// Supported values: BottomCenter, Center, BottomLeft, BottomRight, TopLeft, TopCenter, TopRight.
    /// </summary>
    internal static SDL.Rect GetMainPanelRect(SDL.Rect bounds, int panelW, int panelH, string position)
    {
        int panelX = position switch
        {
            string p when p.EndsWith("Left")  => Margin,
            string p when p.EndsWith("Right") => bounds.W - panelW - Margin,
            _                                  => (bounds.W - panelW) / 2,
        };

        int panelY = position switch
        {
            string p when p.StartsWith("Top")    => Margin,
            string p when p.StartsWith("Bottom") => bounds.H - panelH - bounds.H / 6,
            _                                     => (bounds.H - panelH) / 2,
        };

        return new SDL.Rect { X = Math.Max(8, panelX), Y = Math.Max(8, panelY), W = panelW, H = panelH };
    }

    // ---- Drawing ----

    public static void Draw(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        DrawBackground(ctx, bounds, menu);

        if (menu.CurrentScreen == MainMenuScreen.Screen.Main)
            DrawMainPanel(ctx, bounds, menu);
        else
            DrawSubPanel(ctx, bounds, menu);
    }

    private static void DrawBackground(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        IntPtr preScaled = menu.GetScaledBackground(bounds);
        if (preScaled != IntPtr.Zero)
        {
            ctx.BlitSurface(preScaled, null, bounds);
        }
        else
        {
            ctx.FillRect(ToFRect(bounds), BgFallback);
        }

        var dimColor = menu.CurrentScreen == MainMenuScreen.Screen.Main ? OverlayDim : SubDim;
        ctx.FillRect(ToFRect(bounds), dimColor);
    }

    private static void DrawMainPanel(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        var items  = menu.GetCurrentItems();
        int panelW = MenuRenderConstants.PanelW(MainPanelMaxW, bounds.W);
        int panelH = PanelHeaderH + items.Length * ItemH + Pad;
        var panel  = GetMainPanelRect(bounds, panelW, panelH, menu.MenuPosition);

        DrawPanel(ctx, panel, menu.GetTitle(), TitleColor, items, menu, openMenuIdx: -1,
                  showCtrl: false, listW: panelW);
    }

    private static void DrawSubPanel(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        if (menu.RebindingAction != null || menu.IsGamepadRebinding)
        {
            DrawRebindPrompt(ctx, bounds, menu);
            return;
        }

        var  items       = menu.GetCurrentItems();
        int  openMenuIdx = menu.CurrentScreen == MainMenuScreen.Screen.GamepadBindings
                           ? menu.OpenMenuBindingIndex : -1;
        bool hasSep      = openMenuIdx >= 0;
        bool showCtrl    = ShouldShowController(bounds, menu.CurrentScreen);
        int  ctrlAreaW   = MenuRenderConstants.PanelW(ControllerAreaW, bounds.W);
        int  panelW      = showCtrl ? MenuRenderConstants.PanelW(FullPanelW, bounds.W) : MenuRenderConstants.PanelW(SlimPanelW, bounds.W);
        int  listW       = showCtrl ? panelW - ctrlAreaW : panelW;
        int  panelH      = PanelHeaderH + items.Length * ItemH + Pad + (hasSep ? SeparatorH : 0);
        int  panelX      = Math.Max(8, (bounds.W - panelW) / 2);
        int  panelY      = Math.Max(8, (bounds.H - panelH) / 2);

        DrawPanel(ctx, new SDL.Rect { X = panelX, Y = panelY, W = panelW, H = panelH },
                  menu.GetTitle(), TitleColor, items, menu, openMenuIdx, showCtrl, listW);
    }

    private static void DrawRebindPrompt(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        int panelW = MenuRenderConstants.PanelW(RebindPanelMaxW, bounds.W);
        int panelH = S(RebindPanelH);
        int panelX = (bounds.W - panelW) / 2;
        int panelY = (bounds.H - panelH) / 2;
        var panelFRect = new SDL.FRect { X = panelX, Y = panelY, W = panelW, H = panelH };

        ctx.FillRect(panelFRect, PanelColor);
        ctx.DrawRect(panelFRect, BorderColor, 2f);

        string hint = menu.IsGamepadRebinding
            ? (menu.OverrideStartBindingProtection
                ? menu.Localization.MainMenuRebindPressButtonNoCancel
                : menu.Localization.MainMenuRebindPressButton)
            : menu.Localization.MainMenuRebindPressKey;

        ctx.DrawText(menu.GetTitle(),
            new SDL.FRect { X = panelX, Y = panelY + S(10), W = panelW, H = S(44) },
            RebindColor, menu.Localization.FontFamily, 13f * MenuScale.Scale, bold: true);
        ctx.DrawText(hint,
            new SDL.FRect { X = panelX, Y = panelY + S(60), W = panelW, H = S(44) },
            new SDL.Color { R = 220, G = 220, B = 180, A = 200 },
            menu.Localization.FontFamily, 12f * MenuScale.Scale, bold: false, italic: true);
    }

    private static void DrawPanel(SDL3PaintContext ctx, SDL.Rect panel, string title, SDL.Color titleColor,
                                  string[] items, MainMenuScreen menu, int openMenuIdx,
                                  bool showCtrl, int listW)
    {
        bool hasSep = openMenuIdx >= 0;
        var panelFRect = ToFRect(panel);

        ctx.FillRect(panelFRect, PanelColor);
        ctx.DrawRect(panelFRect, BorderColor, 2f);

        var titleRect = new SDL.FRect { X = panel.X + Pad, Y = panel.Y + TitleYOffset, W = panel.W - Pad * 2, H = TitleRectH };
        ctx.DrawText(title, titleRect, titleColor, menu.Localization.FontFamily, 14f * MenuScale.Scale, bold: true);

        ctx.DrawLine(panel.X + Pad, panel.Y + DividerY, panel.X + panel.W - Pad, panel.Y + DividerY,
            new SDL.Color { R = 255, G = 255, B = 255, A = 60 });

        if (showCtrl)
        {
            ctx.DrawLine(panel.X + listW, panel.Y + 8, panel.X + listW, panel.Y + panel.H - 8,
                new SDL.Color { R = 255, G = 255, B = 255, A = 50 });

            var ctrlArea = new SDL.FRect { X = panel.X + listW + 6, Y = panel.Y + 14, W = panel.W - listW - 10, H = panel.H - 28 };
            DrawControllerSprite(ctx, ctrlArea, menu.ActiveNesButton, menu.Localization.NesControllerLabel, menu.Localization.FontFamily);
        }

        float sliderLabelColumnW = ComputeSliderLabelColumnW(ctx, menu, items.Length, MenuScale.Scale);
        for (int i = 0; i < items.Length; i++)
        {
            if (hasSep && i == openMenuIdx)
            {
                int sepLineY = panel.Y + ItemListStartY + i * ItemH + 2;
                ctx.DrawLine(panel.X + Pad, sepLineY, panel.X + listW - Pad, sepLineY,
                    new SDL.Color { R = 255, G = 255, B = 255, A = 60 });
                var sepRect = new SDL.FRect { X = panel.X + Pad, Y = sepLineY + 3, W = listW - Pad * 2, H = SeparatorLabelH };
                ctx.DrawText(menu.Localization.SystemSectionLabel, sepRect,
                    new SDL.Color { R = 160, G = 160, B = 160, A = 130 },
                    menu.Localization.FontFamily, 8f * MenuScale.Scale, bold: false,
                    TextHAlign.Near, TextVAlign.Top);
            }

            int extraY      = hasSep && i >= openMenuIdx ? SeparatorH : 0;
            bool enabled    = menu.IsItemEnabled(i);
            bool selected   = i == menu.SelectedIndex;
            bool isOpenMenu = openMenuIdx >= 0 && i == openMenuIdx;

            var itemRect = new SDL.Rect
            {
                X = panel.X + 6,
                Y = panel.Y + ItemListStartY + i * ItemH + extraY,
                W = listW - 12,
                H = ItemH - 2,
            };

            var             textColor  = isOpenMenu ? AmberColor : (enabled ? ItemOn : ItemDim);
            SliderItemData? sliderData = menu.GetCurrentSliderData(i);
            IntPtr          icon       = menu.GetCurrentItemIcon(i);

            if (sliderData.HasValue)
            {
                if (selected && enabled) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                DrawSliderItem(ctx, sliderData.Value, itemRect, textColor, menu.Localization.FontFamily, selected && enabled, sliderLabelColumnW);
            }
            else if (icon != IntPtr.Zero)
            {
                if (selected && enabled) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                DrawItemWithIcon(ctx, icon, items[i], selected && enabled, enabled,
                                 itemRect, textColor, menu.Localization.FontFamily);
            }
            else if (selected && enabled)
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
                DrawItemRow(ctx, items[i], itemRect, ItemDim, menu.Localization.FontFamily, bold: false, selected: false);
            }
        }
    }

    private const float ControllerAspect = 2.43f;

    private static void DrawControllerSprite(SDL3PaintContext ctx, SDL.FRect area, string? activeButton, string label, string fontFamily)
    {
        IntPtr sprite = ControllerSprites.Get(activeButton);
        float ctrlW = area.W;
        float ctrlH = ctrlW / ControllerAspect;
        if (ctrlH > area.H) { ctrlH = area.H; ctrlW = ctrlH * ControllerAspect; }
        float ox = area.X + (area.W - ctrlW) * 0.5f;
        float oy = area.Y + (area.H - ctrlH) * 0.5f;
        var ctrlRect = new SDL.Rect { X = (int)ox, Y = (int)oy, W = (int)ctrlW, H = (int)ctrlH };
        ctx.BlitSurface(sprite, null, ctrlRect);

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
        ctx.DrawText(text, textRect, enabled ? textColor : ItemDim,
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

    private static float ComputeSliderLabelColumnW(SDL3PaintContext ctx, MainMenuScreen menu, int itemCount, float scale)
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

    // ---- Helpers ----

    private static bool ShouldShowController(SDL.Rect bounds, MainMenuScreen.Screen screen) =>
        bounds.W >= MinWidthForCtrl
        && (screen == MainMenuScreen.Screen.KeyboardBindings
            || screen == MainMenuScreen.Screen.GamepadBindings);

    private static SDL.FRect ToFRect(SDL.Rect r) => new() { X = r.X, Y = r.Y, W = r.W, H = r.H };

    private static int S(int value) => (int)Math.Round(value * MenuScale.Scale);
}

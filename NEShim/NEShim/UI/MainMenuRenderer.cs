using NEShim.Platform;
using NEShim.Rendering;
using NEShim.UI.Controls;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the pre-game main menu and all its sub-screens.
/// Main screen: background image + panel anchored per <c>MainMenuScreen.MenuPosition</c>.
/// Sub-screens (Settings, Video, Sound, etc.): centred panel over dimmed background.
/// Per-row/per-element drawing (item rows, sliders, the controller diagram, panel chrome) is
/// delegated to the shared, independently-testable components in NEShim.UI.Controls — this
/// class owns only the main-menu-specific panel layout/orchestration around them.
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
    private static readonly SDL.Color AmberColor  = new() { R = 255, G = 220, B = 140, A = 255 };

    // Main-menu items are 4px taller than in-game menu items (42 vs 38) — the main menu
    // is a full-screen overlay with more visual breathing room.
    // On Steam Deck all layout constants scale up so the menus remain readable at handheld distance.
    // Properties, not static readonly fields: they depend on MenuScale.Scale, which changes
    // live on window resize/fullscreen toggle (see MenuScale's doc comment). A static readonly
    // field would bake in whatever scale was active the first time this class was touched and
    // never update again, desyncing spacing from the font sizes computed inline in Draw() below
    // (which do re-read MenuScale.Scale every frame) — reproduced on both Windows and Linux as
    // items overflowing off-screen in windowed mode and overlapping after returning to fullscreen.
    private static int ItemH           => S(42);
    private static int Pad             => S(14);
    private static int SeparatorH      => S(18);
    private static int PanelHeaderH    => S(52);
    private static int ItemListStartY  => S(50);
    private static int TitleRectH      => S(36);
    private static int DividerY        => S(46);
    private static int SeparatorLabelH => S(12);

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

        if (menu.CurrentScreen == Screen.Main)
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

        var dimColor = menu.CurrentScreen == Screen.Main ? OverlayDim : SubDim;
        ctx.FillRect(ToFRect(bounds), dimColor);
    }

    private static void DrawMainPanel(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        var items  = menu.GetCurrentItems();
        int panelW = MenuRenderConstants.ScaledPanelW(MainPanelMaxW, bounds.W);
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
        int  openMenuIdx = menu.CurrentScreen == Screen.GamepadBindings
                           ? menu.OpenMenuBindingIndex : -1;
        // Generic handler-driven divider (e.g. PlayerSelectHandler's gamepad/keyboard grouping),
        // mutually exclusive with the OpenMenu row's own divider — see DrawPanel below, which
        // re-derives the same fallback for the actual line/label draw.
        int  sepIdx      = openMenuIdx >= 0 ? openMenuIdx : menu.GetCurrentSeparatorIndex();
        bool hasSep      = sepIdx >= 0;
        bool showCtrl    = ShouldShowController(bounds, menu);
        int  ctrlAreaW   = MenuRenderConstants.ScaledPanelW(ControllerAreaW, bounds.W);
        int  panelW      = showCtrl ? MenuRenderConstants.ScaledPanelW(FullPanelW, bounds.W) : MenuRenderConstants.ScaledPanelW(SlimPanelW, bounds.W);
        int  listW       = showCtrl ? panelW - ctrlAreaW : panelW;
        int  panelH      = PanelHeaderH + items.Length * ItemH + Pad + (hasSep ? SeparatorH : 0);
        int  panelX      = Math.Max(8, (bounds.W - panelW) / 2);
        int  panelY      = Math.Max(8, (bounds.H - panelH) / 2);

        DrawPanel(ctx, new SDL.Rect { X = panelX, Y = panelY, W = panelW, H = panelH },
                  menu.GetTitle(), TitleColor, items, menu, openMenuIdx, showCtrl, listW);
    }

    private static void DrawRebindPrompt(SDL3PaintContext ctx, SDL.Rect bounds, MainMenuScreen menu)
    {
        int panelW = MenuRenderConstants.ScaledPanelW(RebindPanelMaxW, bounds.W);
        int panelH = S(RebindPanelH);
        int panelX = (bounds.W - panelW) / 2;
        int panelY = (bounds.H - panelH) / 2;
        var panelFRect = new SDL.FRect { X = panelX, Y = panelY, W = panelW, H = panelH };

        PanelFrameControl.Draw(ctx, panelFRect, PanelColor, BorderColor);

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
        int  sepIdx   = openMenuIdx >= 0 ? openMenuIdx : menu.GetCurrentSeparatorIndex();
        string? sepLabel = openMenuIdx >= 0 ? menu.Localization.SystemSectionLabel : menu.GetCurrentSeparatorLabel();
        bool hasSep = sepIdx >= 0;
        var panelFRect = ToFRect(panel);

        PanelFrameControl.Draw(ctx, panelFRect, PanelColor, BorderColor);

        var titleRect = new SDL.FRect { X = panel.X + Pad, Y = panel.Y + TitleYOffset, W = panel.W - Pad * 2, H = TitleRectH };
        ctx.DrawText(title, titleRect, titleColor, menu.Localization.FontFamily, 14f * MenuScale.Scale, bold: true);

        ctx.DrawLine(panel.X + Pad, panel.Y + DividerY, panel.X + panel.W - Pad, panel.Y + DividerY,
            new SDL.Color { R = 255, G = 255, B = 255, A = 60 });

        if (showCtrl)
        {
            // Bounded to the content region (same as where the item list itself starts/ends,
            // ItemListStartY/Pad) rather than the raw panel edges — using unscaled literal
            // offsets here previously let this line start above the title's own scaled Y
            // (S(10)), so it ran through the header band and visibly cut through the title text.
            int contentTop    = panel.Y + ItemListStartY;
            int contentBottom = panel.Y + panel.H - Pad;
            ctx.DrawLine(panel.X + listW, contentTop, panel.X + listW, contentBottom,
                new SDL.Color { R = 255, G = 255, B = 255, A = 50 });

            var ctrlArea = new SDL.FRect { X = panel.X + listW + 6, Y = contentTop, W = panel.W - listW - 10, H = contentBottom - contentTop };
            string ctrlLabel = MenuBindingHelpers.ControllerDiagramLabel(
                menu.Localization, MenuBindingHelpers.PlayerForBindingScreen(menu.CurrentScreen));
            ControllerDiagramControl.Draw(ctx, ctrlArea, menu.ActiveNesButton, ctrlLabel, menu.Localization.FontFamily, MenuScale.Scale);
        }

        var sliderItems = new SliderItemData?[items.Length];
        for (int i = 0; i < items.Length; i++)
            sliderItems[i] = menu.GetCurrentSliderData(i);
        float sliderLabelColumnW = SliderControl.ComputeLabelColumnW(ctx, sliderItems, menu.Localization.FontFamily, MenuScale.Scale);
        for (int i = 0; i < items.Length; i++)
        {
            if (hasSep && i == sepIdx)
            {
                int sepLineY = panel.Y + ItemListStartY + i * ItemH + 2;
                ctx.DrawLine(panel.X + Pad, sepLineY, panel.X + listW - Pad, sepLineY,
                    new SDL.Color { R = 255, G = 255, B = 255, A = 60 });
                if (sepLabel != null)
                {
                    var sepRect = new SDL.FRect { X = panel.X + Pad, Y = sepLineY + 3, W = listW - Pad * 2, H = SeparatorLabelH };
                    ctx.DrawText(sepLabel, sepRect,
                        new SDL.Color { R = 160, G = 160, B = 160, A = 130 },
                        menu.Localization.FontFamily, 8f * MenuScale.Scale, bold: false,
                        TextHAlign.Near, TextVAlign.Top);
                }
            }

            int extraY      = hasSep && i >= sepIdx ? SeparatorH : 0;
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
            SliderItemData? sliderData = sliderItems[i];
            IntPtr          icon       = menu.GetCurrentItemIcon(i);
            IntPtr          valueIcon  = menu.GetCurrentItemValueIcon(i);

            if (sliderData.HasValue)
            {
                if (selected && enabled) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                SliderControl.Draw(ctx, sliderData.Value, itemRect, textColor, menu.Localization.FontFamily, selected && enabled, sliderLabelColumnW, MenuScale.Scale);
            }
            else if (icon != IntPtr.Zero)
            {
                if (selected && enabled) ctx.FillRect(ToFRect(itemRect), SelectedBg);
                IconRowControl.Draw(ctx, icon, items[i], selected && enabled, itemRect, textColor, menu.Localization.FontFamily, MenuScale.Scale);
            }
            else if (selected && enabled)
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
                ItemRowControl.Draw(ctx, items[i], itemRect, ItemDim, menu.Localization.FontFamily, bold: false, selected: false, valueIcon, MenuScale.Scale);
            }
        }
    }

    // ---- Helpers ----

    private static bool ShouldShowController(SDL.Rect bounds, MainMenuScreen menu) =>
        bounds.W >= MinWidthForCtrl && menu.ShowsControllerDiagram;

    private static SDL.FRect ToFRect(SDL.Rect r) => new() { X = r.X, Y = r.Y, W = r.W, H = r.H };

    private static int S(int value) => (int)Math.Round(value * MenuScale.Scale);
}

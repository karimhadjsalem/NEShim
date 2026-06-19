using System.Drawing;
using System.Drawing.Drawing2D;
using NEShim.Platform;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the pre-game main menu and all its sub-screens.
/// Main screen: background image + panel anchored per <c>MainMenuScreen.MenuPosition</c>.
/// Sub-screens (Settings, Video, Sound, etc.): centred panel over dimmed background.
/// </summary>
internal static class MainMenuRenderer
{
    private static readonly Color BgFallback  = Color.FromArgb(255, 12, 12, 24);
    private static readonly Color OverlayDim  = Color.FromArgb(130, 0, 0, 0);
    private static readonly Color SubDim      = Color.FromArgb(175, 0, 0, 0);
    private static readonly Color PanelColor  = Color.FromArgb(225, 16, 16, 30);
    private static readonly Color BorderColor = Color.FromArgb(200, 70, 130, 210);
    private static readonly Color TitleColor  = Color.FromArgb(255, 195, 225, 255);
    private static readonly Color RebindColor = Color.FromArgb(255, 255, 200, 100);
    private static readonly Color ItemOn      = Color.White;
    private static readonly Color ItemDim     = Color.FromArgb(110, 160, 160, 160);
    private static readonly Color SelectedBg  = Color.FromArgb(210, 50, 105, 190);
    private static readonly Color AmberColor  = Color.FromArgb(255, 220, 140);

    // Main-menu items are 4px taller than in-game menu items (42 vs 38) — the main menu
    // is a full-screen overlay with more visual breathing room.
    // On Steam Deck all layout constants scale up so the menus remain readable at handheld distance.
    private static readonly int ItemH           = S(42);
    private static readonly int Pad             = S(14);
    private static readonly int SeparatorH      = S(18);
    private static readonly int PanelHeaderH    = S(52); // height reserved for title + divider above item list
    private static readonly int ItemListStartY  = S(50); // Y offset from panel top to first item row
    private static readonly int TitleRectH      = S(36); // height of the title text rect
    private static readonly int DividerY        = S(46); // Y offset from panel top to the horizontal divider
    private static readonly int SeparatorLabelH = S(12); // height of the "System" section label above separator

    private const int Margin         = 40;  // distance from screen edge for non-centred positions (not scaled)
    private const int TitleYOffset   = 8;   // Y offset from panel top to title rect (not scaled)
    private const int MainPanelMaxW  = 360; // max width for the main-screen panel
    private const int RebindPanelMaxW = 400; // max width for the rebind prompt panel
    private const int RebindPanelH   = 120; // fixed height of the rebind prompt panel

    // Controller-column constants shared with MenuRenderer via MenuRenderConstants.
    private const int ControllerAreaW = MenuRenderConstants.ControllerAreaW;
    private const int FullPanelW      = MenuRenderConstants.FullPanelW;
    private const int SlimPanelW      = MenuRenderConstants.SlimPanelW;
    private const int MinWidthForCtrl = MenuRenderConstants.MinWidthForCtrl;

    // ---- Hit testing ----

    /// <summary>
    /// Returns the index of the item at <paramref name="p"/>, or -1 if none.
    /// Mirrors the item-rect calculation used in <see cref="Draw"/>.
    /// </summary>
    public static int HitTestItem(Point p, Rectangle bounds, MainMenuScreen menu)
    {
        if (menu.RebindingAction != null || menu.IsGamepadRebinding) return -1;

        var items = menu.GetCurrentItems();

        if (menu.CurrentScreen == MainMenuScreen.Screen.Main)
        {
            int panelW = MenuRenderConstants.PanelW(MainPanelMaxW, bounds.Width);
            int panelH = PanelHeaderH + items.Length * ItemH + Pad;
            var panel  = GetMainPanelRect(bounds, panelW, panelH, menu.MenuPosition);
            for (int i = 0; i < items.Length; i++)
            {
                var itemRect = new Rectangle(panel.X + 6, panel.Y + ItemListStartY + i * ItemH, panel.Width - 12, ItemH - 2);
                if (itemRect.Contains(p) && menu.IsItemEnabled(i)) return i;
            }
            return -1;
        }

        bool showCtrl    = ShouldShowController(bounds, menu.CurrentScreen);
        int  openMenuIdx = menu.CurrentScreen == MainMenuScreen.Screen.GamepadBindings
                           ? menu.OpenMenuBindingIndex : -1;
        bool hasSep      = openMenuIdx >= 0;
        int  ctrlAreaW   = MenuRenderConstants.PanelW(ControllerAreaW, bounds.Width);
        int  pw          = showCtrl ? MenuRenderConstants.PanelW(FullPanelW, bounds.Width) : MenuRenderConstants.PanelW(SlimPanelW, bounds.Width);
        int  listW       = showCtrl ? pw - ctrlAreaW : pw;
        int  ph          = PanelHeaderH + items.Length * ItemH + Pad + (hasSep ? SeparatorH : 0);
        int  px          = Math.Max(8, (bounds.Width  - pw) / 2);
        int  py          = Math.Max(8, (bounds.Height - ph) / 2);

        for (int i = 0; i < items.Length; i++)
        {
            int extraY = hasSep && i >= openMenuIdx ? SeparatorH : 0;
            var itemRect = new Rectangle(px + 6, py + ItemListStartY + i * ItemH + extraY, listW - 12, ItemH - 2);
            if (itemRect.Contains(p) && menu.IsItemEnabled(i)) return i;
        }
        return -1;
    }

    // ---- Panel positioning ----

    /// <summary>
    /// Computes the main-screen panel <see cref="Rectangle"/> from <paramref name="position"/>.
    /// Supported values: BottomCenter, Center, BottomLeft, BottomRight, TopLeft, TopCenter, TopRight.
    /// </summary>
    internal static Rectangle GetMainPanelRect(Rectangle bounds, int panelW, int panelH, string position)
    {
        int panelX = position switch
        {
            string p when p.EndsWith("Left")  => Margin,
            string p when p.EndsWith("Right") => bounds.Width - panelW - Margin,
            _                                  => (bounds.Width - panelW) / 2,
        };

        int panelY = position switch
        {
            string p when p.StartsWith("Top")    => Margin,
            string p when p.StartsWith("Bottom") => bounds.Height - panelH - bounds.Height / 6,
            _                                     => (bounds.Height - panelH) / 2, // "Center"
        };

        return new Rectangle(Math.Max(8, panelX), Math.Max(8, panelY), panelW, panelH);
    }

    // ---- Drawing ----

    public static void Draw(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        g.CompositingMode   = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        DrawBackground(g, bounds, menu);

        if (menu.CurrentScreen == MainMenuScreen.Screen.Main)
            DrawMainPanel(g, bounds, menu);
        else
            DrawSubPanel(g, bounds, menu);
    }

    private static void DrawBackground(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        var preScaled = menu.GetScaledBackground(bounds);
        if (preScaled != null)
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(preScaled, bounds.X, bounds.Y);
            g.CompositingMode = CompositingMode.SourceOver;
        }
        else
        {
            using var bg = new SolidBrush(BgFallback);
            g.FillRectangle(bg, bounds);
        }

        var dimColor = menu.CurrentScreen == MainMenuScreen.Screen.Main ? OverlayDim : SubDim;
        using var dim = new SolidBrush(dimColor);
        g.FillRectangle(dim, bounds);
    }

    private static void DrawMainPanel(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        var items  = menu.GetCurrentItems();
        int panelW = MenuRenderConstants.PanelW(MainPanelMaxW, bounds.Width);
        int panelH = PanelHeaderH + items.Length * ItemH + Pad;
        var panel  = GetMainPanelRect(bounds, panelW, panelH, menu.MenuPosition);

        DrawPanel(g, panel, menu.GetTitle(), TitleColor, items, menu, openMenuIdx: -1,
                  showCtrl: false, listW: panelW);
    }

    private static void DrawSubPanel(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        if (menu.RebindingAction != null || menu.IsGamepadRebinding)
        {
            DrawRebindPrompt(g, bounds, menu);
            return;
        }

        var  items       = menu.GetCurrentItems();
        int  openMenuIdx = menu.CurrentScreen == MainMenuScreen.Screen.GamepadBindings
                           ? menu.OpenMenuBindingIndex : -1;
        bool hasSep      = openMenuIdx >= 0;
        bool showCtrl    = ShouldShowController(bounds, menu.CurrentScreen);
        int  ctrlAreaW   = MenuRenderConstants.PanelW(ControllerAreaW, bounds.Width);
        int  panelW      = showCtrl ? MenuRenderConstants.PanelW(FullPanelW, bounds.Width) : MenuRenderConstants.PanelW(SlimPanelW, bounds.Width);
        int  listW       = showCtrl ? panelW - ctrlAreaW : panelW;
        int  panelH      = PanelHeaderH + items.Length * ItemH + Pad + (hasSep ? SeparatorH : 0);
        int  panelX      = Math.Max(8, (bounds.Width  - panelW) / 2);
        int  panelY      = Math.Max(8, (bounds.Height - panelH) / 2);

        DrawPanel(g, new Rectangle(panelX, panelY, panelW, panelH),
                  menu.GetTitle(), TitleColor, items, menu, openMenuIdx, showCtrl, listW);
    }

    private static void DrawRebindPrompt(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        int panelW = MenuRenderConstants.PanelW(RebindPanelMaxW, bounds.Width);
        int panelH = S(RebindPanelH);
        int panelX = (bounds.Width  - panelW) / 2;
        int panelY = (bounds.Height - panelH) / 2;

        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
        using var pb = new SolidBrush(PanelColor);
        g.FillRectangle(pb, panelRect);
        using var bp = new Pen(BorderColor, 2f);
        g.DrawRectangle(bp, panelRect);

        using var tf = new Font(menu.Localization.FontFamily, 13f * MenuScale.Scale, FontStyle.Bold,   GraphicsUnit.Point);
        using var hf = new Font(menu.Localization.FontFamily, 12f * MenuScale.Scale, FontStyle.Italic, GraphicsUnit.Point);
        using var tb = new SolidBrush(RebindColor);
        using var hb = new SolidBrush(Color.FromArgb(200, 220, 220, 180));
        var centred = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        string hint = menu.IsGamepadRebinding
            ? (menu.OverrideStartBindingProtection
                ? menu.Localization.MainMenuRebindPressButtonNoCancel
                : menu.Localization.MainMenuRebindPressButton)
            : menu.Localization.MainMenuRebindPressKey;

        g.DrawString(menu.GetTitle(), tf, tb,
            new RectangleF(panelX, panelY + S(10), panelW, S(44)), centred);
        g.DrawString(hint, hf, hb,
            new RectangleF(panelX, panelY + S(60), panelW, S(44)), centred);
    }

    private static void DrawPanel(Graphics g, Rectangle panel, string title, Color titleColor,
                                  string[] items, MainMenuScreen menu, int openMenuIdx,
                                  bool showCtrl, int listW)
    {
        bool hasSep = openMenuIdx >= 0;

        using var pb = new SolidBrush(PanelColor);
        g.FillRectangle(pb, panel);
        using var bp = new Pen(BorderColor, 2f);
        g.DrawRectangle(bp, panel);

        using var tf  = new Font(menu.Localization.FontFamily, 14f * MenuScale.Scale, FontStyle.Bold, GraphicsUnit.Point);
        using var tb  = new SolidBrush(titleColor);
        var titleRect = new RectangleF(panel.X + Pad, panel.Y + TitleYOffset, panel.Width - Pad * 2, TitleRectH);
        var centred   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(title, tf, tb, titleRect, centred);

        using var div = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
        g.DrawLine(div, panel.X + Pad, panel.Y + DividerY, panel.X + panel.Width - Pad, panel.Y + DividerY);

        // Controller diagram on the right side of binding screens
        if (showCtrl)
        {
            using var vDivPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1);
            g.DrawLine(vDivPen, panel.X + listW, panel.Y + 8, panel.X + listW, panel.Y + panel.Height - 8);

            var ctrlArea = new RectangleF(panel.X + listW + 6, panel.Y + 14, panel.Width - listW - 10, panel.Height - 28);
            NesControllerDiagram.Draw(g, ctrlArea, menu.ActiveNesButton, menu.Localization.NesControllerLabel);
        }

        using var selBrush   = new SolidBrush(SelectedBg);
        using var selFont    = new Font(menu.Localization.FontFamily, 12f * MenuScale.Scale, FontStyle.Bold,    GraphicsUnit.Point);
        using var itemFont   = new Font(menu.Localization.FontFamily, 12f * MenuScale.Scale, FontStyle.Regular, GraphicsUnit.Point);
        using var onBrush    = new SolidBrush(ItemOn);
        using var amberBrush = new SolidBrush(AmberColor);
        using var dimBrush   = new SolidBrush(ItemDim);
        var leftFmt = new StringFormat
        {
            Alignment     = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.EllipsisCharacter,
        };

        for (int i = 0; i < items.Length; i++)
        {
            // Draw separator before the OpenMenu system entry
            if (hasSep && i == openMenuIdx)
            {
                int sepLineY = panel.Y + ItemListStartY + i * ItemH + 2;
                using var sepPen   = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
                using var sepFont  = new Font(menu.Localization.FontFamily, 8f * MenuScale.Scale, FontStyle.Regular, GraphicsUnit.Point);
                using var sepBrush = new SolidBrush(Color.FromArgb(130, 160, 160, 160));
                g.DrawLine(sepPen, panel.X + Pad, sepLineY, panel.X + listW - Pad, sepLineY);
                var nearFmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
                var sepRect = new RectangleF(panel.X + Pad, sepLineY + 3, listW - Pad * 2, SeparatorLabelH);
                g.DrawString(menu.Localization.SystemSectionLabel, sepFont, sepBrush, sepRect, nearFmt);
            }

            int extraY    = hasSep && i >= openMenuIdx ? SeparatorH : 0;
            bool enabled  = menu.IsItemEnabled(i);
            bool selected = i == menu.SelectedIndex;
            bool isOpenMenu = openMenuIdx >= 0 && i == openMenuIdx;

            var itemRect = new Rectangle(
                panel.X + 6,
                panel.Y + ItemListStartY + i * ItemH + extraY,
                listW - 12,
                ItemH - 2);

            Brush activeBrush = isOpenMenu ? amberBrush : onBrush;

            if (selected && enabled)
            {
                g.FillRectangle(selBrush, itemRect);
                g.DrawString("▶  " + items[i], selFont, activeBrush, (RectangleF)itemRect, leftFmt);
            }
            else if (enabled)
            {
                g.DrawString("    " + items[i], itemFont, activeBrush, (RectangleF)itemRect, leftFmt);
            }
            else
            {
                g.DrawString("    " + items[i] + menu.Localization.SlotNoSave, itemFont, dimBrush, (RectangleF)itemRect, leftFmt);
            }
        }
    }

    // ---- Helpers ----

    private static bool ShouldShowController(Rectangle bounds, MainMenuScreen.Screen screen) =>
        bounds.Width >= MinWidthForCtrl
        && (screen == MainMenuScreen.Screen.KeyboardBindings
            || screen == MainMenuScreen.Screen.GamepadBindings);

    private static int S(int value) => (int)Math.Round(value * MenuScale.Scale);
}

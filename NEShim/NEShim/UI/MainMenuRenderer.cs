using System.Drawing;
using System.Drawing.Drawing2D;

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

    private const int ItemH  = 42;
    private const int Pad    = 14;
    private const int Margin = 40; // distance from screen edge for non-centred positions

    // ---- Hit testing ----

    /// <summary>
    /// Returns the index of the item at <paramref name="p"/>, or -1 if none.
    /// Mirrors the item-rect calculation used in <see cref="Draw"/>.
    /// </summary>
    public static int HitTestItem(Point p, Rectangle bounds, MainMenuScreen menu)
    {
        if (menu.RebindingAction != null || menu.IsGamepadRebinding) return -1;

        var       items  = menu.GetCurrentItems();
        Rectangle panel;

        if (menu.CurrentScreen == MainMenuScreen.Screen.Main)
        {
            int panelW = Math.Min(360, bounds.Width - 60);
            int panelH = 52 + items.Length * ItemH + Pad;
            panel = GetMainPanelRect(bounds, panelW, panelH, menu.MenuPosition);
        }
        else
        {
            bool steamOverride = menu.CurrentScreen == MainMenuScreen.Screen.GamepadBindings
                              && menu.IsGamepadOverriddenBySteam;
            int steamBannerH = steamOverride ? ItemH : 0;
            int panelW = Math.Min(440, bounds.Width - 60);
            int panelH = 52 + steamBannerH + items.Length * ItemH + Pad;
            int panelX = Math.Max(8, (bounds.Width  - panelW) / 2);
            int panelY = Math.Max(8, (bounds.Height - panelH) / 2);
            panel = new Rectangle(panelX, panelY, panelW, panelH);

            for (int i = 0; i < items.Length; i++)
            {
                var itemRect = new Rectangle(panel.X + 6, panel.Y + 50 + steamBannerH + i * ItemH, panel.Width - 12, ItemH - 2);
                if (itemRect.Contains(p) && menu.IsItemEnabled(i)) return i;
            }
            return -1;
        }

        for (int i = 0; i < items.Length; i++)
        {
            var itemRect = new Rectangle(panel.X + 6, panel.Y + 50 + i * ItemH, panel.Width - 12, ItemH - 2);
            if (itemRect.Contains(p) && menu.IsItemEnabled(i)) return i;
        }
        return -1;
    }

    // ---- Panel positioning ----

    /// <summary>
    /// Computes the main-screen panel <see cref="Rectangle"/> from <paramref name="position"/>.
    /// Supported values: BottomCenter, Center, BottomLeft, BottomRight, TopLeft, TopCenter, TopRight.
    /// </summary>
    private static Rectangle GetMainPanelRect(Rectangle bounds, int panelW, int panelH, string position)
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
        if (menu.Background != null)
        {
            float imgAspect   = (float)menu.Background.Width / menu.Background.Height;
            float panelAspect = (float)bounds.Width / bounds.Height;
            Rectangle dest;
            if (panelAspect > imgAspect)
            {
                int h = (int)(bounds.Width / imgAspect);
                dest = new Rectangle(bounds.X, bounds.Y + (bounds.Height - h) / 2, bounds.Width, h);
            }
            else
            {
                int w = (int)(bounds.Height * imgAspect);
                dest = new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y, w, bounds.Height);
            }

            g.CompositingMode = CompositingMode.SourceCopy;
            using var black = new SolidBrush(Color.Black);
            g.FillRectangle(black, bounds);
            g.CompositingMode = CompositingMode.SourceOver;
            var src = new Rectangle(0, 0, menu.Background.Width, menu.Background.Height);
            g.DrawImage(menu.Background, dest, src, GraphicsUnit.Pixel);
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
        int panelW = Math.Min(360, bounds.Width - 60);
        int panelH = 52 + items.Length * ItemH + Pad;
        var panel  = GetMainPanelRect(bounds, panelW, panelH, menu.MenuPosition);

        DrawPanel(g, panel, menu.GetTitle(), TitleColor, items, menu);
    }

    private static void DrawSubPanel(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        if (menu.RebindingAction != null || menu.IsGamepadRebinding)
        {
            DrawRebindPrompt(g, bounds, menu);
            return;
        }

        bool steamOverride = menu.CurrentScreen == MainMenuScreen.Screen.GamepadBindings
                          && menu.IsGamepadOverriddenBySteam;
        int steamBannerH = steamOverride ? ItemH : 0;

        var items  = menu.GetCurrentItems();
        int panelW = Math.Min(440, bounds.Width - 60);
        int panelH = 52 + steamBannerH + items.Length * ItemH + Pad;
        int panelX = Math.Max(8, (bounds.Width  - panelW) / 2);
        int panelY = Math.Max(8, (bounds.Height - panelH) / 2);

        DrawPanel(g, new Rectangle(panelX, panelY, panelW, panelH),
                  menu.GetTitle(), TitleColor, items, menu, steamBannerH);
    }

    private static void DrawRebindPrompt(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        int panelW = Math.Min(400, bounds.Width - 60);
        int panelH = 120;
        int panelX = (bounds.Width  - panelW) / 2;
        int panelY = (bounds.Height - panelH) / 2;

        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
        using var pb = new SolidBrush(PanelColor);
        g.FillRectangle(pb, panelRect);
        using var bp = new Pen(BorderColor, 2f);
        g.DrawRectangle(bp, panelRect);

        using var tf = new Font("Segoe UI", 13f, FontStyle.Bold,   GraphicsUnit.Point);
        using var hf = new Font("Segoe UI", 12f, FontStyle.Italic, GraphicsUnit.Point);
        using var tb = new SolidBrush(RebindColor);
        using var hb = new SolidBrush(Color.FromArgb(200, 220, 220, 180));
        var centred = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        string hint = menu.IsGamepadRebinding
            ? "Press any controller button  •  Start to cancel"
            : "Press any key  •  Esc to cancel";

        g.DrawString(menu.GetTitle(), tf, tb,
            new RectangleF(panelX, panelY + 10, panelW, 44), centred);
        g.DrawString(hint, hf, hb,
            new RectangleF(panelX, panelY + 60, panelW, 44), centred);
    }

    private static void DrawPanel(Graphics g, Rectangle panel, string title, Color titleColor,
                                  string[] items, MainMenuScreen menu, int steamBannerH = 0)
    {
        using var pb = new SolidBrush(PanelColor);
        g.FillRectangle(pb, panel);
        using var bp = new Pen(BorderColor, 2f);
        g.DrawRectangle(bp, panel);

        using var tf  = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
        using var tb  = new SolidBrush(titleColor);
        var titleRect = new RectangleF(panel.X + Pad, panel.Y + 8, panel.Width - Pad * 2, 36);
        var centred   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(title, tf, tb, titleRect, centred);

        using var div = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
        g.DrawLine(div, panel.X + Pad, panel.Y + 46, panel.X + panel.Width - Pad, panel.Y + 46);

        // Steam Input override banner
        if (steamBannerH > 0)
        {
            using var steamFont  = new Font("Segoe UI", 10f, FontStyle.Italic, GraphicsUnit.Point);
            using var steamBrush = new SolidBrush(Color.FromArgb(230, 255, 190, 50));
            var steamRect = new RectangleF(panel.X + Pad, panel.Y + 48, panel.Width - Pad * 2, steamBannerH - 4);
            g.DrawString("⚠  Steam Input active — these settings are overridden", steamFont, steamBrush, steamRect, centred);
        }

        using var selBrush = new SolidBrush(SelectedBg);
        using var selFont  = new Font("Segoe UI", 12f, FontStyle.Bold,    GraphicsUnit.Point);
        using var itemFont = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
        using var onBrush  = new SolidBrush(ItemOn);
        using var dimBrush = new SolidBrush(ItemDim);
        var leftFmt = new StringFormat
        {
            Alignment     = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.EllipsisCharacter,
        };

        for (int i = 0; i < items.Length; i++)
        {
            bool enabled  = menu.IsItemEnabled(i);
            bool selected = i == menu.SelectedIndex;

            var itemRect = new Rectangle(
                panel.X + 6,
                panel.Y + 50 + steamBannerH + i * ItemH,
                panel.Width - 12,
                ItemH - 2);

            if (selected && enabled)
            {
                g.FillRectangle(selBrush, itemRect);
                g.DrawString("▶  " + items[i], selFont, onBrush, (RectangleF)itemRect, leftFmt);
            }
            else if (enabled)
            {
                g.DrawString("    " + items[i], itemFont, onBrush, (RectangleF)itemRect, leftFmt);
            }
            else
            {
                g.DrawString("    " + items[i] + "  (no save)", itemFont, dimBrush, (RectangleF)itemRect, leftFmt);
            }
        }
    }
}

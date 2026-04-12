using System.Drawing;
using System.Drawing.Drawing2D;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the pre-game main menu.
/// Draws background image (or solid colour) then overlays the menu panel.
/// </summary>
internal static class MainMenuRenderer
{
    private static readonly Color BgFallback  = Color.FromArgb(255, 12, 12, 24);
    private static readonly Color OverlayDim  = Color.FromArgb(120, 0, 0, 0);
    private static readonly Color PanelColor  = Color.FromArgb(220, 16, 16, 30);
    private static readonly Color BorderColor = Color.FromArgb(200, 70, 130, 210);
    private static readonly Color TitleColor  = Color.FromArgb(255, 200, 230, 255);
    private static readonly Color ItemEnabled = Color.White;
    private static readonly Color ItemDisabled= Color.FromArgb(100, 140, 140, 140);
    private static readonly Color SelectedBg  = Color.FromArgb(210, 50, 105, 190);

    private const int ItemH = 44;

    public static void Draw(Graphics g, Rectangle bounds, MainMenuScreen menu)
    {
        g.CompositingMode   = CompositingMode.SourceOver;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 1. Background image or solid colour
        if (menu.Background != null)
        {
            // Scale to fill the panel, centred (cover behaviour)
            var src    = new Rectangle(0, 0, menu.Background.Width, menu.Background.Height);
            float imgAspect   = (float)src.Width / src.Height;
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
            g.DrawImage(menu.Background, dest, src, GraphicsUnit.Pixel);
        }
        else
        {
            using var bg = new SolidBrush(BgFallback);
            g.FillRectangle(bg, bounds);
        }

        // 2. Dim overlay to ensure readability
        using var dimBrush = new SolidBrush(OverlayDim);
        g.FillRectangle(dimBrush, bounds);

        // 3. Menu panel — centred, bottom-third of screen
        var items  = MainMenuScreen.Items;
        int panelW = Math.Min(360, bounds.Width - 80);
        int panelH = 52 + items.Length * ItemH + 16;
        int panelX = (bounds.Width - panelW) / 2;
        int panelY = bounds.Height - panelH - (bounds.Height / 6); // bottom third

        panelX = Math.Max(8, panelX);
        panelY = Math.Max(8, panelY);

        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        using var panelBrush = new SolidBrush(PanelColor);
        g.FillRectangle(panelBrush, panelRect);

        using var borderPen = new Pen(BorderColor, 2f);
        g.DrawRectangle(borderPen, panelRect);

        // 4. Title text above items
        using var titleFont  = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
        using var titleBrush = new SolidBrush(TitleColor);
        var titleRect = new RectangleF(panelX + 8, panelY + 8, panelW - 16, 36);
        var centred   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("MAIN MENU", titleFont, titleBrush, titleRect, centred);

        // Divider
        using var divPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
        g.DrawLine(divPen, panelX + 12, panelY + 46, panelX + panelW - 12, panelY + 46);

        // 5. Items
        using var itemFont  = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point);
        using var selFont   = new Font("Segoe UI", 13f, FontStyle.Bold,    GraphicsUnit.Point);
        using var enaBrush  = new SolidBrush(ItemEnabled);
        using var disBrush  = new SolidBrush(ItemDisabled);
        using var selBrush  = new SolidBrush(SelectedBg);
        var leftFmt = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        for (int i = 0; i < items.Length; i++)
        {
            bool enabled  = menu.IsItemEnabled(i);
            bool selected = i == menu.SelectedIndex;

            var itemRect = new Rectangle(
                panelX + 6,
                panelY + 50 + i * ItemH,
                panelW - 12,
                ItemH - 2);

            if (selected && enabled)
            {
                g.FillRectangle(selBrush, itemRect);
                string label = "▶  " + items[i];
                g.DrawString(label, selFont, enaBrush, (RectangleF)itemRect, leftFmt);
            }
            else if (enabled)
            {
                g.DrawString(items[i], itemFont, enaBrush, (RectangleF)itemRect, leftFmt);
            }
            else
            {
                // Disabled (Resume Game when no save exists)
                string label = items[i] + "  (no save)";
                g.DrawString(label, itemFont, disBrush, (RectangleF)itemRect, leftFmt);
            }
        }
    }
}

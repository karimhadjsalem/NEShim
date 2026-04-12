using System.Drawing;
using System.Drawing.Drawing2D;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the in-game pause menu overlay.
/// Draws on top of the frozen game frame using System.Drawing primitives only.
/// </summary>
internal static class MenuRenderer
{
    private static readonly Color OverlayColor   = Color.FromArgb(180, 0, 0, 0);
    private static readonly Color PanelColor     = Color.FromArgb(220, 20, 20, 35);
    private static readonly Color SelectedBg     = Color.FromArgb(200, 60, 120, 200);
    private static readonly Color TitleColor     = Color.FromArgb(255, 180, 220, 255);
    private static readonly Color ItemColor      = Color.White;
    private static readonly Color DimColor       = Color.FromArgb(160, 180, 180, 180);

    public static void Draw(Graphics g, Rectangle bounds, InGameMenu menu)
    {
        g.CompositingMode = CompositingMode.SourceOver;

        // Semi-transparent full-screen dim
        using var overlayBrush = new SolidBrush(OverlayColor);
        g.FillRectangle(overlayBrush, bounds);

        // Menu panel
        int panelW = Math.Min(400, bounds.Width - 80);
        int itemH  = 40;
        var items  = menu.GetCurrentItems();
        int panelH = 60 + items.Length * itemH + 20;
        int panelX = (bounds.Width  - panelW) / 2;
        int panelY = (bounds.Height - panelH) / 2;
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        using var panelBrush = new SolidBrush(PanelColor);
        g.FillRectangle(panelBrush, panelRect);

        // Panel border
        using var borderPen = new Pen(Color.FromArgb(200, 80, 140, 220), 2);
        g.DrawRectangle(borderPen, panelRect);

        // Title
        using var titleFont = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Point);
        using var titleBrush = new SolidBrush(TitleColor);
        var titleRect = new RectangleF(panelX, panelY + 10, panelW, 40);
        var titleFormat = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString(menu.GetTitle(), titleFont, titleBrush, titleRect, titleFormat);

        // Divider
        using var divPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1);
        g.DrawLine(divPen, panelX + 16, panelY + 52, panelX + panelW - 16, panelY + 52);

        // Items
        using var itemFont    = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point);
        using var selFont     = new Font("Segoe UI", 13f, FontStyle.Bold,    GraphicsUnit.Point);
        using var itemBrush   = new SolidBrush(ItemColor);
        using var dimBrush    = new SolidBrush(DimColor);
        using var selBrush    = new SolidBrush(SelectedBg);
        var itemFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < items.Length; i++)
        {
            var itemRect = new Rectangle(panelX + 8, panelY + 58 + i * itemH, panelW - 16, itemH - 2);

            if (i == menu.SelectedItem)
            {
                g.FillRectangle(selBrush, itemRect);
                g.DrawString("▶ " + items[i], selFont, itemBrush, (RectangleF)itemRect, itemFormat);
            }
            else
            {
                g.DrawString("  " + items[i], itemFont, dimBrush, (RectangleF)itemRect, itemFormat);
            }
        }
    }
}

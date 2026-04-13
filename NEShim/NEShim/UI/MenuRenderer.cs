using System.Drawing;
using System.Drawing.Drawing2D;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for the in-game pause menu overlay.
/// Draws on top of the frozen game frame using System.Drawing primitives only.
/// </summary>
internal static class MenuRenderer
{
    private static readonly Color OverlayColor  = Color.FromArgb(180, 0, 0, 0);
    private static readonly Color PanelColor    = Color.FromArgb(230, 18, 18, 32);
    private static readonly Color SelectedBg    = Color.FromArgb(210, 55, 110, 195);
    private static readonly Color TitleColor    = Color.FromArgb(255, 175, 215, 255);
    private static readonly Color SubtitleColor = Color.FromArgb(255, 255, 200, 100);
    private static readonly Color WarningColor  = Color.FromArgb(255, 255, 120,  60);
    private static readonly Color ItemColor     = Color.White;
    private static readonly Color DimColor      = Color.FromArgb(170, 190, 190, 190);
    private static readonly Color BorderColor   = Color.FromArgb(200, 75, 135, 215);
    private static readonly Color WarningBorder = Color.FromArgb(200, 200, 90, 40);

    internal const int ItemH    = 38;
    private  const int PanelPad = 16;

    // ---- Hit testing ----

    /// <summary>
    /// Returns the index of the item at <paramref name="p"/>, or -1 if none.
    /// Mirrors the item-rect calculation in <see cref="Draw"/>.
    /// </summary>
    public static int HitTestItem(Point p, Rectangle bounds, InGameMenu menu)
    {
        if (menu.RebindingAction != null) return -1;

        var  items       = menu.GetCurrentItems();
        bool isConfirm   = menu.Current == InGameMenu.Screen.ConfirmMainMenu
                        || menu.Current == InGameMenu.Screen.ConfirmExit;
        int  warningRowH = isConfirm ? ItemH : 0;

        var (panelX, panelY, panelW, _) = PanelMetrics(bounds, items.Length, warningRowH);

        for (int i = 0; i < items.Length; i++)
        {
            var itemRect = new Rectangle(
                panelX + 6,
                panelY + 56 + warningRowH + i * ItemH,
                panelW - 12,
                ItemH - 2);
            if (itemRect.Contains(p)) return i;
        }
        return -1;
    }

    // ---- Drawing ----

    public static void Draw(Graphics g, Rectangle bounds, InGameMenu menu)
    {
        g.CompositingMode = CompositingMode.SourceOver;

        using var overlayBrush = new SolidBrush(OverlayColor);
        g.FillRectangle(overlayBrush, bounds);

        var    items      = menu.GetCurrentItems();
        string title      = menu.GetTitle();
        bool   isConfirm  = menu.Current == InGameMenu.Screen.ConfirmMainMenu
                         || menu.Current == InGameMenu.Screen.ConfirmExit;
        int    warningRowH = isConfirm ? ItemH : 0;

        var (panelX, panelY, panelW, panelH) = PanelMetrics(bounds, items.Length, warningRowH);
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        using var panelBrush = new SolidBrush(PanelColor);
        g.FillRectangle(panelBrush, panelRect);

        using var borderPen = new Pen(isConfirm ? WarningBorder : BorderColor, 2f);
        g.DrawRectangle(borderPen, panelRect);

        // Title
        using var titleFont  = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Point);
        var titleColor = isConfirm                   ? WarningColor
                       : menu.RebindingAction != null ? SubtitleColor
                       : TitleColor;
        using var titleBrush = new SolidBrush(titleColor);
        var titleRect = new RectangleF(panelX + PanelPad, panelY + 10, panelW - PanelPad * 2, 36);
        var centred   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(title, titleFont, titleBrush, titleRect, centred);

        // Divider
        using var divPen = new Pen(Color.FromArgb(70, 255, 255, 255), 1);
        g.DrawLine(divPen, panelX + PanelPad, panelY + 50, panelX + panelW - PanelPad, panelY + 50);

        // Warning label on confirm screens
        if (isConfirm)
        {
            using var warnFont  = new Font("Segoe UI", 11f, FontStyle.Italic, GraphicsUnit.Point);
            using var warnBrush = new SolidBrush(Color.FromArgb(200, 255, 180, 100));
            var warnRect = new RectangleF(panelX + PanelPad, panelY + 52, panelW - PanelPad * 2, 28);
            g.DrawString("Unsaved progress will be lost.", warnFont, warnBrush, warnRect, centred);
        }

        // Rebind prompt replaces the item list
        if (menu.RebindingAction != null)
        {
            using var hintFont  = new Font("Segoe UI", 13f, FontStyle.Italic, GraphicsUnit.Point);
            using var hintBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 180));
            var hintRect = new RectangleF(panelX + PanelPad, panelY + 56, panelW - PanelPad * 2,
                                           panelH - 56 - PanelPad);
            g.DrawString("Press any key to bind\n(Esc to cancel)", hintFont, hintBrush, hintRect, centred);
            return;
        }

        // Item list
        using var itemFont  = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
        using var selFont   = new Font("Segoe UI", 12f, FontStyle.Bold,    GraphicsUnit.Point);
        using var itemBrush = new SolidBrush(ItemColor);
        using var dimBrush  = new SolidBrush(DimColor);
        using var selBrush  = new SolidBrush(SelectedBg);
        var leftFmt = new StringFormat
        {
            Alignment     = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.EllipsisCharacter,
        };

        for (int i = 0; i < items.Length; i++)
        {
            var itemRect = new Rectangle(
                panelX + 6,
                panelY + 56 + warningRowH + i * ItemH,
                panelW - 12,
                ItemH - 2);

            bool enabled  = menu.IsItemEnabled(i);
            bool selected = i == menu.SelectedItem && enabled;

            if (selected)
            {
                g.FillRectangle(selBrush, itemRect);
                g.DrawString("▶  " + items[i], selFont, itemBrush, (RectangleF)itemRect, leftFmt);
            }
            else if (enabled)
            {
                g.DrawString("    " + items[i], itemFont, itemBrush, (RectangleF)itemRect, leftFmt);
            }
            else
            {
                g.DrawString("    " + items[i] + "  (no save)", itemFont, dimBrush, (RectangleF)itemRect, leftFmt);
            }
        }
    }

    // ---- Shared layout calculation ----

    private static (int panelX, int panelY, int panelW, int panelH) PanelMetrics(
        Rectangle bounds, int itemCount, int warningRowH)
    {
        int panelW = Math.Min(440, bounds.Width - 60);
        int panelH = 64 + warningRowH + itemCount * ItemH + PanelPad;
        int panelX = Math.Max(8, (bounds.Width  - panelW) / 2);
        int panelY = Math.Max(8, (bounds.Height - panelH) / 2);
        return (panelX, panelY, panelW, panelH);
    }
}

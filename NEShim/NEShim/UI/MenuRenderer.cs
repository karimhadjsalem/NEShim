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
    private static readonly Color AmberColor    = Color.FromArgb(255, 220, 140);

    internal const int ItemH           = 38;
    private  const int PanelPad        = 16;
    private  const int SeparatorH      = 18;
    private  const int ControllerAreaW = 260;  // width of the right-side controller column
    private  const int FullPanelW      = 520;  // panel width when controller is shown
    private  const int SlimPanelW      = 440;  // panel width when controller is hidden
    private  const int MinWidthForCtrl = 580;  // minimum bounds.Width to show controller

    // ---- Hit testing ----

    /// <summary>
    /// Returns the index of the item at <paramref name="p"/>, or -1 if none.
    /// Mirrors the item-rect calculation in <see cref="Draw"/>.
    /// </summary>
    public static int HitTestItem(Point p, Rectangle bounds, InGameMenu menu)
    {
        if (menu.RebindingAction != null || menu.IsGamepadRebinding) return -1;

        var  items        = menu.GetCurrentItems();
        bool isConfirm    = menu.Current == InGameMenu.Screen.ConfirmMainMenu
                         || menu.Current == InGameMenu.Screen.ConfirmExit;
        int  warningRowH  = isConfirm ? ItemH : 0;
        int  openMenuIdx  = menu.Current == InGameMenu.Screen.GamepadBindings
                            ? menu.OpenMenuBindingIndex : -1;
        bool hasSeparator = openMenuIdx >= 0;
        bool showCtrl     = ShouldShowController(bounds, menu.Current);

        var (panelX, panelY, _, _, listW) = PanelMetrics(bounds, items.Length, warningRowH, hasSeparator, showCtrl);

        for (int i = 0; i < items.Length; i++)
        {
            int extraY = hasSeparator && i >= openMenuIdx ? SeparatorH : 0;
            var itemRect = new Rectangle(
                panelX + 6,
                panelY + 56 + warningRowH + i * ItemH + extraY,
                listW - 12,
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

        var    items         = menu.GetCurrentItems();
        string title         = menu.GetTitle();
        bool   isConfirm     = menu.Current == InGameMenu.Screen.ConfirmMainMenu
                            || menu.Current == InGameMenu.Screen.ConfirmExit;
        int    warningRowH   = isConfirm ? ItemH : 0;
        int    openMenuIdx   = menu.Current == InGameMenu.Screen.GamepadBindings
                               ? menu.OpenMenuBindingIndex : -1;
        bool   hasSeparator  = openMenuIdx >= 0;
        bool   showCtrl      = ShouldShowController(bounds, menu.Current);

        var (panelX, panelY, panelW, panelH, listW) = PanelMetrics(bounds, items.Length, warningRowH, hasSeparator, showCtrl);
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

        using var panelBrush = new SolidBrush(PanelColor);
        g.FillRectangle(panelBrush, panelRect);

        using var borderPen = new Pen(isConfirm ? WarningBorder : BorderColor, 2f);
        g.DrawRectangle(borderPen, panelRect);

        // Title
        using var titleFont  = new Font(menu.Localization.FontFamily, 15f, FontStyle.Bold, GraphicsUnit.Point);
        var titleColor = isConfirm                                          ? WarningColor
                       : (menu.RebindingAction != null || menu.IsGamepadRebinding) ? SubtitleColor
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
            using var warnFont  = new Font(menu.Localization.FontFamily, 11f, FontStyle.Italic, GraphicsUnit.Point);
            using var warnBrush = new SolidBrush(Color.FromArgb(200, 255, 180, 100));
            var warnRect = new RectangleF(panelX + PanelPad, panelY + 52, panelW - PanelPad * 2, 28);
            g.DrawString(menu.Localization.InGameConfirmWarning, warnFont, warnBrush, warnRect, centred);
        }

        // Controller diagram on the right side of binding screens
        if (showCtrl)
        {
            using var vDivPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1);
            g.DrawLine(vDivPen, panelX + listW, panelY + 8, panelX + listW, panelY + panelH - 8);

            var ctrlArea = new RectangleF(panelX + listW + 6, panelY + 14, ControllerAreaW - 10, panelH - 28);
            NesControllerDiagram.Draw(g, ctrlArea, menu.ActiveNesButton, menu.Localization.NesControllerLabel);
        }

        // Rebind prompt (left portion)
        if (menu.RebindingAction != null || menu.IsGamepadRebinding)
        {
            string hint = menu.IsGamepadRebinding
                ? (menu.OverrideStartBindingProtection
                    ? menu.Localization.InGameRebindPressButtonNoCancel
                    : menu.Localization.InGameRebindPressButton)
                : menu.Localization.InGameRebindPressKey;
            using var hintFont  = new Font(menu.Localization.FontFamily, 13f, FontStyle.Italic, GraphicsUnit.Point);
            using var hintBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 180));
            var hintRect = new RectangleF(panelX + PanelPad, panelY + 56, listW - PanelPad * 2,
                                           panelH - 56 - PanelPad);
            g.DrawString(hint, hintFont, hintBrush, hintRect, centred);
            return;
        }

        // Item list (left portion)
        using var itemFont   = new Font(menu.Localization.FontFamily, 12f, FontStyle.Regular, GraphicsUnit.Point);
        using var selFont    = new Font(menu.Localization.FontFamily, 12f, FontStyle.Bold,    GraphicsUnit.Point);
        using var itemBrush  = new SolidBrush(ItemColor);
        using var amberBrush = new SolidBrush(AmberColor);
        using var dimBrush   = new SolidBrush(DimColor);
        using var selBrush   = new SolidBrush(SelectedBg);
        var leftFmt = new StringFormat
        {
            Alignment     = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.EllipsisCharacter,
        };

        for (int i = 0; i < items.Length; i++)
        {
            // Draw separator before the OpenMenu system entry
            if (hasSeparator && i == openMenuIdx)
            {
                int sepLineY = panelY + 56 + warningRowH + i * ItemH + 2;
                using var sepPen   = new Pen(Color.FromArgb(70, 255, 255, 255), 1);
                using var sepFont  = new Font(menu.Localization.FontFamily, 8f, FontStyle.Regular, GraphicsUnit.Point);
                using var sepBrush = new SolidBrush(Color.FromArgb(140, 180, 180, 180));
                g.DrawLine(sepPen, panelX + PanelPad, sepLineY, panelX + listW - PanelPad, sepLineY);
                var nearFmt  = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
                var sepRect  = new RectangleF(panelX + PanelPad, sepLineY + 3, listW - PanelPad * 2, 12);
                g.DrawString(menu.Localization.SystemSectionLabel, sepFont, sepBrush, sepRect, nearFmt);
            }

            int extraY = hasSeparator && i >= openMenuIdx ? SeparatorH : 0;
            var itemRect = new Rectangle(
                panelX + 6,
                panelY + 56 + warningRowH + i * ItemH + extraY,
                listW - 12,
                ItemH - 2);

            bool  enabled     = menu.IsItemEnabled(i);
            bool  selected    = i == menu.SelectedItem && enabled;
            bool  isOpenMenu  = openMenuIdx >= 0 && i == openMenuIdx;
            Brush activeBrush = isOpenMenu ? amberBrush : itemBrush;

            if (selected)
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

    // ---- Shared layout calculation ----

    private static bool ShouldShowController(Rectangle bounds, InGameMenu.Screen screen) =>
        bounds.Width >= MinWidthForCtrl
        && (screen == InGameMenu.Screen.KeyboardBindings
            || screen == InGameMenu.Screen.GamepadBindings);

    private static (int panelX, int panelY, int panelW, int panelH, int listW) PanelMetrics(
        Rectangle bounds, int itemCount, int warningRowH, bool hasSeparator, bool showCtrl)
    {
        int panelW = showCtrl
            ? Math.Min(FullPanelW, bounds.Width - 60)
            : Math.Min(SlimPanelW, bounds.Width - 60);
        int listW  = showCtrl ? panelW - ControllerAreaW : panelW;
        int panelH = 64 + warningRowH + itemCount * ItemH + PanelPad + (hasSeparator ? SeparatorH : 0);
        int panelX = Math.Max(8, (bounds.Width  - panelW) / 2);
        int panelY = Math.Max(8, (bounds.Height - panelH) / 2);
        return (panelX, panelY, panelW, panelH, listW);
    }
}

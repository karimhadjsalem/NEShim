using System.Drawing;
using System.Drawing.Drawing2D;

namespace NEShim.Rendering;

/// <summary>
/// Stateless GDI+ drawing helpers for in-game overlays (FPS counter, toast, achievement notification,
/// sidebar images). Used by GamePanel.OnPaint (GDI+ path) and D3D11Renderer (renders to an off-screen
/// Bitmap that is then uploaded to a D3D11 overlay texture).
/// </summary>
internal static class OverlayRenderer
{
    private const string OverlayFontFamily = "Segoe UI";

    internal const double ToastDurationSeconds    = 1.5;
    private  const float  ToastFontSize           = 14f;
    private  const float  ToastBottomPad          = 30f;
    private  const int    ToastBgPadX             = 8;
    private  const int    ToastBgPadY             = 4;
    private  const int    ToastBgAlpha            = 160;

    internal const int    AchievementDurationSeconds = 5;
    private  const float  AchievementMargin       = 15f;
    private  const float  AchievementPadding      = 10f;
    private  const float  AchievementInnerGap     = 4f;
    private  const float  AchievementHeaderSize   = 10f;
    private  const float  AchievementNameSize     = 13f;
    private static readonly Color AchievementBgColor     = Color.FromArgb(210, 20, 20, 20);
    private static readonly Color AchievementBorderColor = Color.FromArgb(200, 200, 160, 40);
    private static readonly Color AchievementHeaderColor = Color.FromArgb(255, 220, 180, 50);

    private const float FpsFontSize = 11f;
    private const float FpsRightPad = 10f;
    private const float FpsTopPad   = 8f;
    private const int   FpsBgAlpha  = 140;
    private static readonly Color FpsTextColor = Color.FromArgb(255, 180, 255, 120);

    internal static void DrawFps(Graphics g, Rectangle clientRect, float fps)
    {
        string text = $"{fps:F1} fps";
        using var font = new Font(OverlayFontFamily, FpsFontSize, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        float x = clientRect.Width  - size.Width  - FpsRightPad;
        float y = FpsTopPad;

        g.CompositingMode = CompositingMode.SourceOver;
        using var bg = new SolidBrush(Color.FromArgb(FpsBgAlpha, 0, 0, 0));
        g.FillRectangle(bg, x - 4, y - 2, size.Width + 8, size.Height + 4);
        using var fg = new SolidBrush(FpsTextColor);
        g.DrawString(text, font, fg, x, y);
    }

    internal static void DrawToast(Graphics g, Rectangle clientRect, string text)
    {
        using var font = new Font(OverlayFontFamily, ToastFontSize, FontStyle.Bold, GraphicsUnit.Point);
        var size = g.MeasureString(text, font);
        float x = (clientRect.Width  - size.Width)  / 2f;
        float y =  clientRect.Height - size.Height  - ToastBottomPad;

        g.CompositingMode = CompositingMode.SourceOver;
        using var bg = new SolidBrush(Color.FromArgb(ToastBgAlpha, 0, 0, 0));
        g.FillRectangle(bg, x - ToastBgPadX, y - ToastBgPadY, size.Width + ToastBgPadX * 2, size.Height + ToastBgPadY * 2);
        using var fg = new SolidBrush(Color.White);
        g.DrawString(text, font, fg, x, y);
    }

    internal static void DrawAchievementNotification(Graphics g, Rectangle clientRect, string displayName)
    {
        const string header = "Achievement Unlocked!";

        using var headerFont = new Font(OverlayFontFamily, AchievementHeaderSize, FontStyle.Bold, GraphicsUnit.Point);
        using var nameFont   = new Font(OverlayFontFamily, AchievementNameSize,   FontStyle.Bold, GraphicsUnit.Point);

        var headerSize = g.MeasureString(header,      headerFont);
        var nameSize   = g.MeasureString(displayName, nameFont);

        float boxW = Math.Max(headerSize.Width, nameSize.Width) + AchievementPadding * 2;
        float boxH = headerSize.Height + nameSize.Height + AchievementPadding * 2 + AchievementInnerGap;
        float boxX = clientRect.Width  - boxW - AchievementMargin;
        float boxY = clientRect.Height - boxH - AchievementMargin;

        g.CompositingMode = CompositingMode.SourceOver;
        using var bg     = new SolidBrush(AchievementBgColor);
        using var border = new Pen(AchievementBorderColor, 1.5f);
        g.FillRectangle(bg, boxX, boxY, boxW, boxH);
        g.DrawRectangle(border, boxX, boxY, boxW, boxH);

        using var headerBrush = new SolidBrush(AchievementHeaderColor);
        using var nameBrush   = new SolidBrush(Color.White);
        g.DrawString(header,      headerFont, headerBrush, boxX + AchievementPadding, boxY + AchievementPadding);
        g.DrawString(displayName, nameFont,   nameBrush,   boxX + AchievementPadding, boxY + AchievementPadding + headerSize.Height + AchievementInnerGap);
    }

    internal static (RectangleF src, Rectangle dst) ComputeSidebarCover(Size imageSize, Rectangle dest)
    {
        float scale = Math.Max((float)dest.Width / imageSize.Width, (float)dest.Height / imageSize.Height);
        float srcW  = dest.Width  / scale;
        float srcH  = dest.Height / scale;
        float srcX  = (imageSize.Width  - srcW) / 2f;
        float srcY  = (imageSize.Height - srcH) / 2f;
        return (new RectangleF(srcX, srcY, srcW, srcH), dest);
    }

    internal static void DrawSidebar(Graphics g, Bitmap bmp, Rectangle dest)
    {
        var (src, dst) = ComputeSidebarCover(bmp.Size, dest);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
    }
}

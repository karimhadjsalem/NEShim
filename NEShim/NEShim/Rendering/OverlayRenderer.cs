using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Stateless SDL3PaintContext drawing helpers for in-game overlays (FPS counter, toast).
/// Used by D3D11Renderer via SDL software renderer into the overlay surface.
/// </summary>
internal static class OverlayRenderer
{
    private const string OverlayFontFamily = "Segoe UI";

    internal const double ToastDurationSeconds = 1.5;
    private  const float  ToastFontSize        = 14f;
    private  const float  ToastBottomPad       = 30f;
    private  const int    ToastBgPadX          = 8;
    private  const int    ToastBgPadY          = 4;
    private  const byte   ToastBgAlpha         = 160;

    private const float FpsFontSize = 11f;
    private const float FpsRightPad = 10f;
    private const float FpsTopPad   = 8f;
    private const byte  FpsBgAlpha  = 140;
    private static readonly SDL.Color FpsTextColor = new() { R = 180, G = 255, B = 120, A = 255 };

    internal static void DrawFps(SDL3PaintContext ctx, SDL.Rect clientRect, float fps)
    {
        string text = $"{fps:F1} fps";
        var (textW, textH) = ctx.MeasureText(text, OverlayFontFamily, FpsFontSize, bold: true);
        float x = clientRect.W - textW - FpsRightPad;
        float y = FpsTopPad;

        ctx.FillRect(
            new SDL.FRect { X = x - 4, Y = y - 2, W = textW + 8, H = textH + 4 },
            new SDL.Color { R = 0, G = 0, B = 0, A = FpsBgAlpha });
        ctx.DrawText(text,
            new SDL.FRect { X = x, Y = y, W = textW, H = textH },
            FpsTextColor, OverlayFontFamily, FpsFontSize, bold: true,
            TextHAlign.Near, TextVAlign.Top);
    }

    internal static void DrawToast(SDL3PaintContext ctx, SDL.Rect clientRect, string text)
    {
        var (textW, textH) = ctx.MeasureText(text, OverlayFontFamily, ToastFontSize, bold: true);
        float x = (clientRect.W - textW) / 2f;
        float y = clientRect.H - textH  - ToastBottomPad;

        ctx.FillRect(
            new SDL.FRect { X = x - ToastBgPadX, Y = y - ToastBgPadY, W = textW + ToastBgPadX * 2, H = textH + ToastBgPadY * 2 },
            new SDL.Color { R = 0, G = 0, B = 0, A = ToastBgAlpha });
        ctx.DrawText(text,
            new SDL.FRect { X = x, Y = y, W = textW, H = textH },
            new SDL.Color { R = 255, G = 255, B = 255, A = 255 },
            OverlayFontFamily, ToastFontSize, bold: true,
            TextHAlign.Near, TextVAlign.Top);
    }
}

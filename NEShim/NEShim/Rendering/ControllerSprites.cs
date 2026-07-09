using System.Reflection;
using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Provides the base NES controller surface and draws per-button highlights at runtime.
/// One SDL surface for the base image; highlights are drawn over it by <see cref="DrawHighlight"/>.
/// </summary>
internal static class ControllerSprites
{
    private static IntPtr _base = LoadBase();

    private static readonly SDL.Color HighlightColor = new() { R = 55, G = 110, B = 195, A = 190 };

    internal static IntPtr Base => _base;

    internal static void DrawHighlight(SDL3PaintContext ctx, SDL.Rect ctrlRect, string? activeButton)
    {
        if (activeButton is null) return;

        float aw = ctrlRect.W, ah = ctrlRect.H;
        float ox = ctrlRect.X, oy = ctrlRect.Y;

        float dcx  = ox + 0.173f * aw, dcy  = oy + 0.500f * ah;
        float armT = 0.058f * aw,      armL = 0.090f * aw;
        float pad  = 0.010f * aw,      halfT = armT * 0.5f;

        float cpX   = ox + 0.356f * aw,  cpW  = 0.258f * aw;
        float baseY = oy + 0.622f * ah;
        float pillY = baseY - 0.035f * ah + 0.188f * ah * 0.33f;
        float pillW = 0.090f * aw,        pillH = 0.070f * ah;
        float selCx = cpX + cpW * 0.25f,  staCx = cpX + cpW * 0.75f;

        float btnR = 0.048f * aw;
        float bcx  = ox + 0.722f * aw, bcy = oy + 0.636f * ah;
        float acx  = ox + 0.862f * aw, acy = oy + 0.636f * ah;

        switch (activeButton)
        {
            case "P1 Up":
                ctx.FillRect(new SDL.FRect { X = dcx - halfT - pad, Y = dcy - armL - pad, W = armT + 2f * pad, H = armL - halfT + 2f * pad }, HighlightColor);
                break;
            case "P1 Down":
                ctx.FillRect(new SDL.FRect { X = dcx - halfT - pad, Y = dcy + halfT - pad, W = armT + 2f * pad, H = armL - halfT + 2f * pad }, HighlightColor);
                break;
            case "P1 Left":
                ctx.FillRect(new SDL.FRect { X = dcx - armL - pad, Y = dcy - halfT - pad, W = armL - halfT + 2f * pad, H = armT + 2f * pad }, HighlightColor);
                break;
            case "P1 Right":
                ctx.FillRect(new SDL.FRect { X = dcx + halfT - pad, Y = dcy - halfT - pad, W = armL - halfT + 2f * pad, H = armT + 2f * pad }, HighlightColor);
                break;
            case "P1 Select":
                ctx.FillRect(new SDL.FRect { X = selCx - pillW * 0.5f - pad, Y = pillY - pillH * 0.33f, W = pillW + 2f * pad, H = pillH + 2f * pad }, HighlightColor);
                break;
            case "P1 Start":
                ctx.FillRect(new SDL.FRect { X = staCx - pillW * 0.5f - pad, Y = pillY - pillH * 0.33f, W = pillW + 2f * pad, H = pillH + 2f * pad }, HighlightColor);
                break;
            case "P1 B":
                ctx.FillEllipse(bcx, bcy, btnR + pad, btnR + pad, HighlightColor);
                break;
            case "P1 A":
                ctx.FillEllipse(acx, acy, btnR + pad, btnR + pad, HighlightColor);
                break;
        }
    }

    internal static void Dispose()
    {
        if (_base != IntPtr.Zero)
            SDL.DestroySurface(_base);
        _base = IntPtr.Zero;
    }

    private static IntPtr LoadBase()
    {
        const string resourceName = "NEShim.Assets.controllers.controller_none.png";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return SdlSurfaceLoader.LoadFromStream(stream);
    }
}

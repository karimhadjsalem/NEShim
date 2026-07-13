using NEShim.Rendering;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for <see cref="GameCarouselScreen"/>. v1 is text-title tiles only — one
/// game's display title shown at a time, Left/Right to switch, no thumbnail art (see
/// <c>GameManifest.ThumbnailPath</c>, reserved but unused). Runs before any game's config is
/// loaded, so — unlike every other menu renderer in this codebase — it has no
/// <c>LocalizationData</c> available; its handful of UI strings are English-only constants
/// (a deliberate v1 simplification, not an oversight).
/// </summary>
internal static class GameCarouselRenderer
{
    private const string FontFamily = "Segoe UI"; // matches LocalizationData's own default

    private static readonly SDL.Color BgColor       = new() { R = 10,  G = 10,  B = 20,  A = 255 };
    private static readonly SDL.Color TitleColor    = new() { R = 195, G = 225, B = 255, A = 255 };
    private static readonly SDL.Color HintColor     = new() { R = 160, G = 160, B = 160, A = 200 };
    private static readonly SDL.Color CounterColor  = new() { R = 130, G = 130, B = 130, A = 180 };
    private static readonly SDL.Color EmptyColor    = new() { R = 200, G = 200, B = 200, A = 220 };
    private static readonly SDL.Color ArrowColor    = new() { R = 110, G = 150, B = 210, A = 220 };

    internal static void Draw(SDL3PaintContext ctx, SDL.Rect bounds, GameCarouselScreen carousel)
    {
        ctx.Clear(BgColor);

        if (carousel.Games.Count == 0)
        {
            ctx.DrawText("No games available", CenterRect(bounds, 0.6f), EmptyColor,
                FontFamily, 16f, bold: true);
            return;
        }

        var game = carousel.Games[carousel.SelectedIndex];

        ctx.DrawText("Select a Game", CenterRect(bounds, 0.25f), HintColor, FontFamily, 12f, bold: false);
        ctx.DrawText(game.DisplayTitle, CenterRect(bounds, 0.45f), TitleColor, FontFamily, 22f, bold: true);

        if (carousel.Games.Count > 1)
        {
            ctx.DrawText("<", ArrowRect(bounds, near: true), ArrowColor, FontFamily, 24f, bold: true);
            ctx.DrawText(">", ArrowRect(bounds, near: false), ArrowColor, FontFamily, 24f, bold: true);
            ctx.DrawText($"{carousel.SelectedIndex + 1} / {carousel.Games.Count}",
                CenterRect(bounds, 0.58f), CounterColor, FontFamily, 11f, bold: false);
        }

        ctx.DrawText("Press Enter to play", CenterRect(bounds, 0.85f), HintColor, FontFamily, 11f, bold: false);
    }

    // A horizontal band centred in bounds, vertically positioned at yFraction of the height.
    private static SDL.FRect CenterRect(SDL.Rect bounds, float yFraction)
    {
        const float BandH = 40f;
        return new SDL.FRect
        {
            X = bounds.X,
            Y = bounds.Y + bounds.H * yFraction - BandH / 2f,
            W = bounds.W,
            H = BandH,
        };
    }

    private static SDL.FRect ArrowRect(SDL.Rect bounds, bool near)
    {
        const float ArrowW = 60f;
        const float ArrowH = 50f;
        float x = near ? bounds.X + bounds.W * 0.12f : bounds.X + bounds.W * 0.88f - ArrowW;
        return new SDL.FRect { X = x, Y = bounds.Y + bounds.H * 0.45f - ArrowH / 2f, W = ArrowW, H = ArrowH };
    }
}

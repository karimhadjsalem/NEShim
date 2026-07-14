using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using SDL3;

namespace NEShim.UI;

/// <summary>
/// Stateless renderer for <see cref="GameCarouselScreen"/>: a filmstrip of box-art tiles
/// (<see cref="ComputeSlotGameIndices"/> maps carousel state to which game occupies each
/// visible slot, wrapping/duplicating games when the library is smaller than the visible
/// slot count), an animated slide when the selection changes, and a card-flip on the
/// selected tile revealing a description (<see cref="ComputeFlipVisual"/>). All player-facing
/// strings come from <see cref="GameCarouselScreen.Localization"/> (resolved from the shell
/// config's <c>language</c> field before the carousel is ever shown — see
/// <c>NEShimApp.InitializeCarousel</c>), same as every other menu renderer in this codebase.
/// </summary>
internal static class GameCarouselRenderer
{
    private const string FontFamily = "Segoe UI"; // matches LocalizationData's own default

    private const int VisibleSlotCount = 5; // center + 2 neighbors each side
    private const float BoxArtAspectRatio = 1.42f; // NES cardboard box front face (~6.5in x 4.5in)
    private const float ArtAreaFraction = 0.78f; // fraction of a slot's height given to box art; rest is the title
    private const int SlideWideSlotCount = VisibleSlotCount + 2; // 2 extra edge slots so tiles entering/exiting stay visible

    // Anchor values at integer |offset from center| 0, 1, 2, ... — interpolated continuously
    // via InterpolateByOffset so a tile's size/opacity change smoothly as it slides between
    // slots, rather than snapping at each offset boundary. Values beyond the array clamp to
    // the last entry. Non-center tiles' title/placeholder-glyph font size also shrinks with
    // this same scale (via *PtSize helpers below) — but the centered tile's text always uses a
    // fixed textScale of 1, decoupled from this array entirely, so it stays full-size and
    // legible even for the "about-to-become-center" tile mid-slide, which would otherwise still
    // be short of scale 1 at that instant (see DrawSlotAt).
    private static readonly float[] SlotScaleByOffset = { 1f, 0.75f, 0.55f };
    private static readonly float[] SlotAlphaByOffset  = { 1f, 0.75f, 0.5f };

    private const float TitlePtSize            = 15f; // the centered tile's title — the single most prominent text on the carousel
    private const float MinTitlePtSize         = 4f; // crash-guard only — non-center tiles are allowed to shrink to illegible; only the centered tile is guaranteed legible (see DrawSlotAt's textScale)
    private const float TitleWrapThreshold     = 1.15f; // a title only wraps once its single-line width exceeds the box art's width by more than this — minor overflow is left alone
    private const float TitleLineSpacing       = 1.15f; // extra breathing room between wrapped title lines, applied on top of the font's own measured line height
    private const float PlaceholderGlyphPtSize = 28f;
    private const float InvalidPtSize          = 10f;
    private const float InvalidSubPtSize       = 8f;
    private const float InvalidLineHeight      = 11f;
    private const float DescriptionPtSize      = 10f;
    private const float DescriptionLineHeight  = 14f;

    private static readonly SDL.Color BgColor              = new() { R = 10,  G = 10,  B = 20,  A = 255 };
    private static readonly SDL.Color TitleColor           = new() { R = 195, G = 225, B = 255, A = 255 };
    private static readonly SDL.Color HintColor             = new() { R = 160, G = 160, B = 160, A = 200 };
    private static readonly SDL.Color EmptyColor            = new() { R = 200, G = 200, B = 200, A = 220 };
    private static readonly SDL.Color PlaceholderFill       = new() { R = 60,  G = 60,  B = 60,  A = 255 };
    private static readonly SDL.Color PlaceholderBorder     = new() { R = 120, G = 120, B = 120, A = 255 };
    private static readonly SDL.Color PlaceholderGlyphColor = new() { R = 180, G = 180, B = 180, A = 255 };
    private static readonly SDL.Color InvalidOverlayFill    = new() { R = 40,  G = 10,  B = 10,  A = 190 };
    private static readonly SDL.Color InvalidTextColor      = new() { R = 255, G = 150, B = 150, A = 255 };
    private static readonly SDL.Color InvalidSubTextColor   = new() { R = 230, G = 190, B = 190, A = 230 };
    private static readonly SDL.Color DescriptionBackFill   = new() { R = 25,  G = 25,  B = 40,  A = 235 };

    internal static void Draw(SDL3PaintContext ctx, SDL.Rect bounds, GameCarouselScreen carousel)
    {
        var loc = carousel.Localization;
        DrawBackground(ctx, bounds, carousel);

        if (carousel.Games.Count == 0)
        {
            ctx.DrawText(loc.CarouselNoGamesAvailable, CenterRect(bounds, 0.5f), EmptyColor, FontFamily, 16f, bold: true);
            return;
        }

        ctx.DrawText(loc.CarouselSelectGame, CenterRect(bounds, 0.10f), HintColor, FontFamily, 12f, bold: false);

        var band = FilmstripBand(bounds);
        if (carousel.SlideDirection != 0 && carousel.SlideProgress < 1f)
            DrawSliding(ctx, band, carousel);
        else
            DrawStatic(ctx, band, carousel);

        // Static control legend — always the same regardless of selection, phrased by direction
        // (Left/Right/Up) rather than a specific key or button so it reads the same whether the
        // player is on keyboard or gamepad. Actions without a natural directional name (select,
        // quit, fullscreen) name their actual bindings instead. No game counter or arrow glyphs —
        // the centered, highlighted tile already shows the selection; a numeric count and "<"/">"
        // hints added nothing the filmstrip itself doesn't already convey.
        ctx.DrawText(loc.CarouselLegendLine1, CenterRect(bounds, 0.89f), HintColor, FontFamily, 11f, bold: false);
        ctx.DrawText(loc.CarouselLegendLine2, CenterRect(bounds, 0.96f), HintColor, FontFamily, 11f, bold: false);
    }

    // ---- Pure layout/geometry helpers (unit tested; no SDL rendering side effects) ----

    /// <summary>
    /// Which game occupies each of <paramref name="visibleSlotCount"/> filmstrip slots
    /// (offsets <c>-half..+half</c> from the centered <paramref name="selectedIndex"/>).
    /// Modulo wraparound naturally duplicates games when <paramref name="gameCount"/> is
    /// smaller than <paramref name="visibleSlotCount"/> — e.g. 2 games across 5 slots yields
    /// the same two games repeated, including the same game at both far edges.
    /// </summary>
    internal static int[] ComputeSlotGameIndices(int selectedIndex, int gameCount, int visibleSlotCount)
    {
        if (gameCount == 0) return Array.Empty<int>();
        int half = visibleSlotCount / 2;
        var result = new int[visibleSlotCount];
        for (int i = 0; i < visibleSlotCount; i++)
        {
            int offset = i - half;
            result[i] = ((selectedIndex + offset) % gameCount + gameCount) % gameCount;
        }
        return result;
    }

    /// <summary>Contain-fits (never crops) <see cref="BoxArtAspectRatio"/> inside <paramref name="slotBounds"/>, centered.</summary>
    internal static SDL.Rect ComputeBoxArtRect(SDL.Rect slotBounds)
    {
        float candidateW = slotBounds.H * BoxArtAspectRatio;
        int w, h;
        if (candidateW <= slotBounds.W) { h = slotBounds.H; w = (int)candidateW; }
        else { w = slotBounds.W; h = (int)(slotBounds.W / BoxArtAspectRatio); }
        int x = slotBounds.X + (slotBounds.W - w) / 2;
        int y = slotBounds.Y + (slotBounds.H - h) / 2;
        return new SDL.Rect { X = x, Y = y, W = w, H = h };
    }

    /// <summary>
    /// A 1 → 0 → 1 horizontal squash approximating a card flip. <paramref name="startWithFront"/>
    /// is the face shown at <paramref name="progress"/> 0; the opposite face shows once progress
    /// crosses the midpoint.
    /// </summary>
    internal static (float horizontalScale, bool showFront) ComputeFlipVisual(float progress, bool startWithFront)
    {
        float p = Math.Clamp(progress, 0f, 1f);
        float scale = MathF.Abs(MathF.Cos(p * MathF.PI));
        bool showFront = startWithFront ? p < 0.5f : p >= 0.5f;
        return (scale, showFront);
    }

    /// <summary>
    /// Scales any base font size by textScale, floored at MinTitlePtSize purely to avoid a
    /// zero/negative point size — not to preserve legibility (see DrawSlotAt's textScale: only
    /// the centered tile is guaranteed a full-size textScale of 1; every other per-tile text
    /// element uses this same helper so none of them are left unscaled by accident).
    /// </summary>
    internal static float ScaledPtSize(float basePtSize, float textScale) => Math.Max(MinTitlePtSize, basePtSize * textScale);

    /// <summary>Convenience wrapper over <see cref="ScaledPtSize"/> for the title font specifically.</summary>
    internal static float ScaledTitlePtSize(float slotScale) => ScaledPtSize(TitlePtSize, slotScale);

    // ---- Draw composition ----

    private static void DrawBackground(SDL3PaintContext ctx, SDL.Rect bounds, GameCarouselScreen carousel)
    {
        var frame = carousel.BackgroundFrame;
        if (frame is { } surface && surface != IntPtr.Zero)
            ctx.BlitSurface(surface, null, bounds);
        else
            ctx.Clear(BgColor);
    }

    private static SDL.Rect FilmstripBand(SDL.Rect bounds) => new()
    {
        X = bounds.X,
        Y = bounds.Y + (int)(bounds.H * 0.18f),
        W = bounds.W,
        H = (int)(bounds.H * 0.62f),
    };

    private static void DrawStatic(SDL3PaintContext ctx, SDL.Rect band, GameCarouselScreen carousel)
    {
        int half = VisibleSlotCount / 2;
        var indices = ComputeSlotGameIndices(carousel.SelectedIndex, carousel.Games.Count, VisibleSlotCount);
        for (int i = 0; i < VisibleSlotCount; i++)
        {
            var game = carousel.Games[indices[i]];
            DrawSlotAt(ctx, band, offsetFromCenter: i - half, game, carousel, allowFlip: true);
        }
    }

    private static void DrawSliding(SDL3PaintContext ctx, SDL.Rect band, GameCarouselScreen carousel)
    {
        int wideHalf = SlideWideSlotCount / 2;
        var indices = ComputeSlotGameIndices(carousel.PreviousSelectedIndex, carousel.Games.Count, SlideWideSlotCount);
        float eased = EaseInOut(carousel.SlideProgress);
        int direction = carousel.SlideDirection;

        for (int i = 0; i < SlideWideSlotCount; i++)
        {
            float oldOffset = i - wideHalf;
            float newOffset = oldOffset - direction; // each old slot slides toward the slot one position closer to the new selection
            float offset = oldOffset + (newOffset - oldOffset) * eased;
            var game = carousel.Games[indices[i]];
            DrawSlotAt(ctx, band, offset, game, carousel, allowFlip: false);
        }
    }

    /// <summary>Cosine ease-in-out — zero velocity at both endpoints, matching the flip's cosine feel.</summary>
    internal static float EaseInOut(float t) => (1f - MathF.Cos(Math.Clamp(t, 0f, 1f) * MathF.PI)) / 2f;

    /// <summary>
    /// Linearly interpolates between the anchor values at integer |offset| = 0, 1, 2, ... so a
    /// tile's scale/alpha changes continuously as it slides, instead of snapping at each
    /// boundary. Offsets beyond the last anchor clamp to that anchor's value.
    /// </summary>
    internal static float InterpolateByOffset(float[] anchors, float absOffset)
    {
        if (absOffset <= 0f) return anchors[0];
        int lower = (int)MathF.Floor(absOffset);
        int upper = lower + 1;
        float t = absOffset - lower;
        float lowerVal = anchors[Math.Min(lower, anchors.Length - 1)];
        float upperVal = anchors[Math.Min(upper, anchors.Length - 1)];
        return lowerVal + (upperVal - lowerVal) * t;
    }

    private static void DrawSlotAt(SDL3PaintContext ctx, SDL.Rect band, float offsetFromCenter,
        GameManifest game, GameCarouselScreen carousel, bool allowFlip)
    {
        float absOffset = MathF.Abs(offsetFromCenter);
        float scale = InterpolateByOffset(SlotScaleByOffset, absOffset);
        float alpha = InterpolateByOffset(SlotAlphaByOffset, absOffset);

        float cellW = band.W / (float)VisibleSlotCount;
        float centerX = band.X + band.W / 2f + offsetFromCenter * cellW;
        float slotW = cellW * scale;
        float slotH = band.H * scale;
        var slotRect = new SDL.Rect
        {
            X = (int)(centerX - slotW / 2f),
            Y = (int)(band.Y + (band.H - slotH) / 2f),
            W = (int)slotW,
            H = (int)slotH,
        };

        // Text size is guaranteed full-size exactly when this slot IS the settled center
        // (offset 0), regardless of the tile's own geometry scale — the only entry that needs
        // to stay legible is the highlighted one; every other slot's text scales down with its
        // tile and is allowed to shrink to illegible at the far edges (see MinTitlePtSize).
        bool isCenter = MathF.Abs(offsetFromCenter) < 0.01f;
        float textScale = isCenter ? 1f : scale;

        if (allowFlip && isCenter && (carousel.DescriptionShown || carousel.FlipProgress < 1f))
        {
            DrawFlippingTile(ctx, slotRect, game, carousel, alpha, textScale);
            return;
        }

        DrawTile(ctx, slotRect, game, carousel, alpha, textScale);
    }

    private static void DrawTile(SDL3PaintContext ctx, SDL.Rect slotRect, GameManifest game,
        GameCarouselScreen carousel, float alpha, float textScale)
    {
        var artArea = new SDL.Rect
        {
            X = slotRect.X, Y = slotRect.Y, W = slotRect.W, H = (int)(slotRect.H * ArtAreaFraction),
        };
        var artRect = ComputeBoxArtRect(artArea);

        if (carousel.TryGetThumbnail(game.GameId, out var surface))
            ctx.BlitSurfaceAlpha(surface, artRect, alpha);
        else
            DrawPlaceholder(ctx, artRect, game, alpha, textScale);

        if (!game.IsValid)
            DrawInvalidOverlay(ctx, artRect, game, carousel.Localization, textScale);

        var titleRect = new SDL.FRect
        {
            X = slotRect.X, Y = artArea.Y + artArea.H, W = slotRect.W, H = slotRect.H - artArea.H,
        };
        DrawTitle(ctx, game.DisplayTitle, titleRect, artRect.W, WithAlpha(TitleColor, alpha), textScale);
    }

    /// <summary>
    /// Draws a tile's title as a single centered line, unless it extends significantly beyond
    /// the box art's width (<see cref="TitleWrapThreshold"/>), in which case it wraps — reusing
    /// the same word-wrap/centering logic as the description and invalid-overlay text.
    /// </summary>
    private static void DrawTitle(SDL3PaintContext ctx, string title, SDL.FRect titleRect, int artWidth,
        SDL.Color color, float textScale)
    {
        float ptSize = ScaledTitlePtSize(textScale);
        var (measuredWidth, measuredHeight) = ctx.MeasureText(title, FontFamily, ptSize, bold: true);

        if (measuredWidth <= artWidth * TitleWrapThreshold)
        {
            ctx.DrawText(title, titleRect, color, FontFamily, ptSize, bold: true);
            return;
        }

        // measuredHeight is the font's own line height at this ptSize (ascent+descent), not just
        // the glyph height — a fixed ptSize-based ratio undershot it for bold text and caused
        // wrapped lines to overlap. TitleLineSpacing adds a little breathing room on top of that.
        DrawWrappedText(ctx, title, titleRect, color, ptSize, measuredHeight * TitleLineSpacing, bold: true);
    }

    private static void DrawFlippingTile(SDL3PaintContext ctx, SDL.Rect slotRect, GameManifest game,
        GameCarouselScreen carousel, float alpha, float textScale)
    {
        // The face shown at the START of the transition is 'front' exactly when the toggle that
        // just fired turned the description ON (DescriptionShown already reflects the new target
        // state — see GameCarouselScreen.ToggleDescription).
        bool startWithFront = carousel.DescriptionShown;
        var (flipScale, showFront) = ComputeFlipVisual(carousel.FlipProgress, startWithFront);

        int squashedW = Math.Max((int)(slotRect.W * flipScale), 1);
        var squashed = new SDL.Rect
        {
            X = slotRect.X + (slotRect.W - squashedW) / 2,
            Y = slotRect.Y,
            W = squashedW,
            H = slotRect.H,
        };

        if (showFront)
            DrawTile(ctx, squashed, game, carousel, alpha, textScale);
        else
            DrawDescriptionBack(ctx, squashed, game, alpha, textScale, carousel.Localization);
    }

    private static void DrawInvalidOverlay(SDL3PaintContext ctx, SDL.Rect artRect, GameManifest game,
        LocalizationData loc, float textScale)
    {
        var f = ToFRect(artRect);
        ctx.FillRect(f, InvalidOverlayFill);

        // Scaled by the same textScale as the title/placeholder glyph — previously fixed-size
        // regardless of tile scale, which made a small, far, INVALID tile's "Unavailable" label
        // stand out larger than a nearby valid tile's correctly-shrunk title.
        float headlinePt = ScaledPtSize(InvalidPtSize, textScale);
        float subPt      = ScaledPtSize(InvalidSubPtSize, textScale);
        float lineHeight = ScaledPtSize(InvalidLineHeight, textScale);

        var headlineRect = new SDL.FRect { X = f.X + 4, Y = f.Y + f.H * 0.08f, W = f.W - 8, H = f.H * 0.24f };
        ctx.DrawText(loc.CarouselUnavailable, headlineRect, InvalidTextColor, FontFamily, headlinePt, bold: true);

        // Wrapped (never overflows the tile, regardless of message length or tile size) and
        // centered — reason plus a short call to action, as one flowing block rather than two
        // fixed single-line bands that could overlap or spill past the tile edge.
        string message = $"{game.ValidationError}. {loc.CarouselContactPublisher}";
        var bodyRect = new SDL.FRect { X = f.X + 4, Y = f.Y + f.H * 0.34f, W = f.W - 8, H = f.H * 0.62f };
        DrawWrappedText(ctx, message, bodyRect, InvalidSubTextColor, subPt, lineHeight);
    }

    private static void DrawPlaceholder(SDL3PaintContext ctx, SDL.Rect rect, GameManifest game, float alpha, float textScale)
    {
        var f = ToFRect(rect);
        ctx.FillRect(f, WithAlpha(PlaceholderFill, alpha));
        ctx.DrawRect(f, WithAlpha(PlaceholderBorder, alpha), thickness: 2f);
        string glyph = string.IsNullOrEmpty(game.DisplayTitle) ? "?" : game.DisplayTitle[0].ToString().ToUpperInvariant();
        float glyphPtSize = ScaledPtSize(PlaceholderGlyphPtSize, textScale);
        ctx.DrawText(glyph, f, WithAlpha(PlaceholderGlyphColor, alpha), FontFamily, glyphPtSize, bold: true);
    }

    private static void DrawDescriptionBack(SDL3PaintContext ctx, SDL.Rect rect, GameManifest game, float alpha,
        float textScale, LocalizationData loc)
    {
        var f = ToFRect(rect);
        ctx.FillRect(f, WithAlpha(DescriptionBackFill, alpha));
        ctx.DrawRect(f, WithAlpha(PlaceholderBorder, alpha), thickness: 2f);

        var titleRect = new SDL.FRect { X = f.X + 8, Y = f.Y + 6, W = f.W - 16, H = 24f };
        ctx.DrawText(game.DisplayTitle, titleRect, WithAlpha(TitleColor, alpha), FontFamily, ScaledTitlePtSize(textScale), bold: true);

        string description = string.IsNullOrWhiteSpace(game.Description) ? loc.CarouselNoDescription : game.Description;
        var bodyRect = new SDL.FRect
        {
            X = f.X + 8, Y = titleRect.Y + titleRect.H + 4, W = f.W - 16, H = f.H - titleRect.H - 16,
        };
        DrawWrappedText(ctx, description, bodyRect, WithAlpha(HintColor, alpha), DescriptionPtSize, DescriptionLineHeight);
    }

    /// <summary>
    /// Word-wraps <paramref name="text"/> to fit <paramref name="rect"/>'s width, then draws it
    /// as a block centered both horizontally (each line) and vertically (the whole block within
    /// the rect) — never overflows the rect; lines beyond what fits are dropped rather than
    /// spilling past the edge.
    /// </summary>
    private static void DrawWrappedText(SDL3PaintContext ctx, string text, SDL.FRect rect, SDL.Color color,
        float ptSize, float lineHeight, bool bold = false)
    {
        var lines = WrapLines(ctx, text, ptSize, rect.W, bold);
        int maxLines = Math.Max(1, (int)(rect.H / lineHeight));
        if (lines.Count > maxLines) lines.RemoveRange(maxLines, lines.Count - maxLines);

        float blockHeight = lines.Count * lineHeight;
        float startY = rect.Y + Math.Max(0f, (rect.H - blockHeight) / 2f);

        for (int i = 0; i < lines.Count; i++)
        {
            var lineRect = new SDL.FRect { X = rect.X, Y = startY + i * lineHeight, W = rect.W, H = lineHeight };
            ctx.DrawText(lines[i], lineRect, color, FontFamily, ptSize, bold,
                halign: TextHAlign.Center, valign: TextVAlign.Center);
        }
    }

    private static List<string> WrapLines(SDL3PaintContext ctx, string text, float ptSize, float maxWidth, bool bold = false)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var line = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            string candidate = line.Length == 0 ? word : $"{line} {word}";
            var (w, _) = ctx.MeasureText(candidate, FontFamily, ptSize, bold);
            if (w > maxWidth && line.Length > 0)
            {
                lines.Add(line.ToString());
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }
        if (line.Length > 0) lines.Add(line.ToString());
        return lines;
    }

    private static SDL.Color WithAlpha(SDL.Color c, float alphaMultiplier) =>
        new() { R = c.R, G = c.G, B = c.B, A = (byte)(c.A * Math.Clamp(alphaMultiplier, 0f, 1f)) };

    private static SDL.FRect ToFRect(SDL.Rect r) => new() { X = r.X, Y = r.Y, W = r.W, H = r.H };

    // A horizontal band centred in bounds, vertically positioned at yFraction of the height.
    private static SDL.FRect CenterRect(SDL.Rect bounds, float yFraction)
    {
        const float BandH = 30f;
        return new SDL.FRect
        {
            X = bounds.X,
            Y = bounds.Y + bounds.H * yFraction - BandH / 2f,
            W = bounds.W,
            H = BandH,
        };
    }
}

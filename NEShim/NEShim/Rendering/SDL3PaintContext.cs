using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Drawing surface passed to stateless renderer classes.
/// Wraps an SDL software renderer. All drawing goes through <c>_renderer</c> so that
/// <c>SDL.RenderPresent</c> correctly flushes the composited frame to the target surface.
/// Does not own the renderer or font cache — caller disposes those.
///
/// <see cref="BlitSurface"/>/<see cref="BlitSurfaceAlpha"/> cache one GPU texture per source
/// surface pointer (<see cref="_textureCache"/>) instead of uploading a fresh texture on every
/// call — repainting several tiles' worth of box art every frame previously re-uploaded each
/// one from scratch each time, a CPU-bound cost proportional to image size and blit count that
/// showed up as choppy carousel animation with real (larger) box art. The cache is scoped to
/// this instance's lifetime: <c>_renderer</c>'s owner destroys the SDL renderer (which SDL
/// itself uses to auto-invalidate every texture created from it) whenever this context is
/// discarded, so no explicit per-texture cleanup is needed there. The one case that DOES need
/// an explicit call is a surface being destroyed while this context is still alive — see
/// <see cref="InvalidateTexture"/>.
/// </summary>
internal sealed class SDL3PaintContext
{
    private readonly IntPtr        _renderer;
    private readonly SDL3FontCache _fontCache;
    private readonly Dictionary<IntPtr, IntPtr> _textureCache = new();

    internal int Width  { get; }
    internal int Height { get; }

    internal SDL3PaintContext(IntPtr renderer, SDL3FontCache fontCache, int width, int height)
    {
        _renderer  = renderer;
        _fontCache = fontCache;
        Width      = width;
        Height     = height;
        SDL.SetRenderDrawBlendMode(_renderer, SDL.BlendMode.Blend);
    }

    /// <summary>
    /// Destroys and evicts the cached GPU texture for <paramref name="surface"/>, if one exists.
    /// Callers MUST call this before destroying a surface they previously passed to
    /// BlitSurface/BlitSurfaceAlpha on this context — otherwise a later, unrelated surface that
    /// happens to be allocated at the same (recycled) address would incorrectly reuse this
    /// texture's stale pixel data instead of getting its own.
    /// </summary>
    internal void InvalidateTexture(IntPtr surface)
    {
        if (_textureCache.Remove(surface, out IntPtr texture))
            SDL.DestroyTexture(texture);
    }

    // ---- Primitives (go through renderer; composited on RenderPresent) ----

    internal void Clear(SDL.Color color)
    {
        SetRenderColor(color);
        SDL.RenderClear(_renderer);
    }

    internal void FillRect(SDL.FRect rect, SDL.Color color)
    {
        SetRenderColor(color);
        SDL.RenderFillRect(_renderer, in rect);
    }

    internal void DrawRect(SDL.FRect rect, SDL.Color color, float thickness)
    {
        SetRenderColor(color);
        float t = thickness;
        var top    = new SDL.FRect { X = rect.X,              Y = rect.Y,              W = rect.W,      H = t };
        var bottom = new SDL.FRect { X = rect.X,              Y = rect.Y + rect.H - t, W = rect.W,      H = t };
        var left   = new SDL.FRect { X = rect.X,              Y = rect.Y + t,          W = t,           H = rect.H - t * 2 };
        var right  = new SDL.FRect { X = rect.X + rect.W - t, Y = rect.Y + t,          W = t,           H = rect.H - t * 2 };
        SDL.RenderFillRect(_renderer, in top);
        SDL.RenderFillRect(_renderer, in bottom);
        SDL.RenderFillRect(_renderer, in left);
        SDL.RenderFillRect(_renderer, in right);
    }

    internal void DrawLine(float x1, float y1, float x2, float y2, SDL.Color color)
    {
        SetRenderColor(color);
        SDL.RenderLine(_renderer, x1, y1, x2, y2);
    }

    internal void FillEllipse(float cx, float cy, float rx, float ry, SDL.Color color)
    {
        SetRenderColor(color);
        int iRy = (int)MathF.Ceiling(ry);
        for (int dy = -iRy; dy <= iRy; dy++)
        {
            float t = dy / ry;
            if (t * t >= 1f) continue;
            float halfW = rx * MathF.Sqrt(1f - t * t);
            var row = new SDL.FRect { X = cx - halfW, Y = cy + dy, W = halfW * 2f, H = 1f };
            SDL.RenderFillRect(_renderer, in row);
        }
    }

    // ---- Text ----

    internal (float w, float h) MeasureText(string text, string fontFamily, float ptSize, bool bold, bool italic = false)
    {
        if (string.IsNullOrEmpty(text)) return (0f, 0f);
        var font = _fontCache.Get(fontFamily, ptSize, bold, italic);
        if (font == IntPtr.Zero) return (0f, 0f);
        TTF.GetStringSize(font, text, UIntPtr.Zero, out int w, out int h);
        return (w, h);
    }

    internal void DrawText(
        string text, SDL.FRect rect, SDL.Color color,
        string fontFamily, float ptSize, bool bold,
        TextHAlign halign = TextHAlign.Center, TextVAlign valign = TextVAlign.Center,
        float tabStop = 0f, bool italic = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (tabStop > 0f)
        {
            var (leftPart, rightPart) = SplitTabText(text);
            DrawSingleText(leftPart,
                new SDL.FRect { X = rect.X, Y = rect.Y, W = tabStop, H = rect.H },
                color, fontFamily, ptSize, bold, TextHAlign.Near, valign, italic);
            if (rightPart != null)
                DrawSingleText(rightPart,
                    new SDL.FRect { X = rect.X + tabStop, Y = rect.Y, W = rect.W - tabStop, H = rect.H },
                    color, fontFamily, ptSize, bold, TextHAlign.Near, valign, italic);
        }
        else
        {
            DrawSingleText(text, rect, color, fontFamily, ptSize, bold, halign, valign, italic);
        }
    }

    internal static (string left, string? right) SplitTabText(string text)
    {
        int idx = text.IndexOf('\t');
        return idx < 0 ? (text, null) : (text[..idx], text[(idx + 1)..]);
    }

    // ---- Surface blitting (via renderer texture path so all content is composited together) ----

    // SDL3's RenderTexture always stretches to fit the destination rect, so BlitSurface
    // handles both pixel-perfect and scaled blits — the dest rect geometry controls the output size.
    internal void BlitSurface(IntPtr surface, SDL.Rect? srcRect, SDL.Rect dstRect)
    {
        if (surface == IntPtr.Zero) return;
        RenderSurface(surface, srcRect.HasValue ? ToFRect(srcRect.Value) : (SDL.FRect?)null, ToFRect(dstRect), alpha: 1f);
    }

    internal void BlitSurfaceAlpha(IntPtr surface, SDL.Rect dstRect, float alpha)
    {
        if (surface == IntPtr.Zero) return;
        RenderSurface(surface, null, ToFRect(dstRect), alpha);
    }

    /// <summary>
    /// Blits without touching <see cref="_textureCache"/> — uploads a fresh texture and destroys
    /// it immediately after drawing. Required for a surface pointer that is reused across frames
    /// with different pixel content behind it (an animation's "current frame" pointer changes
    /// which logical frame it refers to over time from the same handle in some cases) — caching
    /// by pointer would freeze the very first upload's pixels in place forever. Static, one-shot
    /// surfaces (box art, thumbnails) should use <see cref="BlitSurface"/> instead so the cache's
    /// perf benefit still applies where content truly never changes.
    /// </summary>
    internal void BlitSurfaceUncached(IntPtr surface, SDL.Rect dstRect)
    {
        if (surface == IntPtr.Zero) return;
        IntPtr texture = SDL.CreateTextureFromSurface(_renderer, surface);
        if (texture == IntPtr.Zero) return;
        try
        {
            SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
            var dstF = ToFRect(dstRect);
            SDL.RenderTexture(_renderer, texture, IntPtr.Zero, in dstF);
        }
        finally { SDL.DestroyTexture(texture); }
    }

    // ---- Private helpers ----

    private void RenderSurface(IntPtr surface, SDL.FRect? srcFRect, SDL.FRect dstFRect, float alpha)
    {
        if (!_textureCache.TryGetValue(surface, out IntPtr texture))
        {
            texture = SDL.CreateTextureFromSurface(_renderer, surface);
            if (texture == IntPtr.Zero) return;
            SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
            _textureCache[surface] = texture;
        }

        // Set unconditionally (not just "if alpha < 1f") — the texture is now reused across
        // calls, so a previous frame's fade could otherwise leak into this one.
        SDL.SetTextureAlphaMod(texture, (byte)Math.Clamp(alpha * 255f, 0f, 255f));

        if (srcFRect.HasValue)
        {
            var srcF = srcFRect.Value;
            SDL.RenderTexture(_renderer, texture, in srcF, in dstFRect);
        }
        else
        {
            SDL.RenderTexture(_renderer, texture, IntPtr.Zero, in dstFRect);
        }
    }

    private void DrawSingleText(
        string text, SDL.FRect rect, SDL.Color color,
        string fontFamily, float ptSize, bool bold,
        TextHAlign halign, TextVAlign valign, bool italic = false)
    {
        var font = _fontCache.Get(fontFamily, ptSize, bold, italic);
        if (font == IntPtr.Zero) return;

        TTF.GetStringSize(font, text, UIntPtr.Zero, out int textW, out int textH);

        float x = halign switch
        {
            TextHAlign.Near   => rect.X,
            TextHAlign.Center => rect.X + (rect.W - textW) * 0.5f,
            TextHAlign.Far    => rect.X + rect.W - textW,
            _                 => rect.X,
        };
        float y = valign switch
        {
            TextVAlign.Top    => rect.Y,
            TextVAlign.Center => rect.Y + (rect.H - textH) * 0.5f,
            TextVAlign.Bottom => rect.Y + rect.H - textH,
            _                 => rect.Y,
        };

        var textSurface = TTF.RenderTextBlended(font, text, UIntPtr.Zero, color);
        if (textSurface == IntPtr.Zero) return;
        try
        {
            IntPtr texture = SDL.CreateTextureFromSurface(_renderer, textSurface);
            if (texture == IntPtr.Zero) return;
            try
            {
                var dstF = new SDL.FRect { X = x, Y = y, W = textW, H = textH };
                SDL.RenderTexture(_renderer, texture, IntPtr.Zero, in dstF);
            }
            finally { SDL.DestroyTexture(texture); }
        }
        finally { SDL.DestroySurface(textSurface); }
    }

    private void SetRenderColor(SDL.Color color) =>
        SDL.SetRenderDrawColor(_renderer, color.R, color.G, color.B, color.A);

    private static SDL.FRect ToFRect(SDL.Rect r) =>
        new() { X = r.X, Y = r.Y, W = r.W, H = r.H };

    /// <summary>
    /// Returns the pixel dimensions of an SDL_Surface. Isolates the unsafe pointer dereference
    /// required because SDL3-CS does not expose a managed surface-size API.
    /// </summary>
    internal static unsafe (int Width, int Height) GetSurfaceSize(IntPtr surface)
    {
        if (surface == IntPtr.Zero) return (0, 0);
        SDL.Surface* surf = (SDL.Surface*)surface;
        return (surf->Width, surf->Height);
    }
}

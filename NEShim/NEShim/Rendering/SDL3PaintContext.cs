using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Drawing surface passed to stateless renderer classes.
/// Wraps an SDL software renderer. All drawing goes through <c>_renderer</c> so that
/// <c>SDL.RenderPresent</c> correctly flushes the composited frame to the target surface.
/// Does not own the renderer or font cache — caller disposes those.
/// </summary>
internal sealed class SDL3PaintContext
{
    private readonly IntPtr        _renderer;
    private readonly SDL3FontCache _fontCache;

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

    // ---- Primitives (go through renderer; composited on RenderPresent) ----

    internal void Clear(SDL.Color color)
    {
        SetRenderColor(color);
        SDL.RenderClear(_renderer);
    }

    internal void FillRect(SDL.FRect rect, SDL.Color color)
    {
        SetRenderColor(color);
        SDL.RenderFillRect(_renderer, ref rect);
    }

    internal void DrawRect(SDL.FRect rect, SDL.Color color, float thickness)
    {
        SetRenderColor(color);
        float t = thickness;
        var top    = new SDL.FRect { X = rect.X,              Y = rect.Y,              W = rect.W,      H = t };
        var bottom = new SDL.FRect { X = rect.X,              Y = rect.Y + rect.H - t, W = rect.W,      H = t };
        var left   = new SDL.FRect { X = rect.X,              Y = rect.Y + t,          W = t,           H = rect.H - t * 2 };
        var right  = new SDL.FRect { X = rect.X + rect.W - t, Y = rect.Y + t,          W = t,           H = rect.H - t * 2 };
        SDL.RenderFillRect(_renderer, ref top);
        SDL.RenderFillRect(_renderer, ref bottom);
        SDL.RenderFillRect(_renderer, ref left);
        SDL.RenderFillRect(_renderer, ref right);
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
            SDL.RenderFillRect(_renderer, ref row);
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

    // ---- Private helpers ----

    private void RenderSurface(IntPtr surface, SDL.FRect? srcFRect, SDL.FRect dstFRect, float alpha)
    {
        IntPtr texture = SDL.CreateTextureFromSurface(_renderer, surface);
        if (texture == IntPtr.Zero) return;
        try
        {
            SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
            if (alpha < 1f)
                SDL.SetTextureAlphaMod(texture, (byte)Math.Clamp(alpha * 255f, 0f, 255f));

            if (srcFRect.HasValue)
            {
                var srcF = srcFRect.Value;
                SDL.RenderTexture(_renderer, texture, ref srcF, ref dstFRect);
            }
            else
            {
                SDL.RenderTexture(_renderer, texture, IntPtr.Zero, ref dstFRect);
            }
        }
        finally { SDL.DestroyTexture(texture); }
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
                SDL.RenderTexture(_renderer, texture, IntPtr.Zero, ref dstF);
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

using SDL3;
using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

/// <summary>
/// Geometry unit tests for the cover-scale UV computation used in sidebar rendering.
/// All tests are pure arithmetic — no D3D11 device, no SDL, no file I/O.
/// </summary>
[TestFixture]
internal class SidebarRenderingTests
{
    // Helper: convert UV output back to image-space pixel coordinates.
    // srcX = u0 * imgW,  srcW = (u1 - u0) * imgW
    // srcY = v0 * imgH,  srcH = (v1 - v0) * imgH
    private static (float srcX, float srcY, float srcW, float srcH) UvToPixels(
        float u0, float v0, float u1, float v1, int imgW, int imgH)
        => ((u0 * imgW), (v0 * imgH), ((u1 - u0) * imgW), ((v1 - v0) * imgH));

    [Test]
    public void ComputeCoverUV_IdenticalSizes_UsesEntireImage()
    {
        var (u0, v0, u1, v1) = D3D11Renderer.ComputeCoverUV((100, 200), 100f, 200f);
        var (srcX, srcY, srcW, srcH) = UvToPixels(u0, v0, u1, v1, 100, 200);

        Assert.That(srcX, Is.EqualTo(0f).Within(0.01f));
        Assert.That(srcY, Is.EqualTo(0f).Within(0.01f));
        Assert.That(srcW, Is.EqualTo(100f).Within(0.01f));
        Assert.That(srcH, Is.EqualTo(200f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_WideImageNarrowQuad_CropsSidesNotTopBottom()
    {
        // 200×100 image → 100×100 quad: scale=1 (height), srcW=100, srcH=100, srcX=50
        var (u0, v0, u1, v1) = D3D11Renderer.ComputeCoverUV((200, 100), 100f, 100f);
        var (srcX, srcY, srcW, srcH) = UvToPixels(u0, v0, u1, v1, 200, 100);

        Assert.That(srcY,  Is.EqualTo(0f).Within(0.01f));
        Assert.That(srcH,  Is.EqualTo(100f).Within(0.01f));
        Assert.That(srcX,  Is.EqualTo(50f).Within(0.01f));
        Assert.That(srcW,  Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_TallImageWideQuad_CropsTopBottomNotSides()
    {
        // 100×200 image → 100×100 quad: scale=1 (width), srcW=100, srcH=100, srcY=50
        var (u0, v0, u1, v1) = D3D11Renderer.ComputeCoverUV((100, 200), 100f, 100f);
        var (srcX, srcY, srcW, srcH) = UvToPixels(u0, v0, u1, v1, 100, 200);

        Assert.That(srcX,  Is.EqualTo(0f).Within(0.01f));
        Assert.That(srcW,  Is.EqualTo(100f).Within(0.01f));
        Assert.That(srcY,  Is.EqualTo(50f).Within(0.01f));
        Assert.That(srcH,  Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_SmallImageLargeQuad_NoClipping()
    {
        // 50×100 image → 100×200 quad: same aspect ratio, scale=2, no cropping
        var (u0, v0, u1, v1) = D3D11Renderer.ComputeCoverUV((50, 100), 100f, 200f);
        var (srcX, srcY, srcW, srcH) = UvToPixels(u0, v0, u1, v1, 50, 100);

        Assert.That(srcX, Is.EqualTo(0f).Within(0.01f));
        Assert.That(srcY, Is.EqualTo(0f).Within(0.01f));
        Assert.That(srcW, Is.EqualTo(50f).Within(0.01f));
        Assert.That(srcH, Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_SrcIsCenteredHorizontallyOnImage()
    {
        // 300×100 image → 100×100 quad
        var (u0, _, u1, _) = D3D11Renderer.ComputeCoverUV((300, 100), 100f, 100f);
        float centerU = (u0 + u1) / 2f;
        Assert.That(centerU, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_SrcIsCenteredVerticallyOnImage()
    {
        // 100×300 image → 100×100 quad
        var (_, v0, _, v1) = D3D11Renderer.ComputeCoverUV((100, 300), 100f, 100f);
        float centerV = (v0 + v1) / 2f;
        Assert.That(centerV, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_SrcWidthFillsQuad_AfterScaling()
    {
        (int W, int H) imageSize = (100, 150);
        float quadW = 48f, quadH = 480f;
        var (u0, _, u1, _) = D3D11Renderer.ComputeCoverUV(imageSize, quadW, quadH);
        float srcW = (u1 - u0) * imageSize.W;
        float scale = Math.Max(quadW / imageSize.W, quadH / imageSize.H);

        Assert.That(srcW * scale, Is.EqualTo(quadW).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_SrcHeightFillsQuad_AfterScaling()
    {
        (int W, int H) imageSize = (100, 150);
        float quadW = 48f, quadH = 480f;
        var (_, v0, _, v1) = D3D11Renderer.ComputeCoverUV(imageSize, quadW, quadH);
        float srcH = (v1 - v0) * imageSize.H;
        float scale = Math.Max(quadW / imageSize.W, quadH / imageSize.H);

        Assert.That(srcH * scale, Is.EqualTo(quadH).Within(0.01f));
    }

    [Test]
    public void ComputeCoverUV_UVsAreWithinUnitRange()
    {
        var (u0, v0, u1, v1) = D3D11Renderer.ComputeCoverUV((80, 200), 48f, 480f);

        Assert.That(u0, Is.GreaterThanOrEqualTo(0f));
        Assert.That(v0, Is.GreaterThanOrEqualTo(0f));
        Assert.That(u1, Is.LessThanOrEqualTo(1f + 0.001f));
        Assert.That(v1, Is.LessThanOrEqualTo(1f + 0.001f));
    }

    // ---- ComputeCoverSrcFRect (SDL_GPU path) ----
    // Same cover-crop math as ComputeCoverUV above, expressed as a pixel-space source rect
    // (what SDL.RenderTexture expects) rather than normalized UVs (what D3D11 sampling expects).

    [Test]
    public void ComputeCoverSrcFRect_IdenticalSizes_UsesEntireImage()
    {
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((100, 200), 100f, 200f);

        Assert.That(src.X, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Y, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.W, Is.EqualTo(100f).Within(0.01f));
        Assert.That(src.H, Is.EqualTo(200f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_WideImageNarrowQuad_CropsSidesNotTopBottom()
    {
        // 200×100 image → 100×100 quad: scale=1 (height), srcW=100, srcH=100, srcX=50
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((200, 100), 100f, 100f);

        Assert.That(src.Y, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.H, Is.EqualTo(100f).Within(0.01f));
        Assert.That(src.X, Is.EqualTo(50f).Within(0.01f));
        Assert.That(src.W, Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_TallImageWideQuad_CropsTopBottomNotSides()
    {
        // 100×200 image → 100×100 quad: scale=1 (width), srcW=100, srcH=100, srcY=50
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((100, 200), 100f, 100f);

        Assert.That(src.X, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.W, Is.EqualTo(100f).Within(0.01f));
        Assert.That(src.Y, Is.EqualTo(50f).Within(0.01f));
        Assert.That(src.H, Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_SmallImageLargeQuad_NoClipping()
    {
        // 50×100 image → 100×200 quad: same aspect ratio, scale=2, no cropping
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((50, 100), 100f, 200f);

        Assert.That(src.X, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.Y, Is.EqualTo(0f).Within(0.01f));
        Assert.That(src.W, Is.EqualTo(50f).Within(0.01f));
        Assert.That(src.H, Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_SrcIsCenteredHorizontallyOnImage()
    {
        // 300×100 image → 100×100 quad
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((300, 100), 100f, 100f);
        float centerX = src.X + src.W / 2f;
        Assert.That(centerX, Is.EqualTo(150f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_SrcIsCenteredVerticallyOnImage()
    {
        // 100×300 image → 100×100 quad
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((100, 300), 100f, 100f);
        float centerY = src.Y + src.H / 2f;
        Assert.That(centerY, Is.EqualTo(150f).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_SrcWidthFillsQuad_AfterScaling()
    {
        (int W, int H) imageSize = (100, 150);
        float quadW = 48f, quadH = 480f;
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect(imageSize, quadW, quadH);
        float scale = Math.Max(quadW / imageSize.W, quadH / imageSize.H);

        Assert.That(src.W * scale, Is.EqualTo(quadW).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_SrcHeightFillsQuad_AfterScaling()
    {
        (int W, int H) imageSize = (100, 150);
        float quadW = 48f, quadH = 480f;
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect(imageSize, quadW, quadH);
        float scale = Math.Max(quadW / imageSize.W, quadH / imageSize.H);

        Assert.That(src.H * scale, Is.EqualTo(quadH).Within(0.01f));
    }

    [Test]
    public void ComputeCoverSrcFRect_SrcRectIsWithinImageBounds()
    {
        SDL.FRect src = SDL3HwRenderer.ComputeCoverSrcFRect((80, 200), 48f, 480f);

        Assert.That(src.X, Is.GreaterThanOrEqualTo(0f));
        Assert.That(src.Y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(src.X + src.W, Is.LessThanOrEqualTo(80f + 0.01f));
        Assert.That(src.Y + src.H, Is.LessThanOrEqualTo(200f + 0.01f));
    }
}

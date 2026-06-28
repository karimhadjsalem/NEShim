namespace NEShim.Rendering.MotionEffects;

internal sealed class ScanlineBobMotionEffect : IMotionEffect
{
    private const int NesNativeHeight = 240;

    // Updated by NotifyLayout; starts at 0 (no bob) until the renderer provides dimensions.
    private float _bobAmplitude;

    public VideoMotionEffectMode EffectMode => VideoMotionEffectMode.ScanlineBob;

    public void NotifyLayout(int viewportHeight, int letterboxHeight)
    {
        if (viewportHeight <= 0 || letterboxHeight <= 0)
            return;
        // Half a NES scanline expressed in D3D clip-space units:
        //   half-scanline px = letterboxHeight / (2 * NesNativeHeight)
        //   clip units       = half-scanline px / (viewportHeight / 2)
        //                    = letterboxHeight / (NesNativeHeight * viewportHeight)
        _bobAmplitude = (float)letterboxHeight / (NesNativeHeight * (float)viewportHeight);
    }

    public (float Dx, float Dy) GetFrameOffset(long frameCount)
    {
        float dy = (frameCount % 2 == 0) ? _bobAmplitude : -_bobAmplitude;
        return (0f, dy);
    }
}

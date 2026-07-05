using NEShim.Emulation;
using NEShim.Rendering;

namespace NEShim.GameLoop;

/// <summary>
/// Owns the volatile renderer reference and all render-submission work:
/// frame buffer swap, FPS overlay update, and async upload+present via a UI-thread marshal delegate.
/// </summary>
internal sealed class RenderCoordinator : IRenderCoordinator
{
    // Written on the UI thread only while the emulation thread is blocked on DeviceLost;
    // ManualResetEventSlim.Set() provides the memory barrier that makes the write visible on resume.
    private volatile IFrameRenderer _renderer;
    private readonly FrameBuffer _frameBuffer;
    // Queues an action on the UI thread (wraps Control.BeginInvoke in production).
    private readonly Action<Action> _marshal;

    public RenderCoordinator(FrameBuffer frameBuffer, IFrameRenderer renderer, Action<Action> marshal)
    {
        _frameBuffer = frameBuffer;
        _renderer = renderer;
        _marshal = marshal;
    }

    /// <summary>
    /// Replaces the renderer after device-loss recovery.
    /// Must only be called on the UI thread while the emulation thread is blocked on
    /// <see cref="EmulationThread.PauseReasons.DeviceLost"/>.
    /// </summary>
    internal void UpdateRenderer(IFrameRenderer renderer) => _renderer = renderer;

    /// <summary>
    /// Writes the frame into the back buffer, swaps buffers, updates the FPS overlay,
    /// then queues upload+present on the UI thread.
    /// </summary>
    public void SubmitFrame(FrameData frame, bool showFps, float currentFps, Action? afterPresented)
    {
        _frameBuffer.WriteBack(frame.VideoBuffer, frame.BufferWidth, frame.BufferHeight);
        _frameBuffer.Swap();

        _renderer.UpdateFpsOverlay(showFps, currentFps);
        int fw = _frameBuffer.Width;
        int fh = _frameBuffer.Height;
        _marshal(() =>
        {
            IFrameRenderer r = _renderer;
            r.UploadFrame(_frameBuffer.FrontBuffer, fw, fh);
            r.Tick(vsync: true);
            afterPresented?.Invoke();
        });
    }

    /// <summary>Marshals a toast notification to the UI thread.</summary>
    public void ShowToast(string text) =>
        _marshal(() => _renderer.ShowToast(text));
}

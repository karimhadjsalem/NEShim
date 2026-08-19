using System.Threading;
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
    // 0 = no render action currently queued/running on the UI thread; 1 = one is. Same volatile-
    // int-plus-CAS pattern EmulationThread uses for _pauseReasonBits.
    private int _renderPending;

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
    /// then queues upload+present on the UI thread — unless one is already queued.
    /// </summary>
    public void SubmitFrame(FrameData frame, bool showFps, float currentFps, Action? afterPresented)
    {
        _frameBuffer.WriteBack(frame.VideoBuffer, frame.BufferWidth, frame.BufferHeight);
        _frameBuffer.Swap();

        _renderer.UpdateFpsOverlay(showFps, currentFps);

        // The emulation thread paces itself independently of how fast the UI thread can actually
        // present a frame — with an expensive video filter active, a real GPU frame can take
        // longer than the emulation thread's own ~16.67ms budget. Since the marshalled action
        // below reads _frameBuffer.FrontBuffer live (not a snapshot captured here), a second
        // pending action would just re-present the same or a barely-newer frame for no visual
        // benefit, at the cost of a full real UploadFrame+Tick+Present cycle — and every one of
        // those queued redundant cycles makes the backlog (and visible lag) worse, compounding
        // over time instead of settling at a fixed offset. Skipping the enqueue when one is
        // already pending caps the marshal queue at close to one entry, so display lag stays
        // bounded to roughly the current frame's own cost instead of growing unbounded.
        if (Interlocked.CompareExchange(ref _renderPending, 1, 0) != 0)
            return;

        int fw = _frameBuffer.Width;
        int fh = _frameBuffer.Height;
        _marshal(() =>
        {
            // Reset before doing the real work (not after) so a frame produced while this action
            // is still running is free to queue its own follow-up action immediately, rather than
            // waiting for this one to fully finish first.
            Interlocked.Exchange(ref _renderPending, 0);
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

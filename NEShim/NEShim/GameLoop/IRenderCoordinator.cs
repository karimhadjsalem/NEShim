using NEShim.Emulation;

namespace NEShim.GameLoop;

internal interface IRenderCoordinator
{
    void SubmitFrame(FrameData frame, bool showFps, float currentFps, Action? afterPresented);
    void ShowToast(string text);
}

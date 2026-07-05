using NEShim.Emulation;
using NEShim.GameLoop;
using NEShim.Rendering;
using NSubstitute;

namespace NEShim.Tests.GameLoop;

[TestFixture]
internal class RenderCoordinatorTests
{
    private IFrameRenderer _renderer = null!;
    private FrameBuffer    _buffer   = null!;

    // Discards the posted action — used when we only care about the synchronous parts of SubmitFrame.
    // UploadFrame takes ReadOnlySpan<int>, which Castle DynamicProxy cannot intercept, so we must
    // avoid letting the marshal lambda run when testing synchronous behaviour.
    private static readonly Action<Action> NoOpMarshal = _ => { };

    // Runs the posted action immediately — safe only when the action does not invoke
    // ReadOnlySpan<T> methods on a substituted interface.
    private static readonly Action<Action> SynchronousMarshal = action => action();

    [SetUp]
    public void SetUp()
    {
        _renderer = Substitute.For<IFrameRenderer>();
        _buffer   = new FrameBuffer();
    }

    [TearDown]
    public void TearDown()
    {
        _renderer.Dispose();
    }

    private static FrameData MakeFrame(int width = 256, int height = 240) =>
        new(new int[width * height], width, height, Array.Empty<short>(), 0);

    // ---- SubmitFrame — synchronous work (WriteBack, Swap, UpdateFpsOverlay) ----

    [Test]
    public void SubmitFrame_WritesFrameDimensionsToBuffer()
    {
        var coordinator = new RenderCoordinator(_buffer, _renderer, NoOpMarshal);

        coordinator.SubmitFrame(MakeFrame(128, 120), showFps: false, currentFps: 60f, afterPresented: null);

        Assert.That(_buffer.Width,  Is.EqualTo(128));
        Assert.That(_buffer.Height, Is.EqualTo(120));
    }

    [Test]
    public void SubmitFrame_UpdatesFpsOverlayBeforePosting()
    {
        var coordinator = new RenderCoordinator(_buffer, _renderer, NoOpMarshal);

        coordinator.SubmitFrame(MakeFrame(), showFps: true, currentFps: 59.9f, afterPresented: null);

        _renderer.Received(1).UpdateFpsOverlay(true, 59.9f);
    }

    // ---- ShowToast — marshals string call (no ReadOnlySpan involved) ----

    [Test]
    public void ShowToast_CallsRendererShowToast()
    {
        var coordinator = new RenderCoordinator(_buffer, _renderer, SynchronousMarshal);

        coordinator.ShowToast("hello");

        _renderer.Received(1).ShowToast("hello");
    }

    // ---- UpdateRenderer ----

    [Test]
    public void UpdateRenderer_SubsequentSubmitUsesNewRenderer()
    {
        var renderer2 = Substitute.For<IFrameRenderer>();
        var coordinator = new RenderCoordinator(_buffer, _renderer, NoOpMarshal);

        coordinator.UpdateRenderer(renderer2);
        coordinator.SubmitFrame(MakeFrame(), showFps: false, currentFps: 60f, afterPresented: null);

        renderer2.Received(1).UpdateFpsOverlay(false, 60f);
        _renderer.DidNotReceive().UpdateFpsOverlay(Arg.Any<bool>(), Arg.Any<float>());
        renderer2.Dispose();
    }
}

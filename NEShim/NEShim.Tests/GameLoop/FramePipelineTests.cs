using NEShim.Audio;
using NEShim.Config;
using NEShim.Emulation;
using NEShim.GameLoop;
using NEShim.Input;
using NEShim.Saves;
using NSubstitute;

namespace NEShim.Tests.GameLoop;

[TestFixture]
internal class FramePipelineTests
{
    private IEmulationCore      _core         = null!;
    private IAchievementProcessor _achievements = null!;
    private IRenderCoordinator  _render       = null!;
    private ISaveManager        _saves        = null!;
    private AppConfig           _config       = null!;

    // AudioPlayer is sealed with no interface; we test audio via FrameData inspection
    // rather than mock verification. Audio enqueue correctness is covered by AudioPlayer's
    // own unit tests.

    [TearDown]
    public void TearDown()
    {
        _core.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        _core         = Substitute.For<IEmulationCore>();
        _achievements = Substitute.For<IAchievementProcessor>();
        _render       = Substitute.For<IRenderCoordinator>();
        _saves        = Substitute.For<ISaveManager>();
        _config       = new AppConfig();

        _core.VsyncNumerator.Returns(60);
        _core.VsyncDenominator.Returns(1);
        _core.RunFrame(Arg.Any<InputSnapshot>()).Returns(
            new FrameData(new int[256 * 240], 256, 240, Array.Empty<short>(), 0));
    }

    private FramePipeline CreatePipeline()
    {
        var audio = new AudioPlayer(4, new NesFilterProcessor());
        return new FramePipeline(_core, audio, _achievements, _render, _saves);
    }

    // ---- RunFrame delegates ----

    [Test]
    public void RunFrame_CallsCoreRunFrame()
    {
        var snapshot  = InputSnapshot.Empty;
        var pipeline  = CreatePipeline();

        pipeline.RunFrame(snapshot, _config, afterPresented: null);

        _core.Received(1).RunFrame(snapshot);
    }

    [Test]
    public void RunFrame_TicksAchievements()
    {
        var pipeline = CreatePipeline();

        pipeline.RunFrame(InputSnapshot.Empty, _config, afterPresented: null);

        _achievements.Received(1).Tick();
    }

    [Test]
    public void RunFrame_SubmitsFrameToRenderer()
    {
        var pipeline = CreatePipeline();

        pipeline.RunFrame(InputSnapshot.Empty, _config, afterPresented: null);

        _render.Received(1).SubmitFrame(Arg.Any<FrameData>(), Arg.Any<bool>(), Arg.Any<float>(), Arg.Any<Action?>());
    }

    // ---- Auto-save ----

    [Test]
    public void RunFrame_DoesNotAutoSaveBeforeInterval()
    {
        var pipeline = CreatePipeline();
        int callsBeforeInterval = 18_000 - 1;

        for (int i = 0; i < callsBeforeInterval; i++)
            pipeline.RunFrame(InputSnapshot.Empty, _config, afterPresented: null);

        _saves.DidNotReceive().AutoSave();
    }

    [Test]
    public void RunFrame_AutoSavesAtExactInterval()
    {
        var pipeline = CreatePipeline();

        for (int i = 0; i < 18_000; i++)
            pipeline.RunFrame(InputSnapshot.Empty, _config, afterPresented: null);

        _saves.Received(1).AutoSave();
    }

    [Test]
    public void RunFrame_AutoSaveRepeatsAtEachInterval()
    {
        var pipeline = CreatePipeline();

        for (int i = 0; i < 18_000 * 2; i++)
            pipeline.RunFrame(InputSnapshot.Empty, _config, afterPresented: null);

        _saves.Received(2).AutoSave();
    }

    // ---- FPS tracking ----

    [Test]
    public void CurrentFps_StartsAtZero()
    {
        var pipeline = CreatePipeline();
        Assert.That(pipeline.CurrentFps, Is.EqualTo(0f));
    }
}

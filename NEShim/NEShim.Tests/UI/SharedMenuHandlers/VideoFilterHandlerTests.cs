using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class VideoFilterHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private VideoFilterHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new VideoFilterHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedVideoFilterTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.VideoFilterTitle));

    [Test]
    public void ItemCount_IsSupportedFilterCountPlusBack() =>
        Assert.That(_handler.ItemCount, Is.EqualTo(VideoFilterModeParser.D3D11Supported.Length + 1));

    [Test]
    public void GetItems_CurrentFilter_IsChecked()
    {
        _config.VideoFilter = VideoFilterMode.CrtScanlines.ToString();
        int idx = Array.IndexOf(VideoFilterModeParser.D3D11Supported, VideoFilterMode.CrtScanlines);

        Assert.That(_handler.GetItems()[idx], Does.StartWith("✓"));
    }

    [Test]
    public void Activate_SelectsFilter_ClearsPresetAndFiresCallback()
    {
        var mode = VideoFilterModeParser.D3D11Supported[0];

        _handler.Activate(0);

        Assert.That(_config.VideoFilter, Is.EqualTo(mode.ToString()));
        _host.Received(1).ClearPreset();
        _host.Received(1).OnVideoFilterChanged(mode);
        _host.Received(1).NavigateTo(Screen.Video);
    }

    [Test]
    public void Activate_SelectingFilterThatMatchesOverlay_ClearsOverlay()
    {
        // Must be one of the 3 overlay-eligible filters (OverlaySupported), not just any
        // D3D11Supported entry — ParseOverlay returns null for e.g. PixelPerfect, so the
        // "clears overlay" branch would never trigger for that mode.
        var mode = VideoFilterModeParser.OverlaySupported[0];
        _config.VideoFilterOverlay = mode.ToString();
        int idx = Array.IndexOf(VideoFilterModeParser.D3D11Supported, mode);

        _handler.Activate(idx);

        Assert.That(_config.VideoFilterOverlay, Is.EqualTo("None"));
        _host.Received(1).OnVideoFilterOverlayChanged(null);
    }

    [Test]
    public void Activate_SelectingFilterNotMatchingOverlay_LeavesOverlayUntouched()
    {
        var filterMode = VideoFilterMode.PixelPerfect;
        int filterIdx = Array.IndexOf(VideoFilterModeParser.D3D11Supported, filterMode);
        _config.VideoFilterOverlay = VideoFilterMode.CrtScanlines.ToString();

        _handler.Activate(filterIdx);

        Assert.That(_config.VideoFilterOverlay, Is.EqualTo(VideoFilterMode.CrtScanlines.ToString()));
        _host.DidNotReceive().OnVideoFilterOverlayChanged(Arg.Any<VideoFilterMode?>());
    }

    [Test]
    public void Activate_BackRow_NavigatesWithoutChangingFilter()
    {
        string original = _config.VideoFilter;
        int backIndex = _handler.ItemCount - 1;

        _handler.Activate(backIndex);

        Assert.That(_config.VideoFilter, Is.EqualTo(original));
        _host.Received(1).NavigateTo(Screen.Video);
        _host.DidNotReceive().OnVideoFilterChanged(Arg.Any<VideoFilterMode>());
    }
}

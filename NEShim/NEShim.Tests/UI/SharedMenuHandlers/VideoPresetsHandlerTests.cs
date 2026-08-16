using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using NEShim.UI;
using NSubstitute;

namespace NEShim.Tests.UI.SharedMenuHandlers;

[TestFixture]
internal class VideoPresetsHandlerTests
{
    private IMenuHost _host = null!;
    private AppConfig _config = null!;
    private LocalizationData _localization = null!;
    private VideoPresetsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _config = new AppConfig();
        _localization = new LocalizationData();
        _host = Substitute.For<IMenuHost>();
        _host.Config.Returns(_config);
        _host.Localization.Returns(_localization);
        _handler = new VideoPresetsHandler(_host);
    }

    [Test]
    public void Title_ReturnsLocalizedVideoPresetsTitle() =>
        Assert.That(_handler.Title, Is.EqualTo(_localization.VideoPresetsTitle));

    [Test]
    public void ItemCount_IsPresetCountPlusNoPresetPlusBack() =>
        Assert.That(_handler.ItemCount, Is.EqualTo(VideoPresetRegistry.All.Length + 2));

    [Test]
    public void GetItems_NoPresetActive_ChecksmarksFirstRow()
    {
        _config.VideoPreset = "None";
        Assert.That(_handler.GetItems()[0], Does.StartWith("✓"));
    }

    [Test]
    public void GetItems_NamedPresetActive_ChecksmarksItsRowNotNoPreset()
    {
        var preset = VideoPresetRegistry.All[0];
        _config.VideoPreset = preset.Name;

        var items = _handler.GetItems();

        Assert.That(items[0], Does.Not.StartWith("✓"));
        Assert.That(items[1], Does.StartWith("✓"));
    }

    [Test]
    public void Activate_NoPresetRow_SetsConfigToNoneAndSaves()
    {
        _handler.Activate(0);

        Assert.That(_config.VideoPreset, Is.EqualTo("None"));
        _host.Received(1).OnConfigSaved();
        _host.Received(1).NavigateTo(Screen.Video);
    }

    [Test]
    public void Activate_NamedPresetRow_AppliesThatPreset()
    {
        var preset = VideoPresetRegistry.All[0];

        _handler.Activate(1);

        _host.Received(1).ApplyPreset(preset);
        _host.Received(1).NavigateTo(Screen.Video);
    }

    [Test]
    public void Activate_BackRow_NavigatesWithoutApplyingAnything()
    {
        int backIndex = _handler.ItemCount - 1;

        _handler.Activate(backIndex);

        _host.DidNotReceive().ApplyPreset(Arg.Any<VideoPreset>());
        _host.DidNotReceive().OnConfigSaved();
        _host.Received(1).NavigateTo(Screen.Video);
    }
}

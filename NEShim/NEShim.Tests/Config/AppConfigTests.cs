using NEShim.Config;

namespace NEShim.Tests.Config;

[TestFixture]
internal class AppConfigTests
{
    [Test]
    public void DefaultRomPath_IsGameNes()
    {
        var config = new AppConfig();
        Assert.That(config.RomPath, Is.EqualTo("game.nes"));
    }

    [Test]
    public void DefaultWindowMode_IsFullscreen()
    {
        var config = new AppConfig();
        Assert.That(config.WindowMode, Is.EqualTo("Fullscreen"));
    }

    [Test]
    public void DefaultActiveSlot_IsZero()
    {
        var config = new AppConfig();
        Assert.That(config.ActiveSlot, Is.EqualTo(0));
    }

    [Test]
    public void DefaultAudioBufferFrames_IsThree()
    {
        var config = new AppConfig();
        Assert.That(config.AudioBufferFrames, Is.EqualTo(3));
    }

    [Test]
    public void DefaultGamepadDeadzone_IsEightThousand()
    {
        var config = new AppConfig();
        Assert.That(config.GamepadDeadzone, Is.EqualTo(8000));
    }

    [Test]
    public void DefaultMainMenuMusicPath_IsEmpty()
    {
        var config = new AppConfig();
        Assert.That(config.MainMenuMusicPath, Is.Empty);
    }

    [Test]
    public void DefaultInputMappings_ContainsAllEightNesButtons()
    {
        var config = new AppConfig();
        string[] expected =
        {
            "P1 Up", "P1 Down", "P1 Left", "P1 Right",
            "P1 A",  "P1 B",   "P1 Start", "P1 Select",
        };
        Assert.That(config.InputMappings.Keys, Is.SupersetOf(expected));
    }

    [Test]
    public void DefaultInputMappings_P1Up_MapsToW()
    {
        var config = new AppConfig();
        Assert.That(config.InputMappings["P1 Up"].Key, Is.EqualTo("W"));
    }

    [Test]
    public void DefaultHotkeyMappings_DoesNotContainOpenMenu()
    {
        var config = new AppConfig();
        Assert.That(config.HotkeyMappings.ContainsKey("OpenMenu"), Is.False);
    }

    [Test]
    public void ShowFps_DefaultIsFalse()
    {
        var config = new AppConfig();
        Assert.That(config.ShowFps, Is.False);
    }

    [Test]
    public void InputBinding_StoresKeyAndGamepadButton()
    {
        var binding = new InputBinding("OemPeriod", "A");
        Assert.That(binding.Key,           Is.EqualTo("OemPeriod"));
        Assert.That(binding.GamepadButton, Is.EqualTo("A"));
    }

    [Test]
    public void InputBinding_DefaultConstructor_HasNullValues()
    {
        var binding = new InputBinding();
        Assert.That(binding.Key,           Is.Null);
        Assert.That(binding.GamepadButton, Is.Null);
    }

    // ---- Hidden / developer-only defaults ----

    [Test]
    public void EnableLogging_DefaultIsFalse()
    {
        var config = new AppConfig();
        Assert.That(config.EnableLogging, Is.False);
    }

    [Test]
    public void Region_DefaultIsAuto()
    {
        var config = new AppConfig();
        Assert.That(config.Region, Is.EqualTo("Auto"));
    }

    [Test]
    public void AnalogStickMode_DefaultIsCardinal()
    {
        var config = new AppConfig();
        Assert.That(config.AnalogStickMode, Is.EqualTo("Cardinal"));
    }

    [Test]
    public void AchievementPublicKey_DefaultIsEmpty()
    {
        var config = new AppConfig();
        Assert.That(config.AchievementPublicKey, Is.Empty);
    }
}

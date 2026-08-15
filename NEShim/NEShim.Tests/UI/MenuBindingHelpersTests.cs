using NEShim.Audio;
using NEShim.Config;
using NEShim.Localization;
using NEShim.Rendering;
using NEShim.UI;

namespace NEShim.Tests.UI;

[TestFixture]
internal class MenuBindingHelpersTests
{
    private AppConfig _config = null!;

    [SetUp]
    public void SetUp() => _config = new AppConfig();

    // ── SetGamepadBinding: cross-action duplicate prevention ────────────────────

    [Test]
    public void SetGamepadBinding_ClearsSameButtonFromOtherActions_GamepadButton()
    {
        // DPadUp is the default GamepadButton for P1 Up; rebinding P1 Down to DPadUp
        // must clear it from P1 Up.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "DPadUp");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.Null);
        Assert.That(_config.InputMappings["P1 Down"].GamepadButton, Is.EqualTo("DPadUp"));
    }

    [Test]
    public void SetGamepadBinding_ClearsSameButtonFromOtherActions_GamepadButton2()
    {
        // AnalogUp is the default GamepadButton2 for P1 Up; rebinding P1 Down to AnalogUp
        // must clear it from P1 Up.GamepadButton2 so P1 Up stops responding to AnalogUp.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "AnalogUp");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton2, Is.Null,
            "AnalogUp must be cleared from P1 Up.GamepadButton2 to prevent double-binding");
    }

    // ── SetGamepadBinding: same-action cross-axis prevention ────────────────────

    [Test]
    public void SetGamepadBinding_AnalogIdentifier_ClearsGamepadButton2OnSameAction()
    {
        // Default: P1 Down.GamepadButton2 = "AnalogDown".
        // If user rebinds P1 Down.GamepadButton to "AnalogUp", GamepadButton2 must be cleared
        // so P1 Down doesn't fire for both AnalogUp (primary) and AnalogDown (secondary).
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "AnalogUp");

        Assert.That(_config.InputMappings["P1 Down"].GamepadButton,  Is.EqualTo("AnalogUp"));
        Assert.That(_config.InputMappings["P1 Down"].GamepadButton2, Is.Null,
            "Secondary analog slot must be cleared when primary is set to an analog identifier");
    }

    [Test]
    public void SetGamepadBinding_AnalogDown_ClearsGamepadButton2OnSameAction()
    {
        // Same-axis rebind: P1 Down.GamepadButton2 = "AnalogDown" by default.
        // Explicitly binding GamepadButton to "AnalogDown" should also clear GamepadButton2
        // (becomes redundant; keeps state clean).
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "AnalogDown");

        Assert.That(_config.InputMappings["P1 Down"].GamepadButton2, Is.Null);
    }

    // ── SetGamepadBinding: non-analog primary preserves GamepadButton2 ──────────

    [Test]
    public void SetGamepadBinding_NonAnalogButton_PreservesGamepadButton2()
    {
        // Rebinding P1 Up's primary button to a face button should keep the default
        // AnalogUp secondary binding so the analog stick still triggers P1 Up.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Up", "A");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton,  Is.EqualTo("A"));
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton2, Is.EqualTo("AnalogUp"),
            "Analog secondary slot must survive a non-analog primary rebind");
    }

    [Test]
    public void SetGamepadBinding_DPadButton_PreservesGamepadButton2()
    {
        // Re-confirming DPadDown as the primary button for P1 Down should not erase
        // the AnalogDown secondary slot.
        MenuBindingHelpers.SetGamepadBinding(_config, "P1 Down", "DPadDown");

        Assert.That(_config.InputMappings["P1 Down"].GamepadButton2, Is.EqualTo("AnalogDown"));
    }

    // ── SetGamepadBinding: action not already present in InputMappings ─────────

    [Test]
    public void SetGamepadBinding_ActionNotInMappings_CreatesNewBinding()
    {
        // A hotkey-style action name with no default InputMappings entry — exercises the
        // "not found" branch (new InputBinding(null, buttonName)) rather than the
        // TryGetValue-succeeds update path every other SetGamepadBinding test uses.
        Assert.That(_config.InputMappings.ContainsKey("SomeNewAction"), Is.False);

        MenuBindingHelpers.SetGamepadBinding(_config, "SomeNewAction", "X");

        Assert.That(_config.InputMappings["SomeNewAction"].GamepadButton, Is.EqualTo("X"));
        Assert.That(_config.InputMappings["SomeNewAction"].Key, Is.Null);
    }

    [Test]
    public void SetGamepadBinding_ActionNotInMappings_StillClearsButtonFromOtherActions()
    {
        // DPadUp is P1 Up's default GamepadButton — binding it to a brand-new action must
        // still clear it from P1 Up, exercising both branches (dedup loop + "not found" create)
        // in the same call.
        MenuBindingHelpers.SetGamepadBinding(_config, "SomeNewAction", "DPadUp");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton, Is.Null);
        Assert.That(_config.InputMappings["SomeNewAction"].GamepadButton, Is.EqualTo("DPadUp"));
    }

    // ── SetBinding: keyboard duplicate prevention ───────────────────────────────

    [Test]
    public void SetBinding_ClearsSameKeyFromOtherActions()
    {
        // "W" is default Key for P1 Up; rebinding P1 Down to "W" must clear it from P1 Up.
        MenuBindingHelpers.SetBinding(_config, "P1 Down", "W");

        Assert.That(_config.InputMappings["P1 Up"].Key,   Is.Null);
        Assert.That(_config.InputMappings["P1 Down"].Key, Is.EqualTo("W"));
    }

    [Test]
    public void SetBinding_DoesNotClearGamepadSlots()
    {
        // Keyboard rebind must leave GamepadButton and GamepadButton2 intact.
        MenuBindingHelpers.SetBinding(_config, "P1 Up", "Up");

        Assert.That(_config.InputMappings["P1 Up"].GamepadButton,  Is.EqualTo("DPadUp"));
        Assert.That(_config.InputMappings["P1 Up"].GamepadButton2, Is.EqualTo("AnalogUp"));
    }

    // ── SetBinding: action not already present in InputMappings ────────────────

    [Test]
    public void SetBinding_ActionNotInMappings_CreatesNewBinding()
    {
        // Exercises the "not found" branch (new InputBinding(keyName, null)) rather than the
        // TryGetValue-succeeds update path every other SetBinding test uses.
        Assert.That(_config.InputMappings.ContainsKey("SomeNewAction"), Is.False);

        MenuBindingHelpers.SetBinding(_config, "SomeNewAction", "F1");

        Assert.That(_config.InputMappings["SomeNewAction"].Key, Is.EqualTo("F1"));
        Assert.That(_config.InputMappings["SomeNewAction"].GamepadButton, Is.Null);
    }

    [Test]
    public void SetBinding_ActionNotInMappings_StillClearsKeyFromOtherActions()
    {
        // "W" is P1 Up's default Key — binding it to a brand-new action must still clear it
        // from P1 Up, exercising both branches (dedup loop + "not found" create) in one call.
        MenuBindingHelpers.SetBinding(_config, "SomeNewAction", "W");

        Assert.That(_config.InputMappings["P1 Up"].Key, Is.Null);
        Assert.That(_config.InputMappings["SomeNewAction"].Key, Is.EqualTo("W"));
    }

    // ── LocalizeGamepadButton ────────────────────────────────────────────────────

    [Test]
    public void LocalizeGamepadButton_Null_ReturnsBindNone()
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.LocalizeGamepadButton(null, loc), Is.EqualTo(loc.BindNone));
    }

    [TestCase("A", "A")]
    [TestCase("B", "B")]
    [TestCase("X", "X")]
    [TestCase("Y", "Y")]
    public void LocalizeGamepadButton_FaceButtons_ReturnBareLetter(string identifier, string expected)
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.LocalizeGamepadButton(identifier, loc), Is.EqualTo(expected));
    }

    [Test]
    public void LocalizeGamepadButton_Start_ReturnsBindStart()
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.LocalizeGamepadButton("Start", loc), Is.EqualTo(loc.BindStart));
    }

    [Test]
    public void LocalizeGamepadButton_Back_ReturnsBindSelect()
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.LocalizeGamepadButton("Back", loc), Is.EqualTo(loc.BindSelect));
    }

    [TestCase("LeftShoulder",  nameof(LocalizationData.GamepadButtonLeftShoulder))]
    [TestCase("RightShoulder", nameof(LocalizationData.GamepadButtonRightShoulder))]
    [TestCase("LeftThumb",     nameof(LocalizationData.GamepadButtonLeftThumb))]
    [TestCase("RightThumb",    nameof(LocalizationData.GamepadButtonRightThumb))]
    [TestCase("DPadUp",        nameof(LocalizationData.GamepadDpadUp))]
    [TestCase("DPadDown",      nameof(LocalizationData.GamepadDpadDown))]
    [TestCase("DPadLeft",      nameof(LocalizationData.GamepadDpadLeft))]
    [TestCase("DPadRight",     nameof(LocalizationData.GamepadDpadRight))]
    [TestCase("AnalogUp",      nameof(LocalizationData.GamepadAnalogUp))]
    [TestCase("AnalogDown",    nameof(LocalizationData.GamepadAnalogDown))]
    [TestCase("AnalogLeft",    nameof(LocalizationData.GamepadAnalogLeft))]
    [TestCase("AnalogRight",   nameof(LocalizationData.GamepadAnalogRight))]
    public void LocalizeGamepadButton_KnownIdentifier_ReturnsMatchingLocalizationProperty(string identifier, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;

        Assert.That(MenuBindingHelpers.LocalizeGamepadButton(identifier, loc), Is.EqualTo(expected));
    }

    [Test]
    public void LocalizeGamepadButton_UnrecognizedIdentifier_PassesThroughUnchanged()
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.LocalizeGamepadButton("SomeFutureButton", loc), Is.EqualTo("SomeFutureButton"));
    }

    // ── Localized display names ─────────────────────────────────────────────────
    // These are the single shared source of truth consulted by both InGameMenuHandlers/ and
    // MainMenuHandlers/ — the exhaustive TestCase coverage here is what previously had to be
    // duplicated (or, in practice, wasn't) across every handler pair.

    [TestCase(VideoFilterMode.Bilinear,      nameof(LocalizationData.VideoFilterSmooth))]
    [TestCase(VideoFilterMode.PixelPerfect,  nameof(LocalizationData.VideoFilterPixelPerfect))]
    [TestCase(VideoFilterMode.CrtScanlines,  nameof(LocalizationData.VideoFilterCrtScanlines))]
    [TestCase(VideoFilterMode.CrtPhosphor,   nameof(LocalizationData.VideoFilterCrtPhosphor))]
    [TestCase(VideoFilterMode.NtscComposite, nameof(LocalizationData.VideoFilterNtscComposite))]
    [TestCase(VideoFilterMode.CrtScreen,     nameof(LocalizationData.VideoFilterCrtScreen))]
    [TestCase(VideoFilterMode.Xbr,           nameof(LocalizationData.VideoFilterXbr))]
    public void VideoFilterDisplayName_KnownMode_ReturnsMatchingLocalizationProperty(VideoFilterMode mode, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.VideoFilterDisplayName(mode, loc), Is.EqualTo(expected));
    }

    [Test]
    public void VideoFilterDisplayName_UnmappedMode_FallsBackToModeName()
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.VideoFilterDisplayName(VideoFilterMode.NearestNeighbour, loc),
            Is.EqualTo(VideoFilterMode.NearestNeighbour.ToString()));
    }

    [Test]
    public void VideoOverlayDisplayName_Null_ReturnsNoneLabel()
    {
        var loc = new LocalizationData();
        Assert.That(MenuBindingHelpers.VideoOverlayDisplayName(null, loc), Is.EqualTo(loc.VideoColorFilterNone));
    }

    [TestCase(VideoFilterMode.CrtScanlines, nameof(LocalizationData.VideoFilterCrtScanlines))]
    [TestCase(VideoFilterMode.CrtPhosphor,  nameof(LocalizationData.VideoFilterCrtPhosphor))]
    [TestCase(VideoFilterMode.CrtScreen,    nameof(LocalizationData.VideoFilterCrtScreen))]
    public void VideoOverlayDisplayName_KnownMode_ReturnsMatchingLocalizationProperty(VideoFilterMode mode, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.VideoOverlayDisplayName(mode, loc), Is.EqualTo(expected));
    }

    [TestCase(VideoMotionEffectMode.None,                nameof(LocalizationData.VideoMotionEffectNone))]
    [TestCase(VideoMotionEffectMode.CrtJitter,           nameof(LocalizationData.VideoMotionEffectCrtJitter))]
    [TestCase(VideoMotionEffectMode.ScanlineBob,         nameof(LocalizationData.VideoMotionEffectScanlineBob))]
    [TestCase(VideoMotionEffectMode.MagneticDistortion,  nameof(LocalizationData.VideoMotionEffectMagneticDistortion))]
    [TestCase(VideoMotionEffectMode.PhosphorPersistence, nameof(LocalizationData.VideoMotionEffectPhosphorPersistence))]
    public void VideoMotionEffectDisplayName_KnownMode_ReturnsMatchingLocalizationProperty(VideoMotionEffectMode mode, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.VideoMotionEffectDisplayName(mode, loc), Is.EqualTo(expected));
    }

    [TestCase(VideoColorFilterMode.None,               nameof(LocalizationData.VideoColorFilterNone))]
    [TestCase(VideoColorFilterMode.Warm,               nameof(LocalizationData.VideoColorFilterWarm))]
    [TestCase(VideoColorFilterMode.Greyscale,          nameof(LocalizationData.VideoColorFilterGreyscale))]
    [TestCase(VideoColorFilterMode.NesColorCorrection, nameof(LocalizationData.VideoColorFilterNesColors))]
    [TestCase(VideoColorFilterMode.Cool,               nameof(LocalizationData.VideoColorFilterCool))]
    [TestCase(VideoColorFilterMode.PhosphorAmber,      nameof(LocalizationData.VideoColorFilterPhosphorAmber))]
    [TestCase(VideoColorFilterMode.PhosphorGreen,      nameof(LocalizationData.VideoColorFilterPhosphorGreen))]
    public void VideoColorFilterDisplayName_KnownMode_ReturnsMatchingLocalizationProperty(VideoColorFilterMode mode, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.VideoColorFilterDisplayName(mode, loc), Is.EqualTo(expected));
    }

    [TestCase(OverscanMode.Overscan,  nameof(LocalizationData.OverscanOverscan))]
    [TestCase(OverscanMode.Normal,    nameof(LocalizationData.OverscanNormal))]
    [TestCase(OverscanMode.Underscan, nameof(LocalizationData.OverscanUnderscan))]
    public void OverscanDisplayName_KnownMode_ReturnsMatchingLocalizationProperty(OverscanMode mode, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.OverscanDisplayName(mode, loc), Is.EqualTo(expected));
    }

    [TestCase(AudioFilterMode.Default,       nameof(LocalizationData.AudioFilterDefault))]
    [TestCase(AudioFilterMode.Warm,          nameof(LocalizationData.AudioFilterWarm))]
    [TestCase(AudioFilterMode.PseudoStereo,  nameof(LocalizationData.AudioFilterPseudoStereo))]
    [TestCase(AudioFilterMode.WarmStereo,    nameof(LocalizationData.AudioFilterWarmStereo))]
    [TestCase(AudioFilterMode.Compression,   nameof(LocalizationData.AudioFilterCompression))]
    [TestCase(AudioFilterMode.BassBoost,     nameof(LocalizationData.AudioFilterBassBoost))]
    [TestCase(AudioFilterMode.Saturation,    nameof(LocalizationData.AudioFilterSaturation))]
    [TestCase(AudioFilterMode.DmcStabilizer, nameof(LocalizationData.AudioFilterDmcStabilizer))]
    public void AudioFilterDisplayName_KnownMode_ReturnsMatchingLocalizationProperty(AudioFilterMode mode, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.AudioFilterDisplayName(mode, loc), Is.EqualTo(expected));
    }

    [TestCase("NoFilters",  nameof(LocalizationData.VideoPresetNoFilters))]
    [TestCase("LivingRoom", nameof(LocalizationData.VideoPresetLivingRoom))]
    [TestCase("Arcade",     nameof(LocalizationData.VideoPresetArcade))]
    [TestCase("Sharp",      nameof(LocalizationData.VideoPresetSharp))]
    [TestCase("Phosphor",   nameof(LocalizationData.VideoPresetPhosphor))]
    [TestCase("None",       nameof(LocalizationData.VideoPresetNoPreset))]
    [TestCase("SomeFuturePreset", nameof(LocalizationData.VideoPresetNoPreset))]
    public void VideoPresetDisplayName_KnownAndUnknownNames_ReturnMatchingLocalizationProperty(string presetName, string propertyName)
    {
        var loc = new LocalizationData();
        var expected = (string)typeof(LocalizationData).GetProperty(propertyName)!.GetValue(loc)!;
        Assert.That(MenuBindingHelpers.VideoPresetDisplayName(presetName, loc), Is.EqualTo(expected));
    }
}

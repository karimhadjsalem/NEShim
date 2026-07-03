using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class VideoPresetRegistryTests
{
    // ---- All array ----

    [Test]
    public void All_ContainsFourPresets()
        => Assert.That(VideoPresetRegistry.All, Has.Length.EqualTo(4));

    [Test]
    public void All_ContainsLivingRoom()
        => Assert.That(VideoPresetRegistry.All, Contains.Item(VideoPresetRegistry.LivingRoom));

    [Test]
    public void All_ContainsArcade()
        => Assert.That(VideoPresetRegistry.All, Contains.Item(VideoPresetRegistry.Arcade));

    [Test]
    public void All_ContainsSharp()
        => Assert.That(VideoPresetRegistry.All, Contains.Item(VideoPresetRegistry.Sharp));

    [Test]
    public void All_ContainsPhosphor()
        => Assert.That(VideoPresetRegistry.All, Contains.Item(VideoPresetRegistry.Phosphor));

    [Test]
    public void All_NamesAreUnique()
    {
        var names = VideoPresetRegistry.All.Select(p => p.Name).ToList();
        Assert.That(names, Is.Unique);
    }

    // ---- LivingRoom ----

    [Test]
    public void LivingRoom_Name_IsLivingRoom()
        => Assert.That(VideoPresetRegistry.LivingRoom.Name, Is.EqualTo("LivingRoom"));

    [Test]
    public void LivingRoom_Filter_IsCrtScreen()
        => Assert.That(VideoPresetRegistry.LivingRoom.Filter, Is.EqualTo(VideoFilterMode.CrtScreen));

    [Test]
    public void LivingRoom_Overlay_IsCrtScanlines()
        => Assert.That(VideoPresetRegistry.LivingRoom.Overlay, Is.EqualTo(VideoFilterMode.CrtScanlines));

    [Test]
    public void LivingRoom_MotionEffect_IsCrtJitter()
        => Assert.That(VideoPresetRegistry.LivingRoom.MotionEffect, Is.EqualTo(VideoMotionEffectMode.CrtJitter));

    // ---- Arcade ----

    [Test]
    public void Arcade_Name_IsArcade()
        => Assert.That(VideoPresetRegistry.Arcade.Name, Is.EqualTo("Arcade"));

    [Test]
    public void Arcade_Filter_IsCrtPhosphor()
        => Assert.That(VideoPresetRegistry.Arcade.Filter, Is.EqualTo(VideoFilterMode.CrtPhosphor));

    [Test]
    public void Arcade_Overlay_IsNull()
        => Assert.That(VideoPresetRegistry.Arcade.Overlay, Is.Null);

    // ---- Sharp ----

    [Test]
    public void Sharp_Name_IsSharp()
        => Assert.That(VideoPresetRegistry.Sharp.Name, Is.EqualTo("Sharp"));

    [Test]
    public void Sharp_Filter_IsXbr()
        => Assert.That(VideoPresetRegistry.Sharp.Filter, Is.EqualTo(VideoFilterMode.Xbr));

    [Test]
    public void Sharp_MotionEffect_IsNone()
        => Assert.That(VideoPresetRegistry.Sharp.MotionEffect, Is.EqualTo(VideoMotionEffectMode.None));

    // ---- Phosphor ----

    [Test]
    public void Phosphor_Name_IsPhosphor()
        => Assert.That(VideoPresetRegistry.Phosphor.Name, Is.EqualTo("Phosphor"));

    [Test]
    public void Phosphor_MotionEffect_IsPhosphorPersistence()
        => Assert.That(VideoPresetRegistry.Phosphor.MotionEffect, Is.EqualTo(VideoMotionEffectMode.PhosphorPersistence));

    [Test]
    public void Phosphor_Overlay_IsCrtPhosphor()
        => Assert.That(VideoPresetRegistry.Phosphor.Overlay, Is.EqualTo(VideoFilterMode.CrtPhosphor));

    // ---- Record equality ----

    [Test]
    public void LivingRoom_EqualToEquivalentRecord()
    {
        var copy = new VideoPreset(
            VideoPresetRegistry.LivingRoom.Name,
            VideoPresetRegistry.LivingRoom.Filter,
            VideoPresetRegistry.LivingRoom.Overlay,
            VideoPresetRegistry.LivingRoom.ColorFilter,
            VideoPresetRegistry.LivingRoom.MotionEffect,
            VideoPresetRegistry.LivingRoom.Overscan,
            VideoPresetRegistry.LivingRoom.Brightness,
            VideoPresetRegistry.LivingRoom.Contrast,
            VideoPresetRegistry.LivingRoom.Saturation);
        Assert.That(copy, Is.EqualTo(VideoPresetRegistry.LivingRoom));
    }

    [Test]
    public void LivingRoom_NotEqualToArcade()
        => Assert.That(VideoPresetRegistry.LivingRoom, Is.Not.EqualTo(VideoPresetRegistry.Arcade));
}

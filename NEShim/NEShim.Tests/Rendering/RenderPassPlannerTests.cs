using NEShim.Rendering;

namespace NEShim.Tests.Rendering;

[TestFixture]
internal class RenderPassPlannerTests
{
    [Test]
    public void ColorApplyingStage_NoOverlayNoMotionEffect_ReturnsFilter()
    {
        var stage = RenderPassPlanner.ColorApplyingStage(hasOverlay: false, hasMotionEffectShader: false);
        Assert.That(stage, Is.EqualTo(RenderStage.Filter));
    }

    [Test]
    public void ColorApplyingStage_OverlayOnly_ReturnsOverlay()
    {
        var stage = RenderPassPlanner.ColorApplyingStage(hasOverlay: true, hasMotionEffectShader: false);
        Assert.That(stage, Is.EqualTo(RenderStage.Overlay));
    }

    [Test]
    public void ColorApplyingStage_MotionEffectOnly_ReturnsMotionEffect()
    {
        var stage = RenderPassPlanner.ColorApplyingStage(hasOverlay: false, hasMotionEffectShader: true);
        Assert.That(stage, Is.EqualTo(RenderStage.MotionEffect));
    }

    [Test]
    public void ColorApplyingStage_OverlayAndMotionEffect_MotionEffectWins()
    {
        // Motion effect is always the last stage in the fixed pipeline order
        // (filter -> overlay -> motion effect), so it takes priority when both are active.
        var stage = RenderPassPlanner.ColorApplyingStage(hasOverlay: true, hasMotionEffectShader: true);
        Assert.That(stage, Is.EqualTo(RenderStage.MotionEffect));
    }
}

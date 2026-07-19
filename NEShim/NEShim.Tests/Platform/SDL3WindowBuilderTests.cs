using NEShim.Platform;
using SDL3;

namespace NEShim.Tests.Platform;

[TestFixture]
internal class SDL3WindowBuilderTests
{
    [Test]
    public void ComputeWindowFlags_OnLinux_IncludesVulkan()
    {
        var flags = SDL3WindowBuilder.ComputeWindowFlags(isLinuxOverride: true);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Vulkan), Is.True);
    }

    [Test]
    public void ComputeWindowFlags_NotOnLinux_ExcludesVulkan()
    {
        var flags = SDL3WindowBuilder.ComputeWindowFlags(isLinuxOverride: false);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Vulkan), Is.False);
    }

    [Test]
    public void ComputeWindowFlags_AlwaysIncludesResizableAndHidden()
    {
        var flags = SDL3WindowBuilder.ComputeWindowFlags(isLinuxOverride: false);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Resizable), Is.True);
        Assert.That(flags.HasFlag(SDL.WindowFlags.Hidden), Is.True);
    }
}

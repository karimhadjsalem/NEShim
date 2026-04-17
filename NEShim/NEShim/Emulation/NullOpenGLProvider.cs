using BizHawk.Emulation.Common;

namespace NEShim.Emulation;

internal sealed class NullOpenGLProvider : IOpenGLProvider
{
    // The NES core never calls any OpenGL methods.
    // These stubs satisfy the CoreComm constructor requirement.

    public bool SupportsGLVersion(int major, int minor) => false;

    public object RequestGLContext(int major, int minor, bool coreProfile)
        => throw new NotSupportedException("OpenGL is not supported in NEShim.");

    public void ReleaseGLContext(object context) { }

    public void ActivateGLContext(object context)
        => throw new NotSupportedException("OpenGL is not supported in NEShim.");

    public void DeactivateGLContext() { }

    public IntPtr GetGLProcAddress(string proc) => IntPtr.Zero;
}

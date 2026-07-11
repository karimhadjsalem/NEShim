using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Wraps an SDL_GPUShader + SDL_GPURenderState pair for one SPIR-V fragment shader.
/// Created once per active filter; disposed when the filter changes.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SdlGpuRenderState : IDisposable
{
    // Matches SDL_GPURenderStateCreateInfo layout (SDL 3.4.0+).
    [StructLayout(LayoutKind.Sequential)]
    private struct RenderStateCreateInfo
    {
        public IntPtr FragmentShader;
        public uint   NumVertexSamplers;
        public uint   NumVertexStorageTextures;
        public uint   NumVertexStorageBuffers;
        public uint   NumFragmentSamplers;
        public uint   NumFragmentStorageTextures;
        public uint   NumFragmentStorageBuffers;
        public uint   NumFragmentUniformBuffers;
        public uint   Props;
    }

    private readonly IntPtr _sdlRenderer;
    private readonly IntPtr _gpuDevice;
    private          IntPtr _shader;
    private          IntPtr _renderState;

    /// <summary>
    /// Loads the SPIR-V shader from an embedded resource and creates the GPU render state.
    /// </summary>
    internal SdlGpuRenderState(
        IntPtr sdlRenderer,
        IntPtr gpuDevice,
        string resourceName,
        uint   numFragmentSamplers,
        uint   numFragmentUniformBuffers)
    {
        _sdlRenderer = sdlRenderer;
        _gpuDevice   = gpuDevice;
        _shader      = LoadShader(gpuDevice, resourceName, numFragmentSamplers, numFragmentUniformBuffers);
        if (_shader == IntPtr.Zero) return;

        _renderState = CreateRenderState(sdlRenderer, _shader, numFragmentSamplers, numFragmentUniformBuffers);
        if (_renderState == IntPtr.Zero)
        {
            Logger.Log($"[SdlGpuRenderState] CreateGPURenderState failed for '{resourceName}': {SDL.GetError()}");
            SDL.ReleaseGPUShader(_gpuDevice, _shader);
            _shader = IntPtr.Zero;
        }
    }

    internal bool IsValid => _renderState != IntPtr.Zero;

    /// <summary>
    /// Writes uniform data into slot 0 and applies this render state to the renderer.
    /// The render state remains active for subsequent SDL_RenderTexture calls.
    /// </summary>
    internal unsafe void Apply(Span<float> uniformData)
    {
        if (_renderState == IntPtr.Zero) return;
        if (!uniformData.IsEmpty)
        {
            fixed (float* p = uniformData)
                SDL.SetGPURenderStateFragmentUniforms(_renderState, 0, (IntPtr)p, (uint)(uniformData.Length * sizeof(float)));
        }
        SDL.SetGPURenderState(_sdlRenderer, _renderState);
    }

    /// <summary>Removes the render state from the renderer (returns to default pipeline).</summary>
    internal void Clear() => SDL.SetGPURenderState(_sdlRenderer, IntPtr.Zero);

    public void Dispose()
    {
        if (_renderState != IntPtr.Zero) { SDL.DestroyGPURenderState(_renderState); _renderState = IntPtr.Zero; }
        if (_shader      != IntPtr.Zero) { SDL.ReleaseGPUShader(_gpuDevice, _shader); _shader = IntPtr.Zero; }
    }

    private static IntPtr LoadShader(
        IntPtr gpuDevice,
        string resourceName,
        uint   numSamplers,
        uint   numUniformBuffers)
    {
        byte[]? spv = LoadEmbeddedResource(resourceName);
        if (spv is null)
        {
            Logger.Log($"[SdlGpuRenderState] Embedded SPIR-V resource '{resourceName}' not found.");
            return IntPtr.Zero;
        }

        IntPtr pinnedSpv = Marshal.AllocHGlobal(spv.Length);
        try
        {
            Marshal.Copy(spv, 0, pinnedSpv, spv.Length);
            var shaderInfo = new SDL.GPUShaderCreateInfo
            {
                CodeSize           = (UIntPtr)spv.Length,
                Code               = pinnedSpv,
                Entrypoint         = "main",
                Format             = SDL.GPUShaderFormat.SPIRV,
                Stage              = SDL.GPUShaderStage.Fragment,
                NumSamplers        = numSamplers,
                NumStorageTextures = 0,
                NumStorageBuffers  = 0,
                NumUniformBuffers  = numUniformBuffers,
                Props              = 0,
            };
            IntPtr shader = SDL.CreateGPUShader(gpuDevice, in shaderInfo);
            if (shader == IntPtr.Zero)
                Logger.Log($"[SdlGpuRenderState] CreateGPUShader failed for '{resourceName}': {SDL.GetError()}");
            return shader;
        }
        finally
        {
            Marshal.FreeHGlobal(pinnedSpv);
        }
    }

    private static unsafe IntPtr CreateRenderState(
        IntPtr sdlRenderer,
        IntPtr shader,
        uint   numFragmentSamplers,
        uint   numFragmentUniformBuffers)
    {
        var info = new RenderStateCreateInfo
        {
            FragmentShader           = shader,
            NumVertexSamplers        = 0,
            NumVertexStorageTextures = 0,
            NumVertexStorageBuffers  = 0,
            NumFragmentSamplers      = numFragmentSamplers,
            NumFragmentStorageTextures = 0,
            NumFragmentStorageBuffers  = 0,
            NumFragmentUniformBuffers  = numFragmentUniformBuffers,
            Props                    = 0,
        };
        return SDL.CreateGPURenderState(sdlRenderer, (IntPtr)(&info));
    }

    private static byte[]? LoadEmbeddedResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

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
    // Matches SDL_GPURenderStateCreateInfo layout (SDL 3.4.0+): fragment_shader, then three
    // (count, pointer) pairs for EXTRA bindings beyond what SDL_RenderTexture already supplies
    // for the primary source texture — sampler bindings, storage textures, storage buffers.
    // Confirmed against the real header-derived field list (fragment_shader, num_sampler_bindings,
    // sampler_bindings, num_storage_textures, storage_textures, num_storage_buffers,
    // storage_buffers, props) — the previous version of this struct had a fabricated field set
    // (bogus "vertex" sampler/storage counts, no pointer fields at all, an extra
    // "NumFragmentUniformBuffers" that doesn't exist here) that didn't match any real SDL layout,
    // corrupting native memory on every SDL_CreateGPURenderState call: first field aligned by
    // coincidence, everything after read from the wrong offsets (including past the end of the
    // stack-allocated struct), so a filter needing a shader (i.e. any filter but PixelPerfect)
    // corrupted the GPU renderer's state on Linux — nothing rendered afterward (not even the
    // in-game menu, sharing the same corrupted SDL_Renderer), input froze if Present blocked on a
    // GPU fence that never signaled, while audio (separate subsystem) kept running. Reproduced
    // identically on Steam Deck and WSL2, confirming a real ABI mismatch, not a driver quirk.
    [StructLayout(LayoutKind.Sequential)]
    private struct RenderStateCreateInfo
    {
        public IntPtr FragmentShader;
        public int    NumSamplerBindings;
        public IntPtr SamplerBindings;
        public int    NumStorageTextures;
        public IntPtr StorageTextures;
        public int    NumStorageBuffers;
        public IntPtr StorageBuffers;
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

        _renderState = CreateRenderState(sdlRenderer, _shader);
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

    // No current filter/motion-effect shader needs bindings beyond the primary source texture
    // SDL_RenderTexture already supplies, so every extra (count, pointer) pair stays zero/null.
    private static unsafe IntPtr CreateRenderState(IntPtr sdlRenderer, IntPtr shader)
    {
        var info = new RenderStateCreateInfo
        {
            FragmentShader     = shader,
            NumSamplerBindings = 0,
            SamplerBindings    = IntPtr.Zero,
            NumStorageTextures = 0,
            StorageTextures    = IntPtr.Zero,
            NumStorageBuffers  = 0,
            StorageBuffers     = IntPtr.Zero,
            Props              = 0,
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

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using SDL3;

namespace NEShim.Rendering;

/// <summary>
/// Wraps one SDL_GPUGraphicsPipeline for a single fragment shader, paired with the shared
/// Passthrough.vs.spv vertex shader (loaded once by <see cref="LoadVertexShader"/> and owned by
/// SDL3HwRenderer, passed in here). Replaces SdlGpuRenderState (SDL_CreateGPURenderState), which
/// reproducibly caused SDL's input event queue to go silent forever — main loop and rendering
/// kept running normally — the first time a shader-bound frame was drawn, on both WSL2 and
/// Steam Deck, independent of the X11-vs-Wayland video driver. Matches a still-open upstream
/// report for the exact same API sequence: github.com/libsdl-org/SDL/issues/13892 ("Vulkan/Metal
/// backend crashes on SDL_RenderPresent with no debug info"). This class never touches the
/// swapchain — every draw targets an offscreen texture (a normal SDL_Texture created with
/// TextureAccess.Target), bridged to the manual render pass via the documented
/// "SDL.texture.gpu.texture" property, so SDL_Renderer's own 2D API can read the result
/// afterward exactly like any other texture — no custom code needed on that side.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SdlGpuPipeline : IDisposable
{
    private readonly IntPtr _gpuDevice;
    private          IntPtr _fragmentShader;
    private          IntPtr _pipeline;

    internal bool IsValid => _pipeline != IntPtr.Zero;

    /// <summary>
    /// Loads the fragment shader from an embedded SPIR-V resource and creates the graphics
    /// pipeline, pairing it with the already-loaded shared vertex shader.
    /// </summary>
    internal SdlGpuPipeline(
        IntPtr gpuDevice,
        IntPtr vertexShader,
        SDL.GPUTextureFormat targetFormat,
        string fragmentResourceName,
        uint   numFragmentSamplers,
        uint   numFragmentUniformBuffers)
    {
        _gpuDevice = gpuDevice;

        _fragmentShader = LoadShader(gpuDevice, fragmentResourceName, SDL.GPUShaderStage.Fragment,
            numFragmentSamplers, numFragmentUniformBuffers);
        if (_fragmentShader == IntPtr.Zero) return;

        _pipeline = CreatePipeline(gpuDevice, vertexShader, _fragmentShader, targetFormat);
        if (_pipeline == IntPtr.Zero)
        {
            Logger.Log($"[SdlGpuPipeline] CreateGPUGraphicsPipeline failed for '{fragmentResourceName}': {SDL.GetError()}");
            SDL.ReleaseGPUShader(gpuDevice, _fragmentShader);
            _fragmentShader = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Draws a fullscreen quad — vertex data must already be written to
    /// <paramref name="vertexBuffer"/> by the caller (see SDL3HwRenderer's shared quad buffer) —
    /// into <paramref name="targetTexture"/> (a normal SDL_Texture created with
    /// TextureAccess.Target), clearing it to transparent black first. Binds
    /// <paramref name="sourceBindings"/> as fragment samplers starting at slot 0 and pushes
    /// <paramref name="uniformData"/> to fragment uniform slot 0. Returns false (logging once)
    /// if the pipeline is invalid or the target texture couldn't be bridged to a GPU texture —
    /// callers should skip the draw and leave the target as-is (already cleared/blank) rather
    /// than throw, matching this codebase's existing IsValid-gated pattern for render states.
    /// </summary>
    internal unsafe bool Draw(
        IntPtr commandBuffer,
        IntPtr vertexBuffer,
        IntPtr targetTexture,
        ReadOnlySpan<SDL.GPUTextureSamplerBinding> sourceBindings,
        ReadOnlySpan<float> uniformData)
    {
        if (_pipeline == IntPtr.Zero) return false;

        IntPtr gpuTargetTexture = GetGpuTexture(targetTexture);
        if (gpuTargetTexture == IntPtr.Zero)
        {
            Logger.Log("[SdlGpuPipeline] Draw: target texture has no underlying SDL_GPUTexture.");
            return false;
        }

        Span<SDL.GPUColorTargetInfo> colorTargets = stackalloc SDL.GPUColorTargetInfo[1];
        colorTargets[0] = new SDL.GPUColorTargetInfo
        {
            Texture           = gpuTargetTexture,
            MipLevel          = 0,
            LayerOrDepthPlane = 0,
            ClearColor        = new SDL.FColor { R = 0f, G = 0f, B = 0f, A = 0f },
            LoadOp            = SDL.GPULoadOp.Clear,
            StoreOp           = SDL.GPUStoreOp.Store,
            ResolveTexture    = IntPtr.Zero,
            ResolveMipLevel   = 0,
            ResolveLayer      = 0,
        };

        IntPtr renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargets, 1, IntPtr.Zero);
        if (renderPass == IntPtr.Zero) return false;

        SDL.BindGPUGraphicsPipeline(renderPass, _pipeline);

        Span<SDL.GPUBufferBinding> vbBindings = stackalloc SDL.GPUBufferBinding[1];
        vbBindings[0] = new SDL.GPUBufferBinding { Buffer = vertexBuffer, Offset = 0 };
        SDL.BindGPUVertexBuffers(renderPass, 0, vbBindings, 1);

        if (!sourceBindings.IsEmpty)
            SDL.BindGPUFragmentSamplers(renderPass, 0, sourceBindings, (uint)sourceBindings.Length);

        if (!uniformData.IsEmpty)
        {
            fixed (float* p = uniformData)
                SDL.PushGPUFragmentUniformData(commandBuffer, 0, (IntPtr)p, (uint)(uniformData.Length * sizeof(float)));
        }

        SDL.DrawGPUPrimitives(renderPass, 6, 1, 0, 0);
        SDL.EndGPURenderPass(renderPass);
        return true;
    }

    public void Dispose()
    {
        if (_pipeline       != IntPtr.Zero) { SDL.ReleaseGPUGraphicsPipeline(_gpuDevice, _pipeline); _pipeline = IntPtr.Zero; }
        if (_fragmentShader != IntPtr.Zero) { SDL.ReleaseGPUShader(_gpuDevice, _fragmentShader); _fragmentShader = IntPtr.Zero; }
    }

    // A normal SDL_Texture (TextureAccess.Target, created via the usual SDL_CreateTexture) wraps
    // an underlying SDL_GPUTexture when the GPU renderer backend is active. Querying it via this
    // documented property is what lets our manual render pass target the exact same texture
    // SDL_Renderer's own 2D API (SDL_RenderTexture) reads from afterward — the bridge that lets
    // every custom-pipeline stage stay entirely off the swapchain. Confirmed against the real
    // SDL_render.h header (SDL_PROP_TEXTURE_GPU_TEXTURE_POINTER); not exposed as a named constant
    // in this SDL3-CS version.
    private const string GpuTexturePropertyName = "SDL.texture.gpu.texture";

    /// <summary>
    /// Resolves a normal SDL_Texture (created via SDL_CreateTexture, GPU renderer backend active)
    /// to its underlying SDL_GPUTexture — needed both for this class's own render-pass target and
    /// by callers building <see cref="SDL.GPUTextureSamplerBinding"/> source bindings for
    /// <see cref="Draw"/>, since a texture used as a shader input needs the same bridge as one
    /// used as a render target.
    /// </summary>
    internal static IntPtr GetGpuTexture(IntPtr texture)
    {
        uint props = SDL.GetTextureProperties(texture);
        return props == 0 ? IntPtr.Zero : SDL.GetPointerProperty(props, GpuTexturePropertyName, IntPtr.Zero);
    }

    /// <summary>
    /// Loads the vertex shader shared by every <see cref="SdlGpuPipeline"/> instance. Called
    /// once by SDL3HwRenderer (which owns the returned handle and disposes it via
    /// <see cref="ReleaseVertexShader"/>) — not cached statically here, since a device-loss
    /// recovery constructs a whole new SDL3HwRenderer with a new _gpuDevice, and a shader handle
    /// tied to the old device would be invalid for it.
    /// </summary>
    internal static IntPtr LoadVertexShader(IntPtr gpuDevice) =>
        LoadShader(gpuDevice, "NEShim.Rendering.Shaders.Vulkan.Passthrough.vs.spv", SDL.GPUShaderStage.Vertex, 0, 0);

    internal static void ReleaseVertexShader(IntPtr gpuDevice, IntPtr vertexShader)
    {
        if (vertexShader != IntPtr.Zero) SDL.ReleaseGPUShader(gpuDevice, vertexShader);
    }

    private static IntPtr LoadShader(
        IntPtr gpuDevice,
        string resourceName,
        SDL.GPUShaderStage stage,
        uint   numSamplers,
        uint   numUniformBuffers)
    {
        byte[]? spv = LoadEmbeddedResource(resourceName);
        if (spv is null)
        {
            Logger.Log($"[SdlGpuPipeline] Embedded SPIR-V resource '{resourceName}' not found.");
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
                Stage              = stage,
                NumSamplers        = numSamplers,
                NumStorageTextures = 0,
                NumStorageBuffers  = 0,
                NumUniformBuffers  = numUniformBuffers,
                Props              = 0,
            };
            IntPtr shader = SDL.CreateGPUShader(gpuDevice, in shaderInfo);
            if (shader == IntPtr.Zero)
                Logger.Log($"[SdlGpuPipeline] CreateGPUShader failed for '{resourceName}': {SDL.GetError()}");
            return shader;
        }
        finally
        {
            Marshal.FreeHGlobal(pinnedSpv);
        }
    }

    // Quad vertex layout: {pos.xy, uv.xy} per vertex (16 bytes/vertex), matching
    // Passthrough.vs.hlsl's VSInput (TEXCOORD0 = pos, TEXCOORD1 = uv) and SDL3HwRenderer's
    // shared vertex buffer writes. Standard alpha blend matches the SDL.BlendMode.Blend these
    // intermediate textures already used under the old render-state path. No depth/stencil —
    // GPUDepthStencilState left at its all-false default and TargetInfo.HasDepthStencilTarget
    // (also defaults false) confirms it's genuinely disabled, not just zero-initialized noise.
    private static unsafe IntPtr CreatePipeline(
        IntPtr gpuDevice, IntPtr vertexShader, IntPtr fragmentShader, SDL.GPUTextureFormat targetFormat)
    {
        var attributes = stackalloc SDL.GPUVertexAttribute[2];
        attributes[0] = new SDL.GPUVertexAttribute { Location = 0, BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float2, Offset = 0 };
        attributes[1] = new SDL.GPUVertexAttribute { Location = 1, BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float2, Offset = 8 };

        var bufferDescs = stackalloc SDL.GPUVertexBufferDescription[1];
        bufferDescs[0] = new SDL.GPUVertexBufferDescription { Slot = 0, Pitch = 16, InputRate = SDL.GPUVertexInputRate.Vertex, InstanceStepRate = 0 };

        var colorTargets = stackalloc SDL.GPUColorTargetDescription[1];
        colorTargets[0] = new SDL.GPUColorTargetDescription
        {
            Format = targetFormat,
            BlendState = new SDL.GPUColorTargetBlendState
            {
                SrcColorBlendFactor  = SDL.GPUBlendFactor.SrcAlpha,
                DstColorBlendFactor  = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                ColorBlendOp         = SDL.GPUBlendOp.Add,
                SrcAlphaBlendFactor  = SDL.GPUBlendFactor.One,
                DstAlphaBlendFactor  = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp         = SDL.GPUBlendOp.Add,
                ColorWriteMask       = SDL.GPUColorComponentFlags.R | SDL.GPUColorComponentFlags.G
                                     | SDL.GPUColorComponentFlags.B | SDL.GPUColorComponentFlags.A,
                EnableBlend          = true,
                EnableColorWriteMask = true,
            },
        };

        var createInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader   = vertexShader,
            FragmentShader = fragmentShader,
            VertexInputState = new SDL.GPUVertexInputState
            {
                VertexBufferDescriptions = (IntPtr)bufferDescs,
                NumVertexBuffers         = 1,
                VertexAttributes         = (IntPtr)attributes,
                NumVertexAttributes      = 2,
            },
            PrimitiveType    = SDL.GPUPrimitiveType.TriangleList,
            RasterizerState  = new SDL.GPURasterizerState
            {
                FillMode        = SDL.GPUFillMode.Fill,
                CullMode        = SDL.GPUCullMode.None,
                FrontFace       = SDL.GPUFrontFace.CounterClockwise,
                EnableDepthClip = true,
            },
            MultisampleState = new SDL.GPUMultisampleState { SampleCount = SDL.GPUSampleCount.SampleCount1 },
            DepthStencilState = default,
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = (IntPtr)colorTargets,
                NumColorTargets         = 1,
                HasDepthStencilTarget   = false,
            },
            Props = 0,
        };

        return SDL.CreateGPUGraphicsPipeline(gpuDevice, in createInfo);
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

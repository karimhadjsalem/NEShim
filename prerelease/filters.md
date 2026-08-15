---
layout: default
title: Filters
nav_order: 7
parent: Pre-release
description: "Audio filters and video filters — what each one does, when it's available, and how to configure it."
---

# Filters

NEShim has eight independent filter axes, all configurable at runtime via the in-game pause menu and the main menu under **Settings**. Each persists to `config.json`.

| Axis | Config key | Menu location | Description |
|---|---|---|---|
| Audio Filter | `audioFilter` | Settings → Sound → Audio Filter | DSP processing applied to the NES mono audio output |
| Audio EQ | `audioEqBass` / `audioEqMid` / `audioEqTreble` | Settings → Sound → EQ | 3-band peaking equalizer applied after the audio filter |
| Video Filter | `videoFilter` | Settings → Video → Video Filter | Primary structural transform applied to the NES pixel buffer |
| Video Overlay | `videoFilterOverlay` | Settings → Video → Video Filter → Video Overlay | Second-pass structural filter stacked on top of the primary filter (D3D11 and SDL_GPU/Vulkan) |
| Color Effect | `videoColorFilter` | Settings → Video → Color Effect | Color-grade applied after all structural passes (D3D11 and SDL_GPU/Vulkan) |
| Motion Effect | `videoMotionEffect` | Settings → Video → Motion Effect | Per-frame screen-space displacement applied to the NES frame quad (D3D11 and SDL_GPU/Vulkan) |
| Picture Adjustments | `videoBrightness` / `videoContrast` / `videoSaturation` / `videoHue` | Settings → Video → Picture | Post-process brightness, contrast, saturation, and hue sliders applied after all other video passes (D3D11 and SDL_GPU/Vulkan) |
| Video Presets | `videoPreset` | Settings → Video → Presets | Coordinated preset bundles that apply multiple filter settings in one step (D3D11 and SDL_GPU/Vulkan) |

---

## Audio Filter

Eight audio processors ship with NEShim, all operating on the 44.1 kHz mono output of the NES APU. The processor runs before the SDL3 audio stream and produces stereo output.

| Filter | `audioFilter` value | Signal chain |
|---|---|---|
| Default | `"Default"` | HP@37 Hz → HP@39 Hz → LP@14 kHz. Matches the NES hardware output filter. |
| Warm | `"Warm"` | HP@80 Hz → HP@80 Hz → LP@14 kHz → LP@8 kHz. Raised HP cutoffs tighten bass transients; the extra LP stage rolls off harsh square-wave harmonics above 8 kHz. |
| Pseudo Stereo | `"PseudoStereo"` | Standard NES chain + Haas-effect widening. L = direct × 0.6, R = 20 ms delayed × 0.4. |
| Warm Stereo | `"WarmStereo"` | Pseudo Stereo + independent LP@8 kHz on each channel after the Haas split. |
| Compression | `"Compression"` | Standard NES chain + look-ahead RMS compressor (220-sample window, −6 dBFS threshold, 3:1 ratio, +2 dB makeup). Evens out DPCM channel level spikes. |
| Bass Boost | `"BassBoost"` | Standard NES chain + additive low-shelf boost at 150 Hz (+4 dB at DC, ≈+2 dB at 150 Hz). For fuller sound on bass-light speakers or headphones. |
| Saturation | `"Saturation"` | Standard NES chain + tanh soft-clip (drive = 1.5, normalised). Super-linear below full scale (mild mid-level boost) with smooth limiting at peaks. |
| Pop Filter | `"DmcStabilizer"` | Standard NES chain + slew-rate limiter that softens large DMC (DPCM) sample-to-sample amplitude jumps. DMC samples are 7-bit unsigned values that can jump instantly across the full output range, producing audible pops and clicks. When a consecutive jump exceeds the threshold (~18% of full scale), the delta is blended 50% toward the previous value — a single-sample transition that significantly reduces click amplitude. |

Switching the audio filter takes effect immediately. The new processor's state is reset before it activates to prevent pops from accumulated DC offset.

**Default value:** `"Default"` on Windows. On Steam Deck, the first-run default is `"Saturation"` — see [Steam Deck — Audio default on first run](steamdeck.md#audio-default-on-first-run).

**Adding a new audio filter:** see the [Architecture guide — Adding a new audio processor](architecture.md#adding-a-new-audio-processor).

---

## Audio EQ

A 3-band peaking equalizer applied after the audio processor, available via **Settings → Sound → EQ**. Each band is a Direct Form 1 biquad peaking filter (Q = 0.9) with gain adjustable from −12 dB to +12 dB in 1 dB steps. The EQ is skipped entirely when all three gains are 0 (no processing overhead).

| Band | `config.json` key | Center frequency | Default |
|---|---|---|---|
| Bass | `audioEqBass` | 100 Hz | `0` |
| Mid | `audioEqMid` | 1 kHz | `0` |
| Treble | `audioEqTreble` | 8 kHz | `0` |

The EQ stacks with the selected Audio Filter — audio flows through the filter first, then through the EQ. All filter + EQ combinations are valid. The Sound screen shows **Flat** when all three bands are at 0, or **Custom** when any band is non-zero.

---

## Video Filter (structural)

Controls how the 256×240 NES pixel buffer is scaled and stylised before display. The available options depend on the active rendering path.

| Filter | `videoFilter` value | Shader renderer | Description |
|---|---|:---:|---|
| Pixel Perfect | `"PixelPerfect"` | Yes | Nearest-neighbour scaling with 8:7 pixel aspect ratio correction. NES pixels were never square; this ratio (≈1.143) gives the correct display geometry on modern widescreen monitors. `"NearestNeighbour"` is a legacy alias. |
| Smooth | `"Bilinear"` | Yes | Hann-windowed sinc reconstruction. A radially-symmetric kernel (16 samples in a 4×4 texel grid) weights each NES neighbour by a jinc-frequency sinc tapered with a Hann envelope, then normalises by the total weight. The sinc provides sharp edge reconstruction; the Hann window rolls the kernel smoothly to zero at its 2.44-texel support boundary, suppressing ringing without a second sinc lobe. |
| CRT Scanlines | `"CrtScanlines"` | Yes | Nearest-neighbour with a Gaussian scanline brightness profile. Each NES scanline peaks at full brightness at its centre and fades toward the row edges, recreating the electron-beam spot shape of a CRT phosphor screen. The gap between scanlines emerges from the falloff of adjacent rows rather than binary alternation, so the effect scales naturally — invisible at 1× zoom, increasingly visible as the display size grows. |
| CRT Phosphor | `"CrtPhosphor"` | Yes | CRT scanlines plus an aperture-grille phosphor mask. Each NES pixel is subdivided into three sub-columns (R/G/B dominant), mimicking the continuous vertical phosphor stripes of an aperture-grille CRT (e.g., Sony Trinitron). Stacks with any color effect. |
| CRT Screen | `"CrtScreen"` | Yes | Full-screen CRT simulation combining three effects in one pass: barrel distortion curves the image to match the convex surface of a CRT tube, per-channel chromatic aberration offsets R and B channel UVs independently so colour fringing appears at screen edges, and a radial vignette darkens the corners. UV wrapping is handled — pixels that fall outside [0,1] after warping are clamped to black, producing clean edge falloff. Stacks with any color effect. |
| NTSC Composite | `"NtscComposite"` | Yes | YIQ colour-space NTSC simulation running entirely on the GPU. A 5-tap chroma Gaussian blurs IQ components while keeping Y (luma) sharp, producing authentic chroma smearing and luma/chroma cross-talk at colour boundaries. Adds an animated analogue noise layer that shifts each frame, recreating the grain shimmer of a real composite signal. Output is 256 pixels wide (standard NES resolution). |
| Sharp Pixel | `"Xbr"` | Yes | xBRZ edge-preserving upscaler. Analyses a 5×5 pixel neighbourhood around each texel to classify edge direction and strength, then blends colours across detected edges using two-stage interpolation. Produces crisper diagonal edges and finer sub-pixel detail than Scale2x/EPX, without the colour bleed of HQx-family filters. Full-RGB comparison is used (not luminance-only) so palette entries with equal brightness but different hues are distinguished correctly. Maintains 8:7 pixel aspect ratio. |

**Shader renderer** means D3D11 (Windows) or SDL_GPU/Vulkan (Linux). All filters ship with both DXBC (`.cso`) and SPIR-V (`.spv`) shader variants and work identically on both paths. On systems where the GPU renderer is unavailable (e.g. no Vulkan support), only Pixel Perfect and Bilinear are available.

**Default value:** `"PixelPerfect"`

The active rendering path is detected at startup and shown in `neshim.log` when `enableLogging` is true. The Video Filter sub-menu shows only the options supported by the current renderer.

**Fallback behaviour:** if `config.json` specifies a shader filter but the current renderer is not shader-capable, NEShim logs a warning, falls back to `PixelPerfect`, and saves the fallback value back to `config.json`.

**Adding a new structural filter:** see the [Architecture guide — Adding a new video filter](architecture.md#adding-a-new-video-filter-structural).

---

## Video Overlay

A second structural filter pass applied on top of the primary structural filter. Available on both D3D11 and SDL_GPU/Vulkan.

When a Video Overlay is selected, the renderer renders the primary filter to an intermediate `B8G8R8A8_UNorm` render target sized to the letterbox pixel dimensions, then renders the overlay filter reading from that intermediate into the final frame at the normal NES quad position. Color grading (`videoColorFilter`) is deferred to this second pass so it applies once to the combined result. When the overlay is `"None"` (default), the single-pass path is taken with identical output and no overhead. On D3D11 this is `D3D11Renderer`'s `_overlayRt` intermediate; on SDL_GPU it is `SDL3HwRenderer`'s `_overlayFilterTexture`, composed with a single-sampler `SdlGpuRenderState` pass — the same underlying mechanism SDL_GPU already used for the motion-effect and picture-adjust passes, since a two-pass sequential composite only ever needs one texture sampler per draw.

The overlay slot accepts a subset of structural filters — those composable on top of an already-scaled frame:

| Filter | `videoFilterOverlay` value | Description |
|---|---|---|
| None | `"None"` | No overlay. Single-pass rendering, no additional cost. |
| CRT Scanlines | `"CrtScanlines"` | Scanline pass on top of the primary filter's output. Pairs well with Smooth or NTSC Composite as the base — the primary handles reconstruction or signal simulation, the overlay adds the CRT display surface. |
| CRT Phosphor | `"CrtPhosphor"` | Aperture-grille phosphor mask applied to the primary filter's output. |
| CRT Screen | `"CrtScreen"` | Barrel distortion, chromatic aberration, and vignette applied to the primary filter's output. Wraps any look inside a curved CRT shell. |

**Default value:** `"None"`

**Conflict prevention:** on both rendering paths, the overlay option menu disables any filter that matches the active primary filter, preventing the same filter in both slots. Switching the primary Video Filter to a value that matches the current overlay also automatically resets the overlay to `None`. Config values edited directly in `config.json` are not validated; a duplicate selection produces no useful visual difference from a single pass.

**UV note for overlay shaders:** the intermediate texture holds the upscaled primary frame. Overlay shaders receive UV coordinates spanning 0→1 over the letterbox area and use `nesHeight = 240` for scanline period calculations — the same values as in single-pass mode. Sampling an upscaled intermediate at these UVs gives sub-pixel scanline blending against a higher-resolution source, which generally produces better quality than the equivalent single-pass configuration.

---

## Color Effect

A per-pixel color-grade transform applied after the structural video filter. Available on both D3D11 (Windows) and SDL_GPU/Vulkan (Linux).

| Effect | `videoColorFilter` value | Description |
|---|---|---|
| None | `"None"` | No color transform. Output is the raw NES palette. |
| Warm | `"Warm"` | Slight amber tint. Reds and greens lifted gently; blues desaturated. Recreates the warm cast of a CRT with an aging phosphor coating. |
| Cool | `"Cool"` | Blue-green tint approximating the D93 9300K white point used by CRT displays in consumer televisions. Reds slightly reduced, blues boosted. The cold counterpart to Warm. |
| NES Colors | `"NesColorCorrection"` | Small color-correction matrix shifting from the raw 2C02 composite palette toward a more accurate sRGB representation. Removes the slight pink/purple cast in uncorrected NES output. |
| Greyscale | `"Greyscale"` | Full desaturation using BT.601 luma coefficients (0.299 R + 0.587 G + 0.114 B). Preserves perceived brightness across the NES palette. |
| Phosphor Amber | `"PhosphorAmber"` | Desaturates the image using BT.601 luma coefficients, then tints the result to amber — the characteristic warm orange-yellow hue of monochrome phosphor displays common in early personal computers and terminals. |
| Phosphor Green | `"PhosphorGreen"` | Desaturates the image using BT.601 luma coefficients, then tints the result to phosphor green — the bright green of P1 phosphor used in arcade monitors and early CRT displays. |

**Default value:** `"None"`

**Adding a new color effect:** see the [Architecture guide — Adding a new color effect](architecture.md#adding-a-new-color-effect).

---

## Picture Adjustments

Four independent post-process sliders applied after all structural filter, overlay, and motion effect passes, and before the UI overlay (menus, HUD). Available on both D3D11 (Windows) and SDL_GPU/Vulkan (Linux). When all four are at their neutral values the pass is skipped entirely with no intermediate render target allocated.

| Adjustment | `config.json` field | Range | Default | Description |
|---|---|:---:|:---:|---|
| Brightness | `videoBrightness` | −100 to +100 | `0` | Additive shift applied to linear RGB after color grading. Each unit corresponds to ±0.002 in linear light, so +100 adds +0.2 (roughly a 20% luminance lift). |
| Contrast | `videoContrast` | −100 to +100 | `0` | Scales RGB around mid-grey (0.5). 0 is neutral (1× scale); +100 doubles the contrast; −100 collapses to flat grey. |
| Saturation | `videoSaturation` | −100 to +100 | `0` | Blends linearly between full greyscale (−100) and the original colour (0) through to doubled saturation (+100), using BT.601 luma as the desaturated baseline. |
| Hue | `videoHue` | −100 to +100 | `0` | Rotates all colours around the achromatic (grey) axis using Rodrigues' rotation formula. −100 maps to −π radians (≈ full 180° shift, inverting complementary colour pairs); +100 maps to +π radians (same endpoint from the other direction); 0 is neutral. |

All four are adjustable at runtime via **Settings → Video → Picture** in both the in-game pause menu and the main menu. A Reset option returns all four to their defaults in one step.

---

## Motion Effect

A per-frame animated effect applied to the NES viewport. All four effects — CRT Jitter, Scanline Bob, Magnetic Distortion, and Screen Glow — are available on both D3D11 (Windows) and SDL_GPU/Vulkan (Linux).

Three implementation models exist:

- **CPU quad-offset** (CRT Jitter, Scanline Bob): the displacement is computed on the CPU each frame and written into the vertex buffer quad corners as a clip-space offset. A scissor rect prevents the displaced quad from bleeding into sidebars or letterbox areas. No additional render pass; the overhead is a few ALU instructions before the existing `Draw(6, 0)` call.
- **Shader pass** (Magnetic Distortion): the structural filter (and overlay filter, if active) is rendered into an intermediate letterbox-sized render target first, then a dedicated pixel shader reads from that intermediate and warps pixels per-UV as it renders to the backbuffer. This adds one render pass to the pipeline when active. Identical on D3D11 and SDL_GPU.
- **Temporal accumulation** (Screen Glow): needs the *previous frame's output* as a second input, not just the current frame — a genuine two-sampler shader on D3D11. `SDL_GPURenderState` only binds one texture sampler per draw (no public API exists to bind a second, unlike D3D11's `PSSetShaderResource` slot 1), so SDL_GPU reproduces the same result without a shader at all — see the Screen Glow row below.

| Effect | `videoMotionEffect` value | Description |
|---|---|---|
| None | `"None"` | No effect. The NES frame quad is drawn at its resting position. |
| CRT Jitter | `"CrtJitter"` | Simulates the subtle hold instability of an aging CRT TV. A bounded, non-repeating horizontal (and minimal vertical) offset is derived each frame from the product of two sinusoids at irrational-ratio frequencies. The signal changes sign every 3–6 frames at 60 Hz, reading as nervous micro-jitter rather than slow sway. Horizontal and vertical amplitudes scale independently with viewport width and height respectively, keeping the physical pixel displacement constant across resolutions (calibrated at 1920×1080). |
| Scanline Bob | `"ScanlineBob"` | Alternates the NES frame quad vertically each frame, producing a subtle vertical bob at 30 Hz. Recreates the interlace artifact seen on CRT displays that rendered alternating fields at half the frame rate. The amplitude scales inversely with viewport height so the physical pixel displacement remains constant regardless of screen resolution (calibrated at 1080p). |
| Magnetic Distortion | `"MagneticDistortion"` | Simulates magnetic interference on a CRT by warping UV coordinates in a pixel shader. A sine wave sweeps horizontally across the image each frame — each row is displaced by a different amount, so adjacent rows shift in opposite directions, matching the characteristic non-uniform warp of an external magnetic field deflecting the electron beam unevenly. The wave phase and amplitude evolve slowly over time for an organic feel. Pixels that warp past the horizontal texture boundary render as black, matching the edge roll-off seen on real CRTs. Runs as a shader pass (see above). |
| Screen Glow | `"PhosphorPersistence"` | Simulates CRT phosphor persistence by temporally accumulating frames. Each output pixel is `max(current frame, previous output × 0.65)`, producing a soft after-image trail that fades over approximately 10–15 frames. Uses a ping-pong pair of intermediate render targets (see above). D3D11 reads both textures in one 2-sampler pixel shader pass. SDL_GPU has no way to bind a second sampler through `SDL_GPURenderState`, so it reproduces the identical formula with two ordinary single-sampler draws into the ping-pong buffer: the current frame drawn first, then the history buffer drawn on top with `SDL_SetTextureColorModFloat` pre-scaling its RGB by the 0.65 decay factor and a custom `SDL_ComposeCustomBlendMode` blend (`Maximum` operation) combining it with what's already there — GPU fixed-function blending standing in for the shader's `max()` call, with no CPU pixel work and no SPIR-V shader involved. |

**Default value:** `"None"`

---

## Video Presets

Five built-in presets each apply a coordinated combination of filter settings — Video Filter, Video Overlay, Color Effect, Motion Effect, Overscan, and picture adjustments — in a single selection. Available on both D3D11 (Windows) and SDL_GPU/Vulkan (Linux), including the Video Overlay and Screen Glow components — every preset setting takes effect identically on both rendering paths.

| Preset | `videoPreset` value | Video Filter | Video Overlay | Color Effect | Motion Effect |
|---|---|---|---|---|---|
| No Filters | `"NoFilters"` | Pixel Perfect | None | None | None |
| Living Room | `"LivingRoom"` | CRT Screen | CRT Scanlines | NES Colors | CRT Jitter |
| Arcade Monitor | `"Arcade"` | CRT Phosphor | None | Cool | CRT Jitter |
| Sharp | `"Sharp"` | Sharp Pixel | None | NES Colors | None |
| Phosphor | `"Phosphor"` | CRT Screen | CRT Phosphor | Phosphor Amber | Screen Glow |

All presets use Normal overscan and neutral picture adjustments (0 brightness, 0 contrast, 0 saturation, 0 hue).

The active preset name is shown inline on the Video settings screen next to the Presets entry. The Presets sub-menu itself lists these five plus a sixth entry, **"No Preset"** (`videoPreset: "None"`) — the item at the top of the list. These two are easy to confuse and behave differently:

- **"No Preset"** just clears the tracked preset label without touching any filter setting — whatever Video Filter/Overlay/Color/Motion/Picture values are currently active stay exactly as they are.
- **"No Filters"** is a real preset like the other four — it actively resets every video setting to the engine's neutral defaults (Pixel Perfect, no overlay, no color effect, no motion effect, Normal overscan, 0/0/0/0 picture adjustments).

**Changing any individual setting after applying a preset automatically resets `videoPreset` to `"None"`** ("No Preset") — the preset name is a label, not a constraint; manual changes take full effect immediately.

**Default value:** `"None"`

---

## Combining video filters

Any structural filter can be combined with any overlay filter, any color effect, and any motion effect. Some examples:

| Visual goal | Video Filter | Video Overlay | Color Effect | Motion Effect |
|---|---|---|---|---|
| Classic accurate look | Pixel Perfect | None | None | None |
| Retro CRT with aging monitor | CRT Scanlines | None | Warm | None |
| Black-and-white film look | Pixel Perfect | None | Greyscale | None |
| Full composite TV simulation | NTSC Composite | None | NES Colors | None |
| Softer look with color correction | Smooth | None | NES Colors | None |
| Authentic cold CRT (slot-mask + D93 white point) | CRT Phosphor | None | Cool | None |
| 1980s arcade monitor look | CRT Phosphor | None | Warm | None |
| Curved screen amber terminal | CRT Screen | None | Phosphor Amber | None |
| Classic green monochrome CRT | Pixel Perfect | None | Phosphor Green | None |
| Full vintage CRT simulation | CRT Screen | None | Warm | CRT Jitter |
| Interlaced phosphor display | CRT Scanlines | None | None | Scanline Bob |
| Smooth reconstruction + scanlines | Smooth | CRT Scanlines | None | None |
| Smooth reconstruction + phosphor mask | Smooth | CRT Phosphor | Warm | None |
| Sharp pixels in curved CRT shell | Pixel Perfect | CRT Screen | None | None |
| Composite signal in curved CRT | NTSC Composite | CRT Screen | NES Colors | None |
| Composite with scanline overlay | NTSC Composite | CRT Scanlines | None | None |
| Full vintage CRT (curved + scanlines) | Pixel Perfect | CRT Screen | Warm | CRT Jitter |
| Magnetic interference (warm CRT) | CRT Scanlines | None | Warm | Magnetic Distortion |
| Composite TV under interference | NTSC Composite | None | NES Colors | Magnetic Distortion |
| Sharp pixels + magnetic warp | Pixel Perfect | None | None | Magnetic Distortion |

---

## Shader architecture

Every video filter ships with two compiled variants, both embedded as assembly resources:

- **DXBC** (`.cso` files in `Rendering/Shaders/Dx11/`) — compiled by `fxc.exe` (Windows SDK); used by `D3D11Renderer` on Windows.
- **SPIR-V** (`.spv` files in `Rendering/Shaders/Vulkan/`) — compiled by `dxc.exe` (`Microsoft.Direct3D.DXC` NuGet); used by `SDL3HwRenderer` on Linux and as the Windows fallback via SDL_GPU.

Both variants are compiled from the same HLSL source and share the same color-grade include (`ColorGrade.hlsli`) and constant buffer layout, so the visual output is identical across platforms.

CRT Scanlines, CRT Phosphor, CRT Screen, and NTSC Composite are pixel shaders. All color effects run via a shared include. Smooth (Bilinear) uses `Jinc2.ps.*` — a 16-sample radially-symmetric reconstruction filter. For each output pixel it evaluates a Hann-windowed sinc kernel over a 4x4 NES texel grid: each sample weight is `sinc_norm(r / r1) * hann(r)` where r1 = 1.2197 (first zero of the jinc function) and the Hann envelope tapers the kernel smoothly to zero at the 2.44-texel support boundary. Weights are summed and the result is divided by their total, giving a properly normalised reconstruction with crisp edges and suppressed ringing. A linear-clamp sampler is used; the shader samples at exact texel centres so the sampler mode does not affect reconstruction quality.

### Uniform constant buffer

All pixel shaders share the same 4-float constant buffer (`b0`):

```hlsl
cbuffer FilterParams : register(b0)
{
    float param0;     // structural param 0  (nesWidth for CRT, invWidth for NTSC, barrelStrength for CrtScreen, 0 for PP)
    float param1;     // structural param 1  (nesHeight for CRT, frameParity for NTSC, chromaStrength for CrtScreen, 0 for PP)
    float param2;     // structural param 2  (scanlineIntensity / chromaStrength / vignetteStrength / 0)
    float colorMode;  // 0=none  1=warm  2=greyscale  3=nes_colors  4=cool  5=phosphor_amber  6=phosphor_green — written by renderer
}
```

The renderer always writes the active `VideoColorFilterMode` into `param[3]`. Each structural filter writes its own params into `param[0..2]` via `ID3D11Filter.WriteBaseParams()`. For Pixel Perfect all three structural params are zero.

The buffer is intentionally fixed at 4 floats. No filter may use a second constant buffer; if a future filter genuinely needs more than 3 config floats, update the design rule in `CLAUDE.md` and `D3D11Renderer` deliberately.

### Shared color grade include

All shaders `#include "ColorGrade.hlsli"` and call `ApplyColorGrade(color, colorMode)` as their final step. This means any structural filter + color effect combination works without shader permutations — the grade is baked into every shader via the shared include rather than compiled as separate variants.

The `colorMode` integer encodes all seven grades at fixed positions in the HLSL `if` chain: 0=none, 1=warm, 2=greyscale, 3=nes_colors, 4=cool, 5=phosphor_amber, 6=phosphor_green. The `VideoColorFilterMode` enum declaration order matches these positions exactly; adding a new color mode requires updating both the enum and the HLSL `if` chain together.

### Passthrough shader

`Passthrough.ps.cso` applies only the color grade, with no structural effect. It is bound in place of the active structural shader for two draw calls each frame:

- **Sidebar quads** — so letterbox bar artwork is not distorted by scanlines or NTSC simulation. Color effects are also suppressed here (`colorMode=0`), so sidebar art is always displayed in its original colours regardless of the active Color Effect.
- **Overlay quad** — so the SDL3PaintContext-rendered overlay (menus, frozen frame background, HUD elements) is not affected by structural filters. The color grade still applies via `colorMode`, keeping the overlay tonally consistent with the NES frame.

### Proton / DXVK

When running via Proton (Windows D3D11 path on Steam Deck), DXBC shaders are compiled to SPIR-V by DXVK on first launch and cached in Steam's shader cache. The shaders are simple (under 30 instructions each); compilation is near-instant. Subsequent launches use the cached SPIR-V.

When running natively on Linux (SDL_GPU path), pre-compiled SPIR-V `.spv` files embedded in the assembly are used directly — no Proton, no DXVK, and no shader compilation step on first launch.

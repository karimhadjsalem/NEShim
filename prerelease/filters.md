---
layout: default
title: Filters
nav_order: 7
parent: Pre-release
description: "Audio filters and video filters — what each one does, when it's available, and how to configure it."
---

# Filters

NEShim has three independent filter axes, all configurable at runtime via the in-game pause menu and the main menu under **Settings**. Each persists to `config.json`.

| Axis | Config key | Menu location | Description |
|---|---|---|---|
| Audio Filter | `audioFilter` | Settings → Sound → Audio Filter | DSP processing applied to the NES mono audio output |
| Video Filter | `videoFilter` | Settings → Video → Video Filter | Structural transform applied to the NES pixel buffer |
| Color Effect | `videoColorFilter` | Settings → Video → Color Effect | Color-grade applied after the structural video filter (D3D11 only) |

---

## Audio Filter

Seven audio processors ship with NEShim, all operating on the 44.1 kHz mono output of the NES APU. The processor runs before the NAudio ring buffer and produces stereo output.

| Filter | `audioFilter` value | Signal chain |
|---|---|---|
| Default | `"Default"` | HP@37 Hz → HP@39 Hz → LP@14 kHz. Matches the NES hardware output filter. |
| Warm | `"Warm"` | HP@80 Hz → HP@80 Hz → LP@14 kHz → LP@8 kHz. Raised HP cutoffs tighten bass transients; the extra LP stage rolls off harsh square-wave harmonics above 8 kHz. |
| Pseudo Stereo | `"PseudoStereo"` | Standard NES chain + Haas-effect widening. L = direct × 0.6, R = 20 ms delayed × 0.4. |
| Warm Stereo | `"WarmStereo"` | Pseudo Stereo + independent LP@8 kHz on each channel after the Haas split. |
| Compression | `"Compression"` | Standard NES chain + look-ahead RMS compressor (220-sample window, −6 dBFS threshold, 3:1 ratio, +2 dB makeup). Evens out DPCM channel level spikes. |
| Bass Boost | `"BassBoost"` | Standard NES chain + additive low-shelf boost at 150 Hz (+4 dB at DC, ≈+2 dB at 150 Hz). For fuller sound on bass-light speakers or headphones. |
| Saturation | `"Saturation"` | Standard NES chain + tanh soft-clip (drive = 1.5, normalised). Super-linear below full scale (mild mid-level boost) with smooth limiting at peaks. |

Switching the audio filter takes effect immediately. The new processor's state is reset before it activates to prevent pops from accumulated DC offset.

**Default value:** `"Default"`

**Adding a new audio filter:** see the [Architecture guide — Adding a new audio processor](architecture.md#adding-a-new-audio-processor).

---

## Video Filter (structural)

Controls how the 256×240 NES pixel buffer is scaled and stylised before display. The available options depend on the active rendering path.

| Filter | `videoFilter` value | GDI+ | D3D11 | Description |
|---|---|:---:|:---:|---|
| Pixel Perfect | `"PixelPerfect"` | Yes | Yes | Nearest-neighbour scaling with 8:7 pixel aspect ratio correction. NES pixels were never square; this ratio (≈1.143) gives the correct display geometry on modern widescreen monitors. `"NearestNeighbour"` is a legacy alias. |
| Smooth | `"Bilinear"` | Yes | — | Bilinear interpolation for a softer, anti-aliased look at arbitrary window sizes. GDI+ only. |
| CRT Scanlines | `"CrtScanlines"` | — | D3D11 only | Nearest-neighbour with an alternating scanline darkening pattern. Every second horizontal line is darkened, approximating the phosphor gap of a CRT television. |
| NTSC Composite | `"NtscComposite"` | — | D3D11 only | Simulates NTSC composite signal degradation: horizontal chroma smearing, luma/chroma cross-talk, and a subtle noise layer. Reproduces the characteristic blended look of NES games on a composite TV connection. |

**Default value:** `"PixelPerfect"`

The active rendering path is detected at startup and shown in `neshim.log` when `enableLogging` is true. It can be forced to GDI+ for debugging via `"forceRenderer": "gdi"` in `config.json`. The Video Filter sub-menu shows only the options supported by the current renderer — D3D11-only filters do not appear in GDI+ mode.

**Fallback behaviour:** if `config.json` specifies a D3D11-only filter but D3D11 is unavailable, NEShim logs a warning, falls back to `PixelPerfect`, and saves the fallback value back to `config.json`.

**Adding a new structural filter:** see the [Architecture guide — Adding a new D3D11 video filter](architecture.md#adding-a-new-d3d11-video-filter-structural).

---

## Color Effect

A per-pixel color-grade transform applied after the structural video filter. D3D11 only — the selection is stored in `config.json` in GDI+ mode but has no visual effect until D3D11 is available.

| Effect | `videoColorFilter` value | Description |
|---|---|---|
| None | `"None"` | No color transform. Output is the raw NES palette. |
| Warm | `"Warm"` | Slight amber tint. Reds and greens lifted gently; blues desaturated. Recreates the warm cast of a CRT with an aging phosphor coating. |
| Greyscale | `"Greyscale"` | Full desaturation using BT.601 luma coefficients (0.299 R + 0.587 G + 0.114 B). Preserves perceived brightness across the NES palette. |
| NES Colors | `"NesColorCorrection"` | Small color-correction matrix shifting from the raw 2C02 composite palette toward a more accurate sRGB representation. Removes the slight pink/purple cast in uncorrected NES output. |

**Default value:** `"None"`

The Color Effect sub-menu is always visible regardless of renderer. In GDI+ mode the selection persists to config but has no immediate visual impact.

**Adding a new color effect:** see the [Architecture guide — Adding a new color effect](architecture.md#adding-a-new-color-effect).

---

## Combining video filters

Any structural filter can be combined with any color effect. Some examples:

| Visual goal | Video Filter | Color Effect |
|---|---|---|
| Classic accurate look | Pixel Perfect | None |
| Retro CRT with aging monitor | CRT Scanlines | Warm |
| Black-and-white film look | Pixel Perfect | Greyscale |
| Full composite TV simulation | NTSC Composite | NES Colors |
| Softer look with color correction | Pixel Perfect | NES Colors |

---

## D3D11 shader architecture

CRT Scanlines, NTSC Composite, and all color effects are implemented as DXBC pixel shaders compiled to `.cso` files and embedded as assembly resources.

### Uniform constant buffer

All pixel shaders share the same 4-float constant buffer (`b0`):

```hlsl
cbuffer FilterParams : register(b0)
{
    float param0;     // structural param 0  (nesWidth for CRT, invWidth for NTSC, 0 for PP)
    float param1;     // structural param 1  (nesHeight / invHeight / 0)
    float param2;     // structural param 2  (scanlineIntensity / chromaStrength / 0)
    float colorMode;  // 0=none  1=warm  2=greyscale  3=nes_colors — written by renderer
}
```

The renderer always writes the active `VideoColorFilterMode` into `param[3]`. Each structural filter writes its own params into `param[0..2]` via `ID3D11Filter.WriteBaseParams()`. For Pixel Perfect all three structural params are zero.

### Shared color grade include

All shaders `#include "ColorGrade.hlsli"` and call `ApplyColorGrade(color, colorMode)` as their final step. This means any structural filter + color effect combination works without shader permutations — the grade is baked into every shader via the shared include rather than compiled as separate variants.

### Passthrough shader

`Passthrough.ps.cso` applies only the color grade, with no structural effect. It is bound in place of the active structural shader for two draw calls each frame:

- **Sidebar quads** — so letterbox bar artwork is not distorted by scanlines or NTSC simulation.
- **Overlay quad** — so the GDI+-rendered overlay (menus, frozen frame background, HUD elements) is not affected by structural filters. The color grade still applies via `colorMode`, keeping the overlay tonally consistent with the NES frame.

### DXVK / Proton

DXBC shaders are compiled to SPIR-V by DXVK on first launch and cached in Steam's shader cache directory. The shaders are simple (< 30 instructions each); compilation is near-instant.

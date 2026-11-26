# Stage 17 · Dense WebGPU redraws

Run from the repository root after installing dependencies.

    npm run demo -- stage-17-dense-webgpu
    npm run bench:webgpu

The demo serves a local interactive comparison. The benchmark drives it through Edge, captures matching snapshots and writes artifacts/dense-webgpu/results.json. Each renderer draws the same 960 × 600 scene with identical rectangles, colors and one cached glyph surface. The sweep covers 2, 128, 512 and 2,048 controls and changes the color state of 0%, 10% or 100% of controls per frame, rounded to a whole control.

The Stage 14 WebGPU renderer now uses one instanced triangle-strip draw for all rectangles, with one 16-byte geometry record and one four-byte color value per control. The earlier geometry expanded each rectangle into six interleaved vertices. The new renderer keeps geometry resident and uploads one contiguous span of prepacked RGBA8 values when the active color state changes. It composites glyphs in a second draw. The Canvas 2D baseline uses cached CSS colors and the same glyph surface. The timed sweep reuses rasterized glyphs and changes colors.

## Measured run

Windows, Edge 158.0.4370.33, NVIDIA Blackwell hardware adapter. The machine reports an RTX 5080 and driver 32.0.16.1664. Each scenario alternates renderer order across three repetitions of 32 measured frames after eight warm-up frames.

| Controls | Changed | WebGPU submission p95 | Canvas 2D submission p95 |
| ---: | ---: | ---: | ---: |
| 2 | 0% / 10% / 100% | 0.20 / 0.10 / 0.20 ms | 0.10 / 0.10 / 0.10 ms |
| 128 | 0% / 10% / 100% | 0.20 / 0.10 / 0.10 ms | 0.10 / 0.10 / 0.10 ms |
| 512 | 0% / 10% / 100% | 0.10 / 0.20 / 0.10 ms | 0.20 / 0.20 / 0.10 ms |
| 2,048 | 0% / 10% / 100% | 0.10 / 0.20 / 0.20 ms | 0.30 / 0.30 / 0.30 ms |

At two controls the 10% update rounded to zero controls. With 2,048 controls, the WebGPU submissions took one-third to two-thirds as long as Canvas 2D in this run. Both renderers recorded a 20.6 ms p95 frame interval across the dense cases. Smaller submission differences were close to the timer resolution.

The submission timer covers main-thread updates and draw calls. The timing JSON also records the elapsed wait for WebGPU's submitted work after each repetition. Those waits ranged from 0.30 to 4.70 ms across the dense cases. They include the time to resolve the completion promise. The benchmark measures frame pacing independently because composition, text rerasterization and input handling can affect end-to-end performance.

The verifier compares all 576,000 pixels at each control count and requires every RGB channel to agree within one level. It saves both 2,048-control screenshots beside the timing JSON.

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
| 2 | 0% / 100% | 0.10 / 0.20 ms | 0.10 / 0.10 ms |
| 128 | 0% / 10% / 100% | 0.10 / 0.20 / 0.10 ms | 0.10 / 0.10 / 0.10 ms |
| 512 | 0% / 10% / 100% | 0.10 / 0.20 / 0.10 ms | 0.10 / 0.20 / 0.20 ms |
| 2,048 | 0% / 10% / 100% | 0.10 / 0.20 / 0.10 ms | 0.50 / 0.40 / 0.40 ms |

The 2,048-control submissions were 2–5× shorter in this run. At smaller sizes the differences were close to timer resolution. The 2,048-control frame-interval p95s ranged from 7.8–9.6 ms for WebGPU and 7.8–8.1 ms for Canvas 2D. These frame intervals show no consistent improvement. Submission measures main-thread update and draw calls, with GPU completion awaited outside the timed WebGPU loop. Browser composition, text rerasterization and other input-driven work can change the result.

At each control count, screenshots with an active range matched within one level per RGB channel. The benchmark saves both 2,048-control screenshots beside the timing JSON.

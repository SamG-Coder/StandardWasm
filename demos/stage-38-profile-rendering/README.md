# Stage 38 · Profile rendering timings

Run on Windows from the repository root.

    npm ci
    npm run demo -- stage-38-profile-rendering

The runner first verifies the native WinForms and Edge WebGPU Profile interaction. It then alternates the same two UTF-16 strings through the shared C# `TextChanged` handler. It writes the timing summary and renderer screenshots to `artifacts/profile-rendering/`. The timing run requires cross-origin isolation and records the smallest positive browser clock step it observes.

| Report field | Measured work |
| --- | --- |
| Native update | Assign `TextBox.Text` and run the native callback, including synchronous WinForms work during the assignment. |
| Native refresh | Call `Form.Refresh()` for all four controls on a transparent, layered form. The timing ends before desktop composition. |
| Browser update | Transfer UTF-16 to Wasm, raise managed `TextChanged`, and decode the resulting preview. |
| Browser submit | Rasterize the text layer for the canvas, then submit the WebGPU draw or issue the Canvas 2D paint calls. The timer stops when the CPU call returns. GPU completion and presentation have separate timing endpoints. |

Edge collects 40 frames after 12 warmup frames per renderer in each of four batches. The runner switches renderer order each batch and waits for an animation frame before each sample. Native WinForms uses 12 warmup updates followed by 160 measured updates, back to back. Both browser renderers redraw the same 420 × 238 canvas with the same managed layout and rasterized text layer. The screenshots for both captions compare browser compositing within three RGB levels for at least 99.5% of pixels.

## Observed on one Windows device

The AMD Ryzen 7 9800X3D system used an NVIDIA GeForce RTX 5080 with driver 32.0.16.1664. Edge 158.0.4370.33 reported a hardware Blackwell WebGPU adapter and an observed clock step of 0.005 ms with cross-origin isolation enabled. The run used Node 24.19.0 and .NET 10.0.10. Both screenshot comparisons had zero pixels beyond tolerance. The maximum channel difference was one level.

| Metric | Median | p95 |
| --- | ---: | ---: |
| Native update | 0.201 ms | 0.545 ms |
| Native refresh | 2.125 ms | 3.231 ms |
| WebGPU browser submission | 0.100 ms | 0.155 ms |
| Canvas 2D browser submission | 0.055 ms | 0.090 ms |

In this run, the median CPU-side submission time for WebGPU was about 1.8 times the Canvas 2D median, a difference of 0.045 ms for this workload. Native refresh measures synchronous GDI/GDI+ painting of four controls into a layered window. Browser submission measures canvas-only CPU work at animation-frame pacing. A Windows-versus-browser performance comparison needs matched work, pacing, and measurement endpoints.

# Stage 38 · Profile rendering timings

Run on Windows from the repository root.

    npm ci
    npm run demo -- stage-38-profile-rendering

The runner first verifies the native WinForms and Edge WebGPU Profile interaction. It then alternates the same two UTF-16 strings through the shared C# `TextChanged` handler. It writes the timing summary and renderer screenshots to `artifacts/profile-rendering/`.

| Report field | Measured work |
| --- | --- |
| Native update | Assign `TextBox.Text` and run the native callback, including synchronous WinForms work during the assignment. |
| Native refresh | Call `Form.Refresh()` on the shown, transparent test form. This omits desktop composition. |
| Browser update | Transfer UTF-16 to Wasm, raise managed `TextChanged`, and decode the resulting preview. |
| Browser submit | Rasterize the same text layer, then submit the WebGPU draw or issue the Canvas 2D paint calls. |
| WebGPU queue drain | Wait for outstanding submitted work after each batch. This is a batch-level observation. |

Edge collects 40 frames after 12 warmup frames per renderer in each of four batches. The runner switches renderer order each batch. Native WinForms uses 12 warmup updates followed by 160 measured updates. Both browser renderers use the same 420 × 238 surface, managed layout, text layer, and complete redraw. The screenshots for both captions must agree within three RGB levels for at least 99.5% of pixels.

## Observed on one Windows device

The AMD Ryzen 7 9800X3D system used an NVIDIA GeForce RTX 5080 with driver 32.0.16.1664. The browser reported a hardware Blackwell WebGPU adapter. Edge 158.0.4370.33, Node 24.19.0, and .NET 10.0.10 were used. Both screenshot comparisons had zero pixels beyond tolerance. The maximum channel difference was one level. This table shows the second run.

| Metric | Median | p95 |
| --- | ---: | ---: |
| Native update | 0.197 ms | 0.275 ms |
| Native refresh | 1.606 ms | 1.992 ms |
| WebGPU browser submission | 0.100 ms | 0.200 ms |
| Canvas 2D browser submission | 0.100 ms | 0.100 ms |

Browser submission medians were 0.100 ms in both runs. Canvas 2D p95 varied from 0.100 to 0.200 ms and WebGPU p95 was 0.200 ms at the observed 0.1 ms timer granularity. Native refresh and browser submission stop at different points in the pipeline. These measurements describe synchronous work within each host. GPU execution and form-to-display latency require different timing boundaries.

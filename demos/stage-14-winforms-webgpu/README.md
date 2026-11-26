# Stage 14 · WinForms on WebGPU

Run `npm ci` at the repository root. Start the local browser demo with:

    npm run winforms

Open http://127.0.0.1:5173 in a browser with WebGPU. The server binds to loopback. To run the native form on Windows:

    dotnet run -c Release --project demos/stage-14-winforms-webgpu/Desktop

## What runs where

Both builds compile Shared/CounterForm.cs. The Windows head constructs a native Form, Button and Label using System.Windows.Forms and binds the Click event. The browser build compiles the same layout against a small compatibility API. It executes the constructor at build time to produce the control model and the handler binding. The browser build uses the portable StandardWasm compiler on .NET to emit the shared CounterHandlers.cs transition from its .NET Standard DLL. The browser loads the emitted Wasm directly. The binding factory creates a closed managed delegate over CounterState in Wasm. Browser events dispatch that delegate, which invokes the bound instance method with typed call_ref. The browser host reads the resulting value, formats the label and renders the model.

WebGPU draws the control geometry and composites a cached glyph texture. Canvas 2D rasterizes text when the label changes. A transparent HTML button supplies pointer and keyboard semantics, and an accessible output mirrors the displayed result. Supported WinForms APIs cover the controls, properties and binding used by this demo. General designer-generated forms, arbitrary event delegates, dynamic control trees, and execution of the WinForms control objects inside Wasm are future compatibility work.

## Verification and timing

    npm run test:winforms

On Windows, the test constructs and shows a real WinForms form, calls Button.PerformClick, and exercises Enter through WinForms' dialog-key handling. In Edge, it sends a pointer click and an Enter key event and checks both the accessible output and visible changes in the WebGPU label. Both runs must produce Count: 0, Count: 1 and Count: 2. Generated screenshots and timings are under artifacts/winforms.

One run on Edge 158.0.4370.33, using a hardware NVIDIA Blackwell adapter, changed 302 pixels in the label region. For 90 redraws of the 420 by 240 form after warm-up, main-thread draw-call p95 was about 0.1 ms for WebGPU and about 0.1 ms for the offscreen Canvas 2D baseline. Both reused the same cached glyph image. Medians were below the clock resolution. The final WebGPU queue drain was about 0.3 ms. This measures cached draw submission, with queue drain reported separately. It establishes no speedup for this small form.

See [Stage 17](../stage-17-dense-webgpu/README.md) for the batched renderer and a dense same-device comparison.

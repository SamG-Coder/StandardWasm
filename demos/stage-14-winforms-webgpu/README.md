# Stage 14 · WinForms on WebGPU

Run `npm ci` at the repository root. Start the local browser demo with:

    npm run winforms

Open http://127.0.0.1:5173 in a browser with WebGPU. The server binds to loopback. To run the native form on Windows:

    dotnet run -c Release --project demos/stage-14-winforms-webgpu/Desktop

## What runs where

Both builds use Shared/ControlTree.cs and Shared/CounterHandlers.cs. The Windows head constructs a native Form, Button and Label using System.Windows.Forms and applies rectangles from the shared tree. The browser build packages numeric control-schema IDs and compiles the tree, captions and event code from its .NET Standard DLL. The browser creates the control tree inside Wasm, reads its current geometry, and creates a closed managed delegate over CounterState. Pointer and keyboard events dispatch that delegate and update the same managed tree. The browser host reads managed UTF-16 captions and the complete formatted label, then redraws from the live bounds. See [Stage 21](../stage-21-managed-formatting/README.md) for the shared native and Wasm formatter.

WebGPU draws the control geometry and composites a cached glyph texture. Canvas 2D rasterizes text when the label changes. A transparent HTML button supplies pointer and keyboard semantics, and an accessible output mirrors the displayed result. Supported WinForms APIs cover the controls, properties and binding used by this demo. General designer-generated forms, arbitrary event delegates, dynamic insertion or removal of controls, and executing the full WinForms control types inside Wasm are future compatibility work.

## Verification and timing

    npm run test:winforms

On Windows, the test constructs and shows a real WinForms form, calls Button.PerformClick, and exercises Enter through WinForms' dialog-key handling. In Edge, it sends a pointer click and an Enter key event and checks both the accessible output and visible changes in the WebGPU label. Both runs must produce Count: 0, Count: 1 and Count: 2. The label also moves from x=32 to x=56 and back to x=32 in both hosts. Generated screenshots and timings are under artifacts/winforms.

One run on Edge 158.0.4370.33, using a hardware NVIDIA Blackwell adapter, changed 302 pixels in the label region. For 90 redraws of the 420 by 240 form after warm-up, main-thread draw-call p95 was about 0.1 ms for WebGPU and about 0.1 ms for the offscreen Canvas 2D baseline. Both reused the same cached glyph image. Medians were below the clock resolution. The final WebGPU queue drain was about 0.3 ms. This measures cached draw submission, with queue drain reported separately. It establishes no speedup for this small form.

See [Stage 17](../stage-17-dense-webgpu/README.md) for the batched renderer and a dense same-device comparison.

See [Stage 19](../stage-19-runtime-controls/README.md) for runtime layout and managed storage details.

See [Stage 20](../stage-20-managed-strings/README.md) for the managed string representation and tests.

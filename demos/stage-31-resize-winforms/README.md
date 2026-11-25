# Stage 31 · Resizable WinForms layout

Run from the repository root.

    npm run demo -- stage-31-resize-winforms
    npm run test:winforms

The stage demo compiles the counter's shared managed layout and returns 264, the extra button's left edge after resizing the client area from 520 × 300 to 480 × 280 with that button present. The full test runs the native WinForms form and the Edge WebGPU host.

Run npm run winforms to open the interactive browser demo. Drag the grip at the lower-right of the form to resize it. On Windows, run the Desktop project in stage 14 and drag the window border. The label stretches horizontally, the default button stays at the bottom-left and the additional button follows the bottom-right edge whenever the counter shows it.

Both hosts invoke the same C# resize handler. The browser measures the form's content box, resizes the drawing surface and comparison canvas, and replaces the text texture at the new size. The existing control nodes and WebGPU pipelines are reused. The native window uses its ClientSizeChanged event to update the real WinForms controls.

The test suite compares managed resize results against the CLR in Node and Edge. The native and browser test compares five resize and click states, including a real drag of the Edge resize handle, and checks focus, the retired hit target, and the rendered pixels. It records draw-submission timings at 520 × 300 and 420 × 240 in artifacts/winforms/timing.json. Run npm run bench:webgpu for the dense workload on the same adapter.

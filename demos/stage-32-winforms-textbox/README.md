# Stage 32 · WinForms text entry

Run from the repository root.

    npm run demo -- stage-32-winforms-textbox

Open the printed local URL and type into Name. The WebGPU preview shows Hello, followed by the current value. Clear empties the input and returns focus to it. To run the native Windows form, use:

    dotnet run -c Release --project demos/stage-32-winforms-textbox/Desktop

Both hosts use the same C# profile model and handler. Windows uses WinForms TextBox.TextChanged. The browser uses a standard single-line input for editing, selection and keyboard behavior, and forwards UTF-16 code units to the managed Wasm model. Input under composition is forwarded after composition ends. The C# state retains the string in a sealed object and formats the preview, which WebGPU renders on the canvas.

Run the native/browser parity check with:

    node scripts/winforms-textbox.mjs --verify

It compares the two text controls and previews through Unicode entry, selection replacement, Backspace and Clear. It also checks input focus and compares pixels inside the WebGPU preview. npm test checks default and explicit null string fields, replacement and independent objects, combining characters, and copying UTF-16 input before it is mutated. The self-recompiled compiler reruns those cases.

[Stage 33](../stage-33-shared-winforms-source/README.md) adds shared source for control initialization and the event handler bodies used by this demo.

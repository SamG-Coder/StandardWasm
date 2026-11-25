# Stage 33 · Shared WinForms application source

Run from the repository root.

    npm run demo -- stage-33-shared-winforms-source

Open the printed local URL. Editing Name updates the greeting, and Clear empties and refocuses the input. Run the native counterpart with:

    dotnet run -c Release --project demos/stage-32-winforms-textbox/Desktop

Both projects compile ProfileApplication.cs. The Windows project uses the native Form, TextBox, Label and Button classes. ProfileBrowser.csproj compiles the same file against browser implementations of those controls and includes the shared profile model. StandardWasm compiles that assembly to WebAssembly. Initialization sets Text, MaxLength and bounds. The shared handler bodies update the preview or clear and request focus.

The browser reads layout and captions from the initialized managed instances. The Windows host attaches TextChanged and Click and owns the native controls. The browser host delivers equivalent events from HTML, positions its input and button, draws the preview with WebGPU and applies managed focus requests. The browser Focus method queues a request for the host. Its return value indicates that the request was queued.

Run the integration checks from the repository root.

    node scripts/winforms-textbox.mjs --verify
    npm test

The native/browser test matches all three control rectangles, captions, the input limit, edits and focus. The compiler fixture compiles the same browser assembly in Node and Edge, compares the results with the CLR and replays them using the regenerated compiler.

# Stage 42 · Generate the shared WinForms Profile in the browser

Run the demo on Windows with Edge and WebGPU.

```sh
npm ci
npm run demo -- stage-42-generated-profile
```

The test suite builds the second-generation Wasm compiler. Edge loads that compiler, the [Profile .NET Standard library](../stage-34-shared-winforms-source/ProfileBrowser.csproj), and its export manifest. The compiler creates the WebAssembly module inside Edge. The browser checks that the compiler and application lack runtime imports, then drives the generated app with text entry, selection replacement, Backspace, and Clear. The runner verifies focus, matches the control geometry and text results against native WinForms, and checks for changed WebGPU pixels.

The runner keeps the CLR-produced Wasm on disk for a byte-for-byte comparison with the browser-produced module. The generated app page receives the DLL and export manifest. The evidence and both modules are written under `artifacts/profile-generated/`.

After a successful `npm test`, run `node scripts/winforms-textbox.mjs --generated` to open the generated Profile without the verifier.

# Stage 190 · Compiled window creation binding

Compile a .NET 10 fixture whose raw P/Invoke matches the SDK shape used by WinForms for `USER32.dll!CreateWindowExW`. The compiler forwards the 12 Wasm32 values to the browser host, including the two UTF-16 pointers and the window creation data address. Default wrapper values lower to zero. The return value becomes the SDK-shaped HWND.

Run `npm run demo -- stage-190-window-creation-binding`. The verifier records the call at the Wasm import boundary in Node and Edge. It checks argument order and bit patterns, imported guest memory, and nonzero and zero HWND returns. Stage 189 verifies the production host against actual Windows creation messages. The managed SDK wrapper owns pinning and last-error capture, and the first original WinForms window also requires the reverse managed callback bridge.

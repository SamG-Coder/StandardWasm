# Stage 190 · Compiled window creation binding

Compile a .NET 10 fixture whose raw P/Invoke matches the SDK shape used by WinForms for `USER32.dll!CreateWindowExW`. The compiler forwards the 12 Wasm32 values to the browser host, including the two UTF-16 pointers and the window creation data address. Default wrapper values lower to zero. The return value becomes the SDK-shaped HWND.

Run `npm run demo -- stage-190-window-creation-binding`. The verifier records the call at the Wasm import boundary in Node and Edge and checks argument order, bit patterns, imported guest memory, and nonzero and zero HWND returns. A second check sends the compiled call through the production host and observes the live HWND, the ordered popup creation messages, and system error 1407 for a missing class. Stage 189 compares those host messages with Windows x86. The managed SDK wrapper owns pinning and last-error capture. The original WinForms window path also needs the managed callback bridge.

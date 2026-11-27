# Stage 187 · Compiled window class bindings

Compile an ordinary C# DLL with SDK-shaped raw declarations for Win32 class
registration, class lookup and window procedure replacement. The generated
module calls the production Stage 184 browser host. The Stage 184 Windows
x86 oracle supplies the expected result and error codes.

Run `npm run demo -- stage-187-window-class-bindings`. For an independently
emitted compiler, set `STANDARDWASM_COMPILER_PATH` to its Wasm file first.

This stage verifies the raw import and register/lookup/replace ABI. Managed
reverse callbacks and original `CreateWindowExW` execution are separate
startup checks.

# Stage 345 · CoreLib Windows version out parameter

A native-backed local can be passed to the original CoreLib `RtlGetVersionEx` helper. The compiler preserves the managed initialization, size calculation, and size store. It verifies the generated pinned-byref method before routing the synchronous native call to the browser host. The frame owns the buffer across the entire call.

The browser compatibility profile defaults to Windows 10 version 10.0.19045. A host embedding the launcher can supply `platform.windowsVersion` with integer fields `major`, `minor`, and `build`. The host reports the guest profile for the 276-byte structure used by this CoreLib. It validates the pointer and caller-supplied structure size before writing.

```sh
npm run demo -- stage-345-corelib-version-out --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. The Windows x86 CLR provides a version profile for the host fixture. The runner checks the actual launcher import, structure writes and invalid arguments in Node and Edge. It expects the unchanged managed fixture to reproduce the native compiler diagnostic at `SearchValues.Create` with the pinned Windows x86 .NET 10.0.10 runtime. Its Windows execution supplies the oracle value. Full managed Wasm execution still requires that dependency. Evidence is written to `artifacts/stage-345-corelib-version-out/verified/`.

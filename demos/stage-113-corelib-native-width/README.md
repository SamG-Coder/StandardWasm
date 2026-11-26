# Stage 113 · CoreLib native width

Run `npm test` to build the self-hosted compiler, then run
`npm run demo -- stage-113-corelib-native-width`.

The stage needs matching .NET 10 win-x86 and win-x64 runtimes. The runner finds
the x86 host at `C:/Program Files (x86)/dotnet/dotnet.exe`, or at
`STANDARDWASM_X86_DOTNET` if set. It picks the newest 10.0 version that both
hosts list. `STANDARDWASM_X64_RUNTIME_PACK` overrides the x64 pack directory.
If the browser-wasm runtime pack for the same version is in the NuGet cache,
or `STANDARDWASM_BROWSER_WASM_PACK` points to it, the stage tests it too.

An ordinary net10.0 library calls `IntPtr.Size`, `UIntPtr.Size`,
`IntPtr.MaxValue`, `IntPtr.MinValue` and `UIntPtr.MaxValue`, and widens the
maxima to `long`. The oracle runs the same DLL on the 32-bit CLR, which is the
contract for the wasm32 target. Node and Edge then compile the consumer with the
original CoreLib IL from each pack:

- **win-x86 and browser-wasm** (`IntPtr.Size` IL is 4): each value matches the
  x86 CLR. Native integer results cross to JavaScript as i32 (decision 0027), so
  `UIntPtr.MaxValue` shows as -1 there and as 4294967295 once widened to `long`.
- **win-x64** (`IntPtr.Size` IL is 8): each width-dependent call returns status
  -7 at `[2, <callee MethodDef row>, -1]`. Bundle entry 2 is the x64 CoreLib.
  The output buffer stays unchanged.
- **x86 CoreLib with `IntPtr.Size` patched to 8**: the header still says I386
  and PE32, but the IL disagrees, so the compile is rejected the same way.
- **Metadata-only use**: `Allocate` uses CoreLib only for the System.Object root.
  It compiles and matches the CLR against both win-x86 and win-x64.

`artifacts/corelib-native-width/run-*/results.json` records each pack's
directory, version, PE machine, PE magic, CorFlags, native width read from IL,
and whether it was selected or rejected.

See [decision 0044](../../docs/decisions/0044-corelib-native-width.md).
